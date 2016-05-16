using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using Lithnet.ResourceManagement.Client;
using Lithnet.Logging;
using System.DirectoryServices.Protocols;
using System.Xml;

namespace Lithnet.Miiserver.AutoSync
{
    public class ActiveDirectoryChangeTrigger : IMAExecutionTrigger
    {
        private const string lastLogonAttributeName = "lastLogon";
        private const string lastLogonTimeStampAttributeName = "lastLogonTimeStamp";
        private const string badPasswordAttribute = "badPasswordTime";
        private const string objectClassAttribute = "objectClass";

        private int LastLogonTimestampOffset;

        public event ExecutionTriggerEventHandler TriggerExecution;

        public Timer checkTimer;

        private ADListenerConfiguration config;

        private bool hasChanges = false;

        private LdapConnection connection;

        private bool stopped;

        public ActiveDirectoryChangeTrigger(ADListenerConfiguration config)
        {
            this.LastLogonTimestampOffset = config.LastLogonTimestampOffsetSeconds;
            config.Validate();
            this.config = config;
        }

        private void checkTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.hasChanges)
            {
                this.Fire();
                this.hasChanges = false;
            }
        }

        private void Fire()
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            if (registeredHandlers != null)
            {
                registeredHandlers(this, new ExecutionTriggerEventArgs(MARunProfileType.DeltaImport));
            }
        }

        private void SetupListener()
        {
            this.stopped = false;
            LdapDirectoryIdentifier directory = new LdapDirectoryIdentifier(this.config.HostName);
            if (this.config.Credentials == null)
            {
                this.connection = new LdapConnection(directory);
            }
            else
            {
                this.connection = new LdapConnection(directory, this.config.Credentials);
            }

            SearchRequest request = new SearchRequest(
                this.config.BaseDN,
                "(objectClass=*)",
                 SearchScope.Subtree,
                new string[] { objectClassAttribute, lastLogonAttributeName, lastLogonTimeStampAttributeName, badPasswordAttribute }
                );

            request.Controls.Add(new DirectoryNotificationControl());

            IAsyncResult result = this.connection.BeginSendRequest(
                request,
                TimeSpan.FromDays(100),
                PartialResultProcessing.ReturnPartialResultsAndNotifyCallback,
                Notify,
                request);
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

                PartialResultsCollection col = this.connection.GetPartialResults(result);

                if (this.hasChanges)
                {
                    return;
                }

                DateTime lastLogonOldestDate = DateTime.UtcNow.AddSeconds(-this.LastLogonTimestampOffset);

                foreach (SearchResultEntry r in col.OfType<SearchResultEntry>())
                {
                    IEnumerable<string> objectClasses = r.Attributes[objectClassAttribute].GetValues(typeof(string)).OfType<string>();

                    if (this.config.ObjectClasses.Intersect(objectClasses, StringComparer.OrdinalIgnoreCase).Any())
                    {
                        if (objectClasses.Contains("computer", StringComparer.OrdinalIgnoreCase) && !this.config.ObjectClasses.Contains("computer", StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        DateTime date1 = DateTime.MinValue;

                        if (r.Attributes.Contains(lastLogonAttributeName))
                        {
                            string ts = r.Attributes[lastLogonAttributeName][0] as string;
                            date1 = DateTime.FromFileTimeUtc(Convert.ToInt64(ts));

                            if (date1 > lastLogonOldestDate)
                            {
                                continue;
                            }
                        }

                        DateTime date2 = DateTime.MinValue;

                        if (r.Attributes.Contains(lastLogonTimeStampAttributeName))
                        {
                            string ts = r.Attributes[lastLogonTimeStampAttributeName][0] as string;
                            date2 = DateTime.FromFileTimeUtc(Convert.ToInt64(ts));

                            if (date2 > lastLogonOldestDate)
                            {
                                continue;
                            }
                        }


                        DateTime date3 = DateTime.MinValue;

                        if (r.Attributes.Contains(badPasswordAttribute))
                        {
                            string ts = r.Attributes[badPasswordAttribute][0] as string;
                            date3 = DateTime.FromFileTimeUtc(Convert.ToInt64(ts));

                            if (date3 > lastLogonOldestDate)
                            {
                                continue;
                            }
                        }
                        
                        Logger.WriteLine("AD change: {0}", r.DistinguishedName);
                        Logger.WriteLine("LL: {0}", date1.ToLocalTime());
                        Logger.WriteLine("TS: {0}", date2.ToLocalTime());
                        Logger.WriteLine("BP: {0}", date3.ToLocalTime());

                        this.hasChanges = true;
                        return;
                    }
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
            if (this.config.Disabled)
            {
                Logger.WriteLine("AD change listener disabled");
                return;
            }

            int minimumTriggerInterval = (this.config.MinimumTriggerIntervalSeconds == 0 ? 60 : this.config.MinimumTriggerIntervalSeconds);

            try
            {
                Logger.StartThreadLog();
                Logger.WriteLine("Starting AD change listener");
                Logger.WriteLine("Base DN {0}", this.config.BaseDN);
                Logger.WriteLine("Host name: {0}", this.config.HostName);
                Logger.WriteLine("Credentials: {0}", this.config.Credentials == null ? "(current user)" : this.config.Credentials.UserName);
                Logger.WriteLine("Object classes: {0}", string.Join(",", this.config.ObjectClasses));
                Logger.WriteLine("Minimum interval between triggers: {0} seconds", this.config.MinimumTriggerIntervalSeconds);
            }
            finally
            {
                Logger.EndThreadLog();
            }
            this.SetupListener();
            this.checkTimer = new System.Timers.Timer(minimumTriggerInterval * 1000);
            this.checkTimer.AutoReset = true;
            this.checkTimer.Elapsed += checkTimer_Elapsed;
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

            this.stopped = true;
        }

        public string Name
        {
            get
            {
                return "AD change detection";
            }
        }
    }
}