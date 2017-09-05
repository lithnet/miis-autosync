using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "active-directory-change-trigger")]
    [Description(TypeDescription)]

    public class ActiveDirectoryChangeTrigger : MAExecutionTrigger
    {
        private const string TypeDescription = "AD/LDS change detection";

        private const string LastLogonAttributeName = "lastLogon";
        private const string LastLogonTimeStampAttributeName = "lastLogonTimeStamp";
        private const string BadPasswordAttribute = "badPasswordTime";
        private const string ObjectClassAttribute = "objectClass";

        private object lockObject;

        private DateTime nextTriggerAfter;

        /// <summary>
        /// Gets or sets the interval of time that must pass between trigger events being fired. This prevents a trigger storm.
        /// </summary>
        [DataMember(Name = "min-interval")]
        public TimeSpan MinimumIntervalBetweenEvents { get; set; }

        private LdapConnection connection;

        private bool stopped;

        private IAsyncResult request;

        public ActiveDirectoryChangeTrigger()
        {
            this.Initialize();
        }

        [DataMember(Name = "base-dn")]
        public string BaseDN { get; set; }

        [DataMember(Name = "object-classes")]
        public string[] ObjectClasses { get; set; }

        public bool HasCredentials => string.IsNullOrEmpty(this.Username);

        public NetworkCredential GetCredentialPackage()
        {
            if (!this.HasCredentials)
            {
                return null;
            }

            return new NetworkCredential(this.Username, this.Password?.Value);
        }

        [DataMember(Name = "username")]
        public string Username { get; set; }

        [DataMember(Name = "password")]
        public ProtectedString Password { get; set; }

        [DataMember(Name = "host-name")]
        public string HostName { get; set; }

        [DataMember(Name = "last-logon-offset")]
        public TimeSpan LastLogonTimestampOffset { get; set; }

        [DataMember(Name = "disabled")]
        public bool Disabled { get; set; }

        [DataMember(Name = "use-explicit-credentials")]
        public bool UseExplicitCredentials { get; set; }

        private void Fire()
        {
            this.Fire(MARunProfileType.DeltaImport);

            this.nextTriggerAfter = DateTime.Now.Add(this.MinimumIntervalBetweenEvents);
            Trace.WriteLine($"AD/LDS change detection trigger fired. Suppressing further updates until {this.nextTriggerAfter}");
        }

        private void SetupListener()
        {
            try
            {
                this.stopped = false;
                LdapDirectoryIdentifier directory = new LdapDirectoryIdentifier(this.HostName);

                if (this.HasCredentials)
                {
                    this.connection = new LdapConnection(directory, this.GetCredentialPackage());
                }
                else
                {
                    this.connection = new LdapConnection(directory);
                }

                SearchRequest r = new SearchRequest(
                    this.BaseDN,
                    "(objectClass=*)",
                    SearchScope.Subtree,
                    ActiveDirectoryChangeTrigger.ObjectClassAttribute,
                    ActiveDirectoryChangeTrigger.LastLogonAttributeName,
                    ActiveDirectoryChangeTrigger.LastLogonTimeStampAttributeName,
                    ActiveDirectoryChangeTrigger.BadPasswordAttribute);

                r.Controls.Add(new DirectoryNotificationControl());

                this.request = this.connection.BeginSendRequest(
                    r,
                    TimeSpan.FromDays(100),
                    PartialResultProcessing.ReturnPartialResultsAndNotifyCallback,
                    this.Notify,
                    r);
            }
            catch (Exception ex)
            {
                this.LogError("Could not start the listener", ex);

                if (MessageSender.CanSendMail())
                {
                    string messageContent = MessageBuilder.GetMessageBody(this.ManagementAgentName, this.Type, this.Description, DateTime.Now, true, ex);
                    MessageSender.SendMessage($"{this.ManagementAgentName}: {this.Type} trigger error", messageContent);
                }

                throw;
            }
        }

        private void Notify(IAsyncResult result)
        {
            lock (this.lockObject)
            {
                try
                {
                    if (this.stopped)
                    {
                        this.connection?.EndSendRequest(result);
                        return;
                    }

                    PartialResultsCollection resultsCollection = this.connection?.GetPartialResults(result);

                    if (resultsCollection == null)
                    {
                        Trace.WriteLine("Results collection was empty");
                        return;
                    }

                    if (DateTime.Now < this.nextTriggerAfter)
                    {
                        Trace.WriteLine("Discarding AD/LDS change because next trigger time has not been reached");
                        return;
                    }

                    DateTime lastLogonOldestDate = DateTime.UtcNow.Subtract(this.LastLogonTimestampOffset);

                    foreach (SearchResultEntry r in resultsCollection.OfType<SearchResultEntry>())
                    {
                        if (r.Attributes == null || !r.Attributes.Contains(ActiveDirectoryChangeTrigger.ObjectClassAttribute))
                        {
                            Trace.WriteLine($"Skipping entry {r.DistinguishedName} because the object class list was empty");
                            continue;
                        }

                        IList<string> objectClasses = r.Attributes[ActiveDirectoryChangeTrigger.ObjectClassAttribute].GetValues(typeof(string)).OfType<string>().ToList();

                        if (!this.ObjectClasses.Intersect(objectClasses, StringComparer.OrdinalIgnoreCase).Any())
                        {
                            continue;
                        }

                        if (objectClasses.Contains("computer", StringComparer.OrdinalIgnoreCase) && !this.ObjectClasses.Contains("computer", StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        DateTime date1 = DateTime.MinValue;

                        if (r.Attributes.Contains(ActiveDirectoryChangeTrigger.LastLogonAttributeName))
                        {
                            string ts = r.Attributes[ActiveDirectoryChangeTrigger.LastLogonAttributeName][0] as string;
                            date1 = DateTime.FromFileTimeUtc(Convert.ToInt64(ts));

                            if (date1 > lastLogonOldestDate)
                            {
                                continue;
                            }
                        }

                        DateTime date2 = DateTime.MinValue;

                        if (r.Attributes.Contains(ActiveDirectoryChangeTrigger.LastLogonTimeStampAttributeName))
                        {
                            string ts = r.Attributes[ActiveDirectoryChangeTrigger.LastLogonTimeStampAttributeName][0] as string;
                            date2 = DateTime.FromFileTimeUtc(Convert.ToInt64(ts));

                            if (date2 > lastLogonOldestDate)
                            {
                                continue;
                            }
                        }

                        DateTime date3 = DateTime.MinValue;

                        if (r.Attributes.Contains(ActiveDirectoryChangeTrigger.BadPasswordAttribute))
                        {
                            string ts = r.Attributes[ActiveDirectoryChangeTrigger.BadPasswordAttribute][0] as string;
                            date3 = DateTime.FromFileTimeUtc(Convert.ToInt64(ts));

                            if (date3 > lastLogonOldestDate)
                            {
                                continue;
                            }
                        }

                        this.Log($"AD/LDS change detected on {r.DistinguishedName}");
                        Trace.WriteLine($"LL: {date1.ToLocalTime()}");
                        Trace.WriteLine($"TS: {date2.ToLocalTime()}");
                        Trace.WriteLine($"BP: {date3.ToLocalTime()}");

                        this.Fire();
                    }
                }
                catch (LdapException ex)
                {
                    if (ex.ErrorCode == 85)
                    {
                        this.SetupListener();
                    }
                    else
                    {
                        this.LogError("The AD change listener encountered an unexpected error", ex);

                        if (MessageSender.CanSendMail())
                        {
                            string messageContent = MessageBuilder.GetMessageBody(this.ManagementAgentName, this.Type, this.Description, DateTime.Now, false, ex);
                            MessageSender.SendMessage($"{this.ManagementAgentName}: {this.Type} trigger error", messageContent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LogError("The AD change listener encountered an unexpected error", ex);

                    if (MessageSender.CanSendMail())
                    {
                        string messageContent = MessageBuilder.GetMessageBody(this.ManagementAgentName, this.Type, this.Description, DateTime.Now, false, ex);
                        MessageSender.SendMessage($"{this.ManagementAgentName}: {this.Type} trigger error", messageContent);
                    }
                }
            }
        }

        public override void Start(string managementAgentName)
        {
            this.ManagementAgentName = managementAgentName;

            if (this.Disabled)
            {
                this.Log("AD/LDS change listener disabled");
                return;
            }

            if (this.MinimumIntervalBetweenEvents.Ticks == 0)
            {
                this.MinimumIntervalBetweenEvents = TimeSpan.FromSeconds(60);
            }


            StringBuilder b = new StringBuilder();
            b.AppendLine("Starting AD/LDS change listener");

            b.AppendLine($"Base DN {this.BaseDN}");
            b.AppendLine($"Host name: {this.HostName}");
            if (this.HasCredentials)
            {
                b.AppendLine($"Credentials: {this.Username}");
            }
            else
            {
                b.AppendLine($"Credentials: (current user)");
            }

            b.AppendLine($"Object classes: {string.Join(",", this.ObjectClasses)}");
            b.AppendLine($"Minimum interval between trigger events: {this.MinimumIntervalBetweenEvents}");

            this.Log(b.ToString());

            this.SetupListener();
        }

        public override void Stop()
        {
            try
            {
                if (this.connection != null && this.request != null)
                {
                    this.connection.Abort(this.request);
                }

                this.stopped = true;
            }
            catch (Exception ex)
            {
                this.LogError("An error occurred trying to stop the ActiveDirectoryChangeTrigger", ex);
            }
        }

        public override string DisplayName => $"{this.Type} ({this.HostName})";

        public override string Type => TypeDescription;

        public override string Description => $"{this.HostName}";

        public override string ToString()
        {
            return $"{this.DisplayName}: {this.HostName}";
        }

        internal void Validate()
        {
            if (this.Disabled)
            {
                return;
            }

            if (this.MinimumIntervalBetweenEvents.TotalSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(this.MinimumIntervalBetweenEvents), $"The {nameof(this.MinimumIntervalBetweenEvents)} parameter must be greater than 0");
            }

            if (string.IsNullOrWhiteSpace(this.BaseDN))
            {
                throw new ArgumentNullException(nameof(this.BaseDN), "A base DN must be specified");
            }

            if (string.IsNullOrWhiteSpace(this.HostName))
            {
                throw new ArgumentNullException(nameof(this.HostName), "A host name must be specified");
            }

            if (this.ObjectClasses == null || this.ObjectClasses.Length == 0)
            {
                throw new ArgumentNullException(nameof(this.ObjectClasses), "One or more object classes must be specified");
            }
        }

        public static bool CanCreateForMA(ManagementAgent ma)
        {
            return ma.Category.Equals("AD", StringComparison.OrdinalIgnoreCase) ||
                   ma.Category.Equals("ADAM", StringComparison.OrdinalIgnoreCase);
        }

        public ActiveDirectoryChangeTrigger(ManagementAgent ma)
        {
            if (!ActiveDirectoryChangeTrigger.CanCreateForMA(ma))
            {
                throw new InvalidOperationException("The specified management agent is not an AD or LDS management agent");
            }

            string privateData = ma.ExportManagementAgent();

            XmlDocument d = new XmlDocument();
            d.LoadXml(privateData);

            XmlNode partitionNode = d.SelectSingleNode("/export-ma/ma-data/ma-partition-data/partition[selected=1 and custom-data/adma-partition-data[is-domain=1]]");

            this.HostName = d.SelectSingleNode("/export-ma/ma-data/private-configuration/adma-configuration/forest-name")?.InnerText;
            this.BaseDN = partitionNode?.SelectSingleNode("custom-data/adma-partition-data/dn")?.InnerText;
            this.ObjectClasses = partitionNode?.SelectNodes("filter/object-classes/object-class")?.OfType<XmlElement>().Where(t => t.InnerText != "container" && t.InnerText != "domainDNS" && t.InnerText != "organizationalUnit").Select(u => u.InnerText).ToArray();
            this.LastLogonTimestampOffset = new TimeSpan(0, 5, 0);
            this.MinimumIntervalBetweenEvents = new TimeSpan(0, 1, 0);
            this.UseExplicitCredentials = false;
        }

        private void Initialize()
        {
            this.lockObject = new object();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            this.Initialize();
        }
    }
}