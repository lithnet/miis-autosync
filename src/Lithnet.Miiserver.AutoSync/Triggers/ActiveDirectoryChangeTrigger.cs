using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lithnet.Logging;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Runtime.Serialization;
using System.Xml;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "active-directory-change-trigger")]
    public class ActiveDirectoryChangeTrigger : IMAExecutionTrigger
    {
        private const string LastLogonAttributeName = "lastLogon";
        private const string LastLogonTimeStampAttributeName = "lastLogonTimeStamp";
        private const string BadPasswordAttribute = "badPasswordTime";
        private const string ObjectClassAttribute = "objectClass";

        public event ExecutionTriggerEventHandler TriggerExecution;

        private DateTime nextTriggerAfter;

        /// <summary>
        /// Gets or sets the interval of time that must pass between trigger events being fired. This prevents a trigger storm.
        /// </summary>
        [DataMember(Name = "min-interval")]
        public TimeSpan MinimumIntervalBetweenEvents { get; set; }

        private LdapConnection connection;

        private bool stopped;

        private IAsyncResult request;

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

        [DataMember(Name = "use-service-account-credentials")]
        public bool UseServiceAccountCredentials { get; set; } = true;

        public ActiveDirectoryChangeTrigger()
        {
            //this.LastLogonTimestampOffset = TimeSpan.FromSeconds(60);
            // this.MinimumIntervalBetweenEvents = TimeSpan.FromSeconds(60);
            //this.Validate();
            //this.Password = new ProtectedString();
            //this.Password.Value = "test".ToSecureString();
        }

        private void Fire()
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(MARunProfileType.DeltaImport));

            this.nextTriggerAfter = DateTime.Now.Add(this.MinimumIntervalBetweenEvents);

            Logger.WriteLine($"AD/LDS change detection trigger fired. Supressing further updates until {this.nextTriggerAfter}", LogLevel.Debug);
        }

        private void SetupListener()
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

        private void Notify(IAsyncResult result)
        {
            try
            {
                if (this.stopped)
                {
                    this.connection.EndSendRequest(result);
                    return;
                }

                PartialResultsCollection resultsCollection = this.connection.GetPartialResults(result);

                if (DateTime.Now < this.nextTriggerAfter)
                {
                    Trace.WriteLine("Discarding AD/LDS change because next trigger time has not been reached");
                    return;
                }

                DateTime lastLogonOldestDate = DateTime.UtcNow.Subtract(this.LastLogonTimestampOffset);

                foreach (SearchResultEntry r in resultsCollection.OfType<SearchResultEntry>())
                {
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

                    Logger.WriteLine("AD/LDS change: {0}", r.DistinguishedName);
                    Logger.WriteLine("LL: {0}", LogLevel.Debug, date1.ToLocalTime());
                    Logger.WriteLine("TS: {0}", LogLevel.Debug, date2.ToLocalTime());
                    Logger.WriteLine("BP: {0}", LogLevel.Debug, date3.ToLocalTime());

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
                    throw;
                }
            }
        }

        public void Start()
        {
            if (this.Disabled)
            {
                Logger.WriteLine("AD/LDS change listener disabled");
                return;
            }

            if (this.MinimumIntervalBetweenEvents.Ticks == 0)
            {
                this.MinimumIntervalBetweenEvents = TimeSpan.FromSeconds(60);
            }

            try
            {
                Logger.StartThreadLog();
                Logger.WriteLine("Starting AD/LDS change listener");
                Logger.WriteLine("Base DN {0}", this.BaseDN);
                Logger.WriteLine("Host name: {0}", this.HostName);
                Logger.WriteLine("Credentials: {0}", this.HasCredentials ? this.Username : "(current user)");
                Logger.WriteLine("Object classes: {0}", string.Join(",", this.ObjectClasses));
                Logger.WriteLine("Minimum interval between trigger events: {0}", this.MinimumIntervalBetweenEvents);
            }
            finally
            {
                Logger.EndThreadLog();
            }

            this.SetupListener();
        }

        public void Stop()
        {
            if (this.connection != null && this.request != null)
            {
                this.connection.Abort(this.request);
            }

            this.stopped = true;
        }

        public string DisplayName => $"{this.Type} ({this.HostName})";

        public string Type => "AD/LDS change detection";

        public string Description => $"{this.HostName}";

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

        public static ActiveDirectoryChangeTrigger CreateTrigger(ManagementAgent ma)
        {
            if (!ma.Category.Equals("AD", StringComparison.OrdinalIgnoreCase) && !ma.Category.Equals("ADAM", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The specified management agent is not an AD or LDS management agent");
            }

            ActiveDirectoryChangeTrigger config = new ActiveDirectoryChangeTrigger();

            string privateData = ma.ExportManagementAgent();

            XmlDocument d = new XmlDocument();
            d.LoadXml(privateData);
            
            XmlNode partitionNode = d.SelectSingleNode("/export-ma/ma-data/ma-partition-data/partition[selected=1 and custom-data/adma-partition-data[is-domain=1]]");

            config.HostName = d.SelectSingleNode("/export-ma/ma-data/private-configuration/adma-configuration/forest-name")?.InnerText;
            config.BaseDN = partitionNode?.SelectSingleNode("custom-data/adma-partition-data/dn")?.InnerText;
            config.ObjectClasses = partitionNode?.SelectNodes("filter/object-classes/object-class")?.OfType<XmlElement>().Where(t => t.InnerText != "container" && t.InnerText != "domainDNS" && t.InnerText != "organizationalUnit").Select(u => u.InnerText).ToArray();
            config.LastLogonTimestampOffset = new TimeSpan(0, 0, 300);
            config.MinimumIntervalBetweenEvents = new TimeSpan(0, 0, 60);
            config.UseServiceAccountCredentials = true;

            return config;
        }
    }
}