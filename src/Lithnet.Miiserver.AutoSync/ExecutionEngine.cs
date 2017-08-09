using System;
using System.Collections.Generic;
using System.Linq;
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
                this.maExecutors.Add(x);
            }

            foreach (MAExecutor x in this.maExecutors)
            {
                Task.Run(() => x.Start(this.token.Token));
            }
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
