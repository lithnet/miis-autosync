using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionEngine : MarshalByRefObject
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Dictionary<Guid, MAController> controllers;

        private ServiceHost npService;

        private ServiceHost tcpService;

        internal static object ServiceControlLock = new object();

        private CancellationTokenSource cancellationToken;

        public ControlState State { get; set; }

        public ExecutionEngine()
        {
            this.npService = EventService.CreateNetNamedPipeInstance();
            logger.Info("Initialized named pipe event service host");

            if (RegistrySettings.NetTcpServerEnabled)
            {
                this.tcpService = EventService.CreateNetTcpInstance();
                logger.Info("Initialized TCP event service host");
            }

            this.InitializeMAControllers();
        }

        public void Start()
        {
            lock (ExecutionEngine.ServiceControlLock)
            {
                this.State = ControlState.Starting;
                this.StartMAControllers();
                this.State = ControlState.Running;
            }
        }

        public void Stop(bool cancelRuns)
        {
            lock (ExecutionEngine.ServiceControlLock)
            {
                this.State = ControlState.Stopping;
                this.StopMAControllers(cancelRuns);
                this.State = ControlState.Stopped;
            }
        }

        public void RestartChangedControllers()
        {
            foreach (Guid item in this.GetManagementAgentsPendingRestart())
            {
                logger.Info($"Restarting controller '{item}' with new configuration");
                this.Stop(item, false);
                this.Start(item);
            }
        }

        public IList<Guid> GetManagementAgentsPendingRestart()
        {
            List<Guid> restartItems = new List<Guid>();

            foreach (MAControllerConfiguration newItem in Program.ActiveConfig.ManagementAgents)
            {
                if (!this.controllers.ContainsKey(newItem.ManagementAgentID))
                {
                    continue;
                }

                MAController e = this.controllers[newItem.ManagementAgentID];

                if ((e.ControlState == ControlState.Disabled || e.Configuration == null) && newItem.Disabled)
                {
                    continue;
                }

                if (e.ControlState == ControlState.Disabled && !newItem.Disabled)
                {
                    restartItems.Add(newItem.ManagementAgentID);
                    continue;
                }

                if (e.ControlState == ControlState.Stopped)
                {
                    continue;
                }

                if (e.Configuration == null)
                {
                    continue;
                }

                if (e.Configuration.Version != newItem.Version)
                {
                    restartItems.Add(newItem.ManagementAgentID);
                }
            }

            return restartItems;
        }

        public void ShutdownService()
        {
            try
            {
                if (this.npService != null)
                {
                    if (this.npService.State != CommunicationState.Closed)
                    {
                        this.npService.Close();
                    }
                }

                if (this.tcpService != null)
                {
                    if (this.tcpService.State != CommunicationState.Closed)
                    {
                        this.tcpService.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The service host did not shutdown cleanly");
            }

            this.npService = null;
            this.tcpService = null;
        }

        public void CancelRun(Guid managementAgentID)
        {
            MAController e = this.GetContorllerOrThrow(managementAgentID);

            lock (e)
            {
                e.CancelRun();
            }
        }

        public void Stop(Guid managementAgentID, bool cancelRun)
        {
            MAController e = this.GetContorllerOrThrow(managementAgentID);

            lock (e)
            {
                e.Stop(cancelRun, true, false);
            }
        }

        public void AddToExecutionQueue(Guid managementAgentID, string runProfileName)
        {
            MAController e = this.GetContorllerOrThrow(managementAgentID);
            if (e.ControlState != ControlState.Running)
            {
                return;
            }

            e.AddPendingActionIfNotQueued(runProfileName, "Manual entry");
        }

        public void Start(Guid managementAgentID)
        {
            MAControllerConfiguration c = Program.ActiveConfig.ManagementAgents.GetItemOrDefault(managementAgentID);

            if (c == null)
            {
                throw new InvalidOperationException($"There was no active configuration found for the management agent {managementAgentID}");
            }

            MAController e = this.GetContorllerOrThrow(managementAgentID);

            lock (e)
            {
                logger.Trace($"Starting {e.ManagementAgentName}");
                e.Start(c);
            }
        }

        internal IList<MAStatus> GetMAState()
        {
            List<MAStatus> states = new List<MAStatus>();

            if (this.controllers == null)
            {
                return states;
            }

            foreach (MAController x in this.controllers.Values)
            {
                states.Add(x.InternalStatus);
            }

            return states;
        }

        internal MAStatus GetMAState(Guid managementAgentID)
        {
            return this.GetContorllerOrThrow(managementAgentID).InternalStatus;
        }

        private void InitializeMAControllers()
        {
            this.controllers = new Dictionary<Guid, MAController>();

            foreach (ManagementAgent ma in ManagementAgent.GetManagementAgents())
            {
                MAController x = new MAController(ma);
                x.StateChanged += this.X_StateChanged;
                x.RunProfileExecutionComplete += this.X_RunProfileExecutionComplete;
                x.MessageLogged += this.X_MessageLogged;

                this.controllers.Add(ma.ID, x);
            }
        }

        private void X_MessageLogged(object sender, MessageLoggedEventArgs e)
        {
            EventService.NotifySubscribersOnMessageLogged(((MAController)sender).ManagementAgentID, e);
        }

        private void X_RunProfileExecutionComplete(object sender, RunProfileExecutionCompleteEventArgs e)
        {
            EventService.NotifySubscribersOnRunProfileExecutionComplete(((MAController)sender).ManagementAgentID, e);
        }

        private void X_StateChanged(object sender, MAStatusChangedEventArgs e)
        {
            EventService.NotifySubscribersOnStatusChange(e.Status);
        }

        private void StartMAControllers()
        {
            this.cancellationToken = new CancellationTokenSource();
            List<Task> startTasks = new List<Task>();

            foreach (MAControllerConfiguration c in Program.ActiveConfig.ManagementAgents)
            {
                if (c.IsMissing)
                {
                    logger.Warn($"{c.ManagementAgentName}: Skipping management agent because it is missing from the Sync Engine");
                    continue;
                }


                if (this.controllers.ContainsKey(c.ManagementAgentID))
                {
                    logger.Trace($"Starting {c.ManagementAgentName}");
                    startTasks.Add(Task.Factory.StartNew(() =>
                    {
                        MAController e = this.controllers[c.ManagementAgentID];
                        lock (e)
                        {
                            e.Start(c);
                        }
                    }, this.cancellationToken.Token));
                }
                else
                {
                    logger.Error($"Cannot start management agent controller '{c.ManagementAgentName}' because the management agent was not found");
                }
            }

            Task.WaitAll(startTasks.ToArray(), this.cancellationToken.Token);
        }

        private void StopMAControllers(bool cancelRun)
        {
            if (this.controllers == null)
            {
                return;
            }

            this.cancellationToken?.Cancel();

            List<Task> stopTasks = new List<Task>();

            foreach (MAController e in this.controllers.Values)
            {
                stopTasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        lock (e)
                        {
                            Thread.CurrentThread.SetThreadName($"Stop controller {e.ManagementAgentName}");
                            e.Stop(cancelRun, true, false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"The controller for {e.ManagementAgentName} throw an error while stopping");
                    }
                }));
            }

            logger.Info("Waiting for controllers to stop");

            if (!Task.WaitAll(stopTasks.ToArray(), 10000))
            {
                logger.Warn("Timeout waiting for controllers to stop");
            }
            else
            {
                logger.Info("Controllers stopped successfully");
            }

            this.State = ControlState.Stopped;
        }

        private MAController GetContorllerOrThrow(Guid managementAgentID)
        {
            if (this.controllers.ContainsKey(managementAgentID))
            {
                return this.controllers[managementAgentID];
            }
            else
            {
                throw new NoSuchManagementAgentException(managementAgentID.ToString());
            }
        }
    }
}
