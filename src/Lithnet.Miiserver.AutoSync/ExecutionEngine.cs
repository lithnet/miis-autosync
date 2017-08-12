using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionEngine : MarshalByRefObject
    {
        private Dictionary<string, MAExecutor> maExecutors;

        private ServiceHost service;

        public ExecutorState State { get; set; }

        public ExecutionEngine()
        {
            this.service = EventService.CreateInstance();
            Logger.WriteLine("Initialized event service host");

            this.InitializeMAExecutors();
        }

        public void Start()
        {
            this.StartMAExecutors();
            this.State = ExecutorState.Idle;
        }

        public void Stop()
        {
            this.StopMAExecutors();
            this.State = ExecutorState.Stopped;
        }

        public void Pause()
        {
            List<Task> tasks = new List<Task>();
            foreach (MAExecutor x in this.maExecutors.Values)
            {
                if (x.State != ExecutorState.Disabled && x.State != ExecutorState.Paused && x.State != ExecutorState.Stopped)
                {
                    tasks.Add(Task.Run(() => x.Pause()));
                }
            }

            Task.WhenAll(tasks).ContinueWith(t => this.State = ExecutorState.Paused);
        }

        public void Resume()
        {
            foreach (MAExecutor x in this.maExecutors.Values)
            {
                if (x.State == ExecutorState.Paused)
                {
                    x.Resume();
                }
            }

            this.State = ExecutorState.Idle;
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
            this.GetExecutorOrThrow(managementAgentName).Stop();
        }

        public void Start(string managementAgentName)
        {
            this.GetExecutorOrThrow(managementAgentName).Start();
        }

        public void Pause(string managementAgentName)
        {
            this.GetExecutorOrThrow(managementAgentName).Pause();
        }

        public void Resume(string managementAgentName)
        {
            this.GetExecutorOrThrow(managementAgentName).Resume();
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

            foreach (MAConfigParameters config in Program.ActiveConfig.ManagementAgents)
            {
                MAExecutor x = new MAExecutor(config);
                x.StateChanged += this.X_StateChanged;
                this.maExecutors.Add(config.ManagementAgentName, x);
            }
        }

        private void StartMAExecutors()
        {
            if (RegistrySettings.ExecutionEngineEnabled)
            {
                foreach (MAExecutor x in this.maExecutors.Values)
                {
                    if (x.Configuration.IsNew)
                    {
                        Logger.WriteLine("{0}: Skipping management agent because it does not yet have any configuration defined", x.Configuration.ManagementAgentName);
                        continue;
                    }

                    if (x.Configuration.IsMissing)
                    {
                        Logger.WriteLine("{0}: Skipping management agent because it is missing from the Sync Engine", x.Configuration.ManagementAgentName);
                        continue;
                    }

                    if (x.Configuration.Disabled)
                    {
                        Logger.WriteLine("{0}: Skipping management agent because it has been disabled in config", x.Configuration.ManagementAgentName);
                        continue;
                    }

                    Task.Run(() => x.Start());
                }
            }
            else
            {
                Logger.WriteLine("Execution engine has been disabled");
            }
        }

        private void X_StateChanged(object sender, MAStatusChangedEventArgs e)
        {
            EventService.NotifySubscribers(e.Status);
        }

        private void StopMAExecutors()
        {
            if (this.maExecutors == null)
            {
                return;
            }

            List<Task> stopTasks = new List<Task>();

            foreach (MAExecutor x in this.maExecutors.Values)
            {
                stopTasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        x.Stop();
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
