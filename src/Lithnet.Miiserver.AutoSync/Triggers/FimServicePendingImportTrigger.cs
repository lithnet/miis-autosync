using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using Lithnet.ResourceManagement.Client;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class FimServicePendingImportTrigger : IMAExecutionTrigger
    {
        public Timer checkTimer;

        public int TimerIntervalSeconds { get; set; }

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

                ISearchResultCollection r = c.GetResources(xpath, 1, new string[] { "msidmCompletedTime" }, "msidmCompletedTime", false);

                Logger.WriteLine("Found {0} change{1}", LogLevel.Debug, r.Count, r.Count == 1 ? string.Empty : "s");

                if (r.Count > 0)
                {
                    this.lastCheckDateTime = r.First().Attributes["msidmCompletedTime"].DateTimeValue;

                    ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

                    if (registeredHandlers != null)
                    {
                        registeredHandlers(this, new ExecutionTriggerEventArgs(MARunProfileType.DeltaImport));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Change detection failed");
                Logger.WriteException(ex);
            }
        }

        public void Start()
        {
            this.checkTimer = new Timer(this.TimerIntervalSeconds * 1000);
            this.checkTimer.AutoReset = true;
            this.checkTimer.Elapsed += this.checkTimer_Elapsed;
            this.checkTimer.Start();
        }

        public void Stop()
        {
            if (this.checkTimer != null)
            {
                if (this.checkTimer.Enabled)
                {
                    this.checkTimer.Stop();
                }
            }
        }

        public string Name
        {
            get
            {
                return "FIM Service pending changes";
            }
        }

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}