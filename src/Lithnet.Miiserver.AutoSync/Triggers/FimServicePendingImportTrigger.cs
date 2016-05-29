using System;
using System.Linq;
using System.Timers;
using Lithnet.ResourceManagement.Client;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class FimServicePendingImportTrigger : IMAExecutionTrigger
    {
        private Timer checkTimer;

        private int TimerIntervalSeconds { get; }

        private DateTime? lastCheckDateTime;

        private string fimSvcHostName;

        public FimServicePendingImportTrigger(string hostname)
        {
            this.TimerIntervalSeconds = 60;
            this.fimSvcHostName = hostname;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                ResourceManagementClient c = new ResourceManagementClient(this.fimSvcHostName);

                string xpath;

                if (this.lastCheckDateTime == null)
                {
                    xpath = "/Request";
                    Logger.WriteLine("No watermark available. Querying for latest request history item", LogLevel.Debug);
                }
                else
                {
                    xpath = string.Format("/Request[msidmCompletedTime > '{0}']", this.lastCheckDateTime.Value.ToResourceManagementServiceDateFormat(false));
                    Logger.WriteLine("Searching for changes since {0}", LogLevel.Debug, this.lastCheckDateTime.Value.ToResourceManagementServiceDateFormat(false));
                }

                ISearchResultCollection r = c.GetResources(xpath, 1, new[] { "msidmCompletedTime" }, "msidmCompletedTime", false);

                Logger.WriteLine("Found {0} change{1}", LogLevel.Debug, r.Count, r.Count == 1 ? string.Empty : "s");

                if (r.Count <= 0)
                {
                    return;
                }

                this.lastCheckDateTime = r.First().Attributes["msidmCompletedTime"].DateTimeValue;

                ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

                registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(MARunProfileType.DeltaImport));
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Change detection failed");
                Logger.WriteException(ex);
            }
        }

        public void Start()
        {
            this.checkTimer = new Timer
            {
                AutoReset = true,
                Interval = this.TimerIntervalSeconds*1000
            };

            this.checkTimer.Elapsed += this.checkTimer_Elapsed;
            this.checkTimer.Start();
        }

        public void Stop()
        {
            if (this.checkTimer == null)
            {
                return;
            }

            if (this.checkTimer.Enabled)
            {
                this.checkTimer.Stop();
            }
        }

        public string Name => "FIM Service pending changes";

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}