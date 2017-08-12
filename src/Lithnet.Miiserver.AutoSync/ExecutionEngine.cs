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

        private CancellationTokenSource token;

        private ServiceHost service;

        public ExecutionEngine()
        {
            this.service = EventService.CreateInstance();
            Logger.WriteLine("Initialized event service host");
        }

        public void Start()
        {
            this.StartMAExecutors();
        }

        public void Stop()
        {
            this.StopMAExecutors();
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

        internal MAStatus GetMAState(string maName)
        {
            if (this.maExecutors.ContainsKey(maName))
            {
                return this.maExecutors[maName].InternalStatus;
            }
            else
            {
                return null;
            }
        }

        private void StartMAExecutors()
        {
            this.maExecutors = new Dictionary<string, MAExecutor>(StringComparer.OrdinalIgnoreCase);
            this.token = new CancellationTokenSource();

            foreach (MAConfigParameters config in Program.ActiveConfig.ManagementAgents)
            {
                if (config.IsNew)
                {
                    Logger.WriteLine("{0}: Skipping management agent because it does not yet have any configuration defined", config.ManagementAgentName);
                    continue;
                }

                if (config.IsMissing)
                {
                    Logger.WriteLine("{0}: Skipping management agent because it is missing from the Sync Engine", config.ManagementAgentName);
                    continue;
                }

                if (config.Disabled)
                {
                    Logger.WriteLine("{0}: Skipping management agent because it has been disabled in config", config.ManagementAgentName);
                    continue;
                }

                MAExecutor x = new MAExecutor(config);
                x.StateChanged += this.X_StateChanged;
                this.maExecutors.Add(config.ManagementAgentName, x);
            }

            foreach (MAExecutor x in this.maExecutors.Values)
            {
                Task.Run(() => x.Start(this.token.Token));
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

            this.token?.Cancel();

            List<Task> stopTasks = new List<Task>();

            foreach (MAExecutor x in this.maExecutors.Values)
            {
                stopTasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        x.StateChanged -= this.X_StateChanged;
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
        }
    }
}
