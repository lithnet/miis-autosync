using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionEngine : MarshalByRefObject
    {
        private Dictionary<Guid, MAExecutor> maExecutors;

        private ServiceHost npService;

        private ServiceHost tcpService;
        
        internal static object ServiceControlLock = new object();

        private CancellationTokenSource cancellationToken;

        public ControlState State { get; set; }

        public ExecutionEngine()
        {
            this.npService = EventService.CreateNetNamedPipeInstance();
            Logger.WriteLine("Initialized named pipe event service host");

            if (RegistrySettings.NetTcpServerEnabled)
            {
                this.tcpService = EventService.CreateNetTcpInstance();
                Logger.WriteLine("Initialized TCP event service host");
            }

            this.InitializeMAExecutors();
        }

        public void Start()
        {
            lock (ExecutionEngine.ServiceControlLock)
            {
                this.State = ControlState.Starting;
                this.StartMAExecutors();
                this.State = ControlState.Running;
            }
        }

        public void Stop(bool cancelRuns)
        {
            lock (ExecutionEngine.ServiceControlLock)
            {
                this.State = ControlState.Stopping;
                this.StopMAExecutors(cancelRuns);
                this.State = ControlState.Stopped;
            }
        }

        public void RestartChangedExecutors()
        {
            foreach (Guid item in this.GetManagementAgentsPendingRestart())
            {
                Logger.WriteLine($"Restarting executor '{item}' with new configuration");
                this.Stop(item, false);
                this.Start(item);
            }
        }

        public IList<Guid> GetManagementAgentsPendingRestart()
        {
            List<Guid> restartItems = new List<Guid>();

            foreach (MAConfigParameters newItem in Program.ActiveConfig.ManagementAgents)
            {
                if (!this.maExecutors.ContainsKey(newItem.ManagementAgentID))
                {
                    continue;
                }

                MAExecutor e = this.maExecutors[newItem.ManagementAgentID];

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
                Logger.WriteLine("The service host did not shutdown cleanly");
                Logger.WriteException(ex);
            }

            this.npService = null;
            this.tcpService = null;
        }

        public void CancelRun(Guid managementAgentID)
        {
            MAExecutor e = this.GetExecutorOrThrow(managementAgentID);

            lock (e)
            {
                e.CancelRun();
            }
        }

        public void Stop(Guid managementAgentID, bool cancelRun)
        {
            MAExecutor e = this.GetExecutorOrThrow(managementAgentID);

            lock (e)
            {
                e.Stop(cancelRun);
            }
        }

        public void AddToExecutionQueue(Guid managementAgentID, string runProfileName)
        {
            MAExecutor e = this.GetExecutorOrThrow(managementAgentID);
            if (e.ControlState != ControlState.Running)
            {
                return;
            }

            e.AddPendingActionIfNotQueued(runProfileName, "Manual entry");
        }

        public void Start(Guid managementAgentID)
        {
            MAConfigParameters c = Program.ActiveConfig.ManagementAgents.GetItemOrDefault(managementAgentID);

            if (c == null)
            {
                throw new InvalidOperationException($"There was no active configuration found for the management agent {managementAgentID}");
            }

            MAExecutor e = this.GetExecutorOrThrow(managementAgentID);

            lock (e)
            {
                Trace.WriteLine($"Starting {e.ManagementAgentName}");
                e.Start(c);
            }
        }

        internal IList<MAStatus> GetMAState()
        {
            List<MAStatus> states = new List<MAStatus>();

            if (this.maExecutors == null)
            {
                return states;
            }

            foreach (MAExecutor x in this.maExecutors.Values)
            {
                states.Add(x.InternalStatus);
            }

            return states;
        }

        internal MAStatus GetMAState(Guid managementAgentID)
        {
            return this.GetExecutorOrThrow(managementAgentID).InternalStatus;
        }

        private void InitializeMAExecutors()
        {
            this.maExecutors = new Dictionary<Guid, MAExecutor>();

            foreach (ManagementAgent ma in ManagementAgent.GetManagementAgents())
            {
                MAExecutor x = new MAExecutor(ma);
                x.StateChanged += this.X_StateChanged;
                x.RunProfileExecutionComplete += this.X_RunProfileExecutionComplete;
                this.maExecutors.Add(ma.ID, x);
            }
        }

        private void X_RunProfileExecutionComplete(object sender, RunProfileExecutionCompleteEventArgs e)
        {
            EventService.NotifySubscribersOnRunProfileExecutionComplete(((MAExecutor)sender).ManagementAgentID, e);
        }

        private void X_StateChanged(object sender, MAStatusChangedEventArgs e)
        {
            EventService.NotifySubscribersOnStatusChange(e.Status);
        }

        private void StartMAExecutors()
        {
            this.cancellationToken = new CancellationTokenSource();

            foreach (MAConfigParameters c in Program.ActiveConfig.ManagementAgents)
            {
                if (c.IsMissing)
                {
                    Logger.WriteLine("{0}: Skipping management agent because it is missing from the Sync Engine", c.ManagementAgentName);
                    continue;
                }

                if (this.maExecutors.ContainsKey(c.ManagementAgentID))
                {
                    Trace.WriteLine($"Starting {c.ManagementAgentName}");
                    Task.Run(() =>
                    {
                        MAExecutor e = this.maExecutors[c.ManagementAgentID];
                        lock (e)
                        {
                            e.Start(c);
                        }
                    }, this.cancellationToken.Token);
                }
                else
                {
                    Logger.WriteLine($"Cannot start management agent executor '{c.ManagementAgentName}' because the management agent was not found");
                }
            }
        }

        private void StopMAExecutors(bool cancelRun)
        {
            if (this.maExecutors == null)
            {
                return;
            }

            this.cancellationToken?.Cancel();

            List<Task> stopTasks = new List<Task>();

            foreach (MAExecutor e in this.maExecutors.Values)
            {
                stopTasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        lock (e)
                        {
                            Thread.CurrentThread.SetThreadName($"Stop executor {e.ManagementAgentName}");
                            e.Stop(cancelRun);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"The executor for {e.ManagementAgentName} throw an error while stopping");
                        Logger.WriteException(ex);
                    }
                }));
            }

            Logger.WriteLine("Waiting for executors to stop");

            if (!Task.WaitAll(stopTasks.ToArray(), 10000))
            {
                Logger.WriteLine("Timeout waiting for executors to stop");
            }
            else
            {
                Logger.WriteLine("Executors stopped successfully");
            }

            this.State = ControlState.Stopped;
        }

        private MAExecutor GetExecutorOrThrow(Guid managementAgentID)
        {
            if (this.maExecutors.ContainsKey(managementAgentID))
            {
                return this.maExecutors[managementAgentID];
            }
            else
            {
                throw new NoSuchManagementAgentException(managementAgentID.ToString());
            }
        }
    }
}
