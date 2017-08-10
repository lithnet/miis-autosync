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
        private List<MAExecutor> maExecutors;

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

        private void StartMAExecutors()
        {
            this.maExecutors = new List<MAExecutor>();
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
                x.ExecutingRunProfileChanged += this.X_ExecutingRunProfileChanged;
                x.ExecutionQueueChanged += this.X_ExecutionQueueChanged;
                x.StatusChanged += this.X_StatusChanged;
                this.maExecutors.Add(x);
            }

            foreach (MAExecutor x in this.maExecutors)
            {
                Task.Run(() => x.Start(this.token.Token));
            }
        }

        private void X_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            EventService.StatusChanged?.Invoke(e.Status, e.MAName);
        }

        private void X_ExecutionQueueChanged(object sender, ExecutionQueueChangedEventArgs e)
        {
            EventService.ExecutionQueueChanged?.Invoke(e.ExecutionQueue, e.MAName);
        }

        private void X_ExecutingRunProfileChanged(object sender, ExecutingRunProfileChangedEventArgs e)
        {
            EventService.ExecutingRunProfileChanged?.Invoke(e.ExecutingRunProfile, e.MAName);
        }

        private void StopMAExecutors()
        {
            if (this.maExecutors == null)
            {
                return;
            }

            this.token?.Cancel();

            List<Task> stopTasks = new List<Task>();

            foreach (MAExecutor x in this.maExecutors)
            {
                stopTasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        x.ExecutingRunProfileChanged -= this.X_ExecutingRunProfileChanged;
                        x.ExecutionQueueChanged -= this.X_ExecutionQueueChanged;
                        x.StatusChanged -= this.X_StatusChanged;
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
