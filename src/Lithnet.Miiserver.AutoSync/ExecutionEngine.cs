using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionEngine : MarshalByRefObject
    {
        private Dictionary<string, MAExecutor> maExecutors;

        private ServiceHost service;

        internal static object ServiceControlLock = new object();

        private CancellationTokenSource cancellationToken;

        public ExecutorState State { get; set; }

        public ExecutionEngine()
        {
            this.service = EventService.CreateInstance();
            Logger.WriteLine("Initialized event service host");

            this.InitializeMAExecutors();
        }

        public void Start()
        {
            lock (ExecutionEngine.ServiceControlLock)
            {
                this.State = ExecutorState.Starting;
                this.StartMAExecutors();
                this.State = ExecutorState.Running;
            }
        }

        public void Stop()
        {
            lock (ExecutionEngine.ServiceControlLock)
            {
                this.State = ExecutorState.Stopping;
                this.StopMAExecutors();
                this.State = ExecutorState.Stopped;
            }
        }

        public void RestartChangedExecutors()
        {
            foreach (string item in this.GetManagementAgentsPendingRestart())
            {
                Logger.WriteLine($"Restarting executor '{item}' with new configuration");
                this.Stop(item);
                this.Start(item);
            }
        }

        public IList<string> GetManagementAgentsPendingRestart()
        {
            List<string> restartItems = new List<string>();

            foreach (MAConfigParameters newItem in Program.ActiveConfig.ManagementAgents)
            {
                if (!this.maExecutors.ContainsKey(newItem.ManagementAgentName))
                {
                    continue;
                }

                MAExecutor e = this.maExecutors[newItem.ManagementAgentName];

                if ((e.ControlState == ControlState.Disabled || e.Configuration == null) && newItem.Disabled)
                {
                    continue;
                }

                if (e.ControlState == ControlState.Disabled && !newItem.Disabled)
                {
                    restartItems.Add(newItem.ManagementAgentName);
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
                    restartItems.Add(newItem.ManagementAgentName);
                }
            }

            return restartItems;
        }

        public void ShutdownService()
        {
            try
            {
                if (this.service != null)
                {
                    if (this.service.State != CommunicationState.Closed)
                    {
                        this.service.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("The service host did not shutdown cleanly");
                Logger.WriteException(ex);
            }

            this.service = null;
        }

        public void Stop(string managementAgentName)
        {
            MAExecutor e = this.GetExecutorOrThrow(managementAgentName);

            lock (e)
            {
                e.Stop();
            }
        }

        public void Start(string managementAgentName)
        {
            MAConfigParameters c = Program.ActiveConfig.ManagementAgents.GetItemOrDefault(managementAgentName);

            if (c == null)
            {
                throw new InvalidOperationException($"There was no active configuration found for the management agent {managementAgentName}");
            }

            MAExecutor e = this.GetExecutorOrThrow(managementAgentName);
            lock (e)
            {
                Trace.WriteLine($"Starting {managementAgentName}");
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

        internal MAStatus GetMAState(string managementAgentName)
        {
            return this.GetExecutorOrThrow(managementAgentName).InternalStatus;
        }

        private void InitializeMAExecutors()
        {
            this.maExecutors = new Dictionary<string, MAExecutor>(StringComparer.OrdinalIgnoreCase);

            foreach (ManagementAgent ma in ManagementAgent.GetManagementAgents())
            {
                MAExecutor x = new MAExecutor(ma);
                x.StateChanged += this.X_StateChanged;
                x.RunProfileExecutionComplete += this.X_RunProfileExecutionComplete;
                this.maExecutors.Add(ma.Name, x);
            }
        }

        private void X_RunProfileExecutionComplete(object sender, string runProfileName, string result)
        {
            EventService.NotifySubscribersOnRunProfileExecutionComplete(((MAExecutor)sender).ManagementAgentName, runProfileName, result);
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

                if (this.maExecutors.ContainsKey(c.ManagementAgentName))
                {
                    Trace.WriteLine($"Starting {c.ManagementAgentName}");
                    Task.Run(() =>
                    {
                        MAExecutor e = this.maExecutors[c.ManagementAgentName];
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

        private void StopMAExecutors()
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
                            e.Stop();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }));
            }

            Logger.WriteLine("Waiting for executors to stop");

            if (!Task.WaitAll(stopTasks.ToArray(), 90000))
            {
                Logger.WriteLine("Timeout waiting for executors to stop");
                throw new TimeoutException();
            }
            else
            {
                Logger.WriteLine("Executors stopped successfully");
            }

            this.State = ExecutorState.Stopped;
        }

        private MAExecutor GetExecutorOrThrow(string managementAgentName)
        {
            if (this.maExecutors.ContainsKey(managementAgentName))
            {
                return this.maExecutors[managementAgentName];
            }
            else
            {
                throw new NoSuchManagementAgentException(managementAgentName);
            }
        }
    }
}
