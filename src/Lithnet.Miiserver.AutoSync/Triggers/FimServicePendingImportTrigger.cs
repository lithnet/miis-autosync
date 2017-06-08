using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Timers;
using System.Xml;
using Lithnet.ResourceManagement.Client;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "mim-service-pending-import-trigger")]
    public class FimServicePendingImportTrigger : IMAExecutionTrigger
    {
        private Timer checkTimer;

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        private DateTime? lastCheckDateTime;

        [DataMember(Name = "host-name")]
        public string HostName { get; set; }

        public FimServicePendingImportTrigger()
        {
        }

        public FimServicePendingImportTrigger(string hostname)
        {
            this.Interval = TimeSpan.FromSeconds(60);
            this.HostName = hostname;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                ResourceManagementClient c = new ResourceManagementClient(this.HostName);

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
                Interval = this.Interval.TotalMilliseconds
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

        public string Name => "MIM Service pending changes";

        public event ExecutionTriggerEventHandler TriggerExecution;
        
        public static string GetFimServiceHostName(ManagementAgent ma)
        {
            if (!ma.Category.Equals("FIM", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The specified management agent is not a MIM Service MA");
            }

            XmlNode privateData = ma.GetPrivateData();
            return privateData.SelectSingleNode("fimma-configuration/connection-info/serviceHost")?.InnerText;
        }

        public static FimServicePendingImportTrigger CreateTrigger(ManagementAgent ma)
        {
            string hostname = FimServicePendingImportTrigger.GetFimServiceHostName(ma);

            return new FimServicePendingImportTrigger(hostname);
        }

        public override string ToString()
        {
            return $"{this.Name}: {this.HostName}";
        }
    }
}