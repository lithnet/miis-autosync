using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Timers;
using System.Xml;
using Lithnet.ResourceManagement.Client;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "mim-service-pending-import-trigger")]
    [Description(TypeDescription)]

    public class FimServicePendingImportTrigger : MAExecutionTrigger
    {
        private const string TypeDescription = "MIM service change detection";

        private Timer checkTimer;

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        private DateTime? lastCheckDateTime;

        [DataMember(Name = "host-name")]
        public string HostName { get; set; }

        private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                ResourceManagementClient c = new ResourceManagementClient(this.HostName);

                string xpath;

                if (this.lastCheckDateTime == null)
                {
                    xpath = "/Request";
                    Trace.WriteLine("No watermark available. Querying for latest request history item");
                }
                else
                {
                    xpath = string.Format("/Request[msidmCompletedTime > '{0}']", this.lastCheckDateTime.Value.ToResourceManagementServiceDateFormat(false));
                    Trace.WriteLine($"Searching for changes since {this.lastCheckDateTime.Value.ToResourceManagementServiceDateFormat(false)}");
                }

                ISearchResultCollection r = c.GetResources(xpath, 1, new[] { "msidmCompletedTime" }, "msidmCompletedTime", false);

               Trace.WriteLine($"Found {r.Count} change{r.Count.Pluralize()}");

                if (r.Count <= 0)
                {
                    return;
                }

                this.lastCheckDateTime = r.First().Attributes["msidmCompletedTime"].DateTimeValue;
             
                this.Fire(MARunProfileType.DeltaImport);
            }
            catch (Exception ex)
            {
                this.LogError("Change detection failed", ex);

                if (MessageSender.CanSendMail())
                {
                    string messageContent = MessageBuilder.GetMessageBody(this.ManagementAgentName, this.Type, this.Description, DateTime.Now, false, ex);
                    MessageSender.SendMessage($"{this.ManagementAgentName}: {this.Type} trigger error", messageContent);
                }
            }
        }

        public override void Start(string managementAgentName)
        {
            this.ManagementAgentName = managementAgentName;

            this.checkTimer = new Timer
            {
                AutoReset = true,
                Interval = this.Interval.TotalMilliseconds
            };

            this.checkTimer.Elapsed += this.CheckTimer_Elapsed;
            this.checkTimer.Start();
        }

        public override void Stop()
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

        public static void CreateMpr(string hostname, NetworkCredential creds, string accountName, string setName, string mprName)
        {
            ResourceManagementClient c = new ResourceManagementClient(hostname, creds);

            Dictionary<string, object> keys = new Dictionary<string, object>();
            string[] split = Global.GetNtAccountName(accountName);

            if (split.Length > 1)
            {
                keys.Add("Domain", split[0]);
                keys.Add("AccountName", split[1]);
            }
            else
            {
                keys.Add("AccountName", accountName);
            }

            ResourceObject user = c.GetResourceByKey("Person", keys);

            if (user == null)
            {
                Trace.WriteLine($"Person {accountName} was not found. Creating");
                user = c.CreateResource("Person");
                SecurityIdentifier sid = (SecurityIdentifier)new NTAccount(accountName).Translate(typeof(SecurityIdentifier));
                user.SetValue("AccountName", split[1]);
                user.SetValue("Domain", split[0]);

                byte[] sidBytes = new byte[sid.BinaryLength];
                sid.GetBinaryForm(sidBytes, 0);
                user.SetValue("ObjectSID", sidBytes);
                user.Save();
            }

            ResourceObject set = c.GetResourceByKey("Set", "DisplayName", setName);

            if (set == null)
            {
                Trace.WriteLine($"Set {setName} was not found");
                set = c.CreateResource("Set");
            }

            set.SetValue("DisplayName", setName);
            set.AddValue("ExplicitMember", user);
            set.SetValue("Description", "Contains the Lithnet AutoSync service account");
            set.Save();
            Trace.WriteLine($"Set {setName} saved");

            ResourceObject allRequestsSet = c.GetResourceByKey("Set", "DisplayName", "All Requests");

            if (allRequestsSet == null)
            {
                Trace.WriteLine("Set All Requests was not found");
                allRequestsSet = c.CreateResource("Set");
                allRequestsSet.SetValue("DisplayName", "All Requests");
                allRequestsSet.SetValue("Filter", "<Filter xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" Dialect=\"http://schemas.microsoft.com/2006/11/XPathFilterDialect\" xmlns=\"http://schemas.xmlsoap.org/ws/2004/09/enumeration\">/Request</Filter>");
                allRequestsSet.Save();
                Trace.WriteLine($"Set All Requests created");
            }

            ResourceObject mpr = c.GetResourceByKey("ManagementPolicyRule", "DisplayName", mprName);

            if (mpr == null)
            {
                Trace.WriteLine($"MPR {mprName} does not exist");
                mpr = c.CreateResource("ManagementPolicyRule");
            }

            mpr.SetValue("DisplayName", mprName);
            mpr.SetValue("Description", "Allows the Lithnet AutoSync service account access to read the msidmCompletedTime attribute from Request objects");
            mpr.SetValue("ActionParameter", "msidmCompletedTime");
            mpr.SetValue("ActionType", "Read");
            mpr.SetValue("GrantRight", true);
            mpr.SetValue("Disabled", false);
            mpr.SetValue("ManagementPolicyRuleType", "Request");
            mpr.SetValue("ResourceCurrentSet", allRequestsSet);
            mpr.SetValue("PrincipalSet", set);
            mpr.Save();
            Trace.WriteLine($"MPR {mprName} saved");

        }

        public override string DisplayName => $"{this.Type} ({this.Description})";

        public override string Type => TypeDescription;

        public override string Description => $"{this.HostName}";
        
        private static string GetFimServiceHostName(ManagementAgent ma)
        {
            XmlNode privateData = ma.GetPrivateData();
            return privateData.SelectSingleNode("fimma-configuration/connection-info/serviceHost")?.InnerText;
        }

        public FimServicePendingImportTrigger(ManagementAgent ma)
        {
            if (!FimServicePendingImportTrigger.CanCreateForMA(ma))
            {
                throw new InvalidOperationException("The specified management agent is not a MIM Service MA");
            }

            this.HostName = FimServicePendingImportTrigger.GetFimServiceHostName(ma);
            this.Interval = TimeSpan.FromSeconds(60);
        }

        public static bool CanCreateForMA(ManagementAgent ma)
        {
            return ma.Category.Equals("FIM", StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return $"{this.DisplayName}: {this.HostName}";
        }
    }
}