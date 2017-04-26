using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lithnet.Logging;
using System.DirectoryServices.Protocols;
using System.Net;

namespace Lithnet.Miiserver.AutoSync
{
    public class ActiveDirectoryChangeTrigger : IMAExecutionTrigger
    {
        private const string LastLogonAttributeName = "lastLogon";
        private const string LastLogonTimeStampAttributeName = "lastLogonTimeStamp";
        private const string BadPasswordAttribute = "badPasswordTime";
        private const string ObjectClassAttribute = "objectClass";

        public event ExecutionTriggerEventHandler TriggerExecution;

        private DateTime nextTriggerAfter;

        public TimeSpan MaximumTriggerInterval { get; set; }

        private LdapConnection connection;

        private bool stopped;

        private IAsyncResult request;

        public string BaseDN { get; set; }

        public string[] ObjectClasses { get; set; }

        public NetworkCredential Credentials { get; set; }

        public string HostName { get; set; }

        public TimeSpan LastLogonTimestampOffset { get; set; }

        public bool Disabled { get; set; }

        public ActiveDirectoryChangeTrigger()
        {
            this.LastLogonTimestampOffset = TimeSpan.FromSeconds(60);
            this.MaximumTriggerInterval = TimeSpan.FromSeconds(60);
            this.Validate();
        }

        private void Fire()
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(MARunProfileType.DeltaImport));

            this.nextTriggerAfter = DateTime.Now.Add(this.MaximumTriggerInterval);

            Logger.WriteLine($"AD/LDS change detection trigger fired. Supressing further updates until {this.nextTriggerAfter}", LogLevel.Debug);
        }

        private void SetupListener()
        {
            this.stopped = false;
            LdapDirectoryIdentifier directory = new LdapDirectoryIdentifier(this.HostName);

            if (this.Credentials == null)
            {
                this.connection = new LdapConnection(directory);
            }
            else
            {
                this.connection = new LdapConnection(directory, this.Credentials);
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

            if (this.MaximumTriggerInterval == new TimeSpan(0))
            {
                this.MaximumTriggerInterval = TimeSpan.FromSeconds(60);
            }
            
            try
            {
                Logger.StartThreadLog();
                Logger.WriteLine("Starting AD/LDS change listener");
                Logger.WriteLine("Base DN {0}", this.BaseDN);
                Logger.WriteLine("Host name: {0}", this.HostName);
                Logger.WriteLine("Credentials: {0}", this.Credentials == null ? "(current user)" : this.Credentials.UserName);
                Logger.WriteLine("Object classes: {0}", string.Join(",", this.ObjectClasses));
                Logger.WriteLine("Minimum interval between triggers: {0}", this.MaximumTriggerInterval);
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

        public string Name => "AD/LDS change detection";

        public override string ToString()
        {
            return $"{this.Name}: {this.HostName}";
        }

        internal void Validate()
        {
            if (this.Disabled)
            {
                return;
            }

            if (this.MaximumTriggerInterval.TotalSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(this.MaximumTriggerInterval), "The MaximumTriggerInterval parameter must be greater than 0");
            }

            if (string.IsNullOrWhiteSpace(this.BaseDN))
            {
                throw new ArgumentNullException(nameof(this.BaseDN), "A BaseDN must be specified");
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
    }
}