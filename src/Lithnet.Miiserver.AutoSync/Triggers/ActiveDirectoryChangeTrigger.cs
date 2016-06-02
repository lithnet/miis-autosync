using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Lithnet.Logging;
using System.DirectoryServices.Protocols;

namespace Lithnet.Miiserver.AutoSync
{
    public class ActiveDirectoryChangeTrigger : IMAExecutionTrigger
    {
        private const string LastLogonAttributeName = "lastLogon";
        private const string LastLogonTimeStampAttributeName = "lastLogonTimeStamp";
        private const string BadPasswordAttribute = "badPasswordTime";
        private const string ObjectClassAttribute = "objectClass";

        private int lastLogonTimestampOffset;

        public event ExecutionTriggerEventHandler TriggerExecution;

        private Timer checkTimer;

        private ADListenerConfiguration config;

        private bool hasChanges;

        private LdapConnection connection;

        private bool stopped;

        public ActiveDirectoryChangeTrigger(ADListenerConfiguration config)
        {
            this.lastLogonTimestampOffset = config.LastLogonTimestampOffsetSeconds;
            config.Validate();
            this.config = config;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
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

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(MARunProfileType.DeltaImport));
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
                 ActiveDirectoryChangeTrigger.ObjectClassAttribute, 
                 ActiveDirectoryChangeTrigger.LastLogonAttributeName, 
                 ActiveDirectoryChangeTrigger.LastLogonTimeStampAttributeName, 
                 ActiveDirectoryChangeTrigger.BadPasswordAttribute);

            request.Controls.Add(new DirectoryNotificationControl());

            this.connection.BeginSendRequest(
                request,
                TimeSpan.FromDays(100),
                PartialResultProcessing.ReturnPartialResultsAndNotifyCallback, this.Notify,
                request);
        }

        private void Notify(IAsyncResult result)
        {
            PartialResultsCollection col;

            try
            {
                if (this.stopped)
                {
                    this.connection.EndSendRequest(result);
                    return;
                }
                
                col = this.connection.GetPartialResults(result);
                
                if (this.hasChanges)
                {
                    return;
                }

                DateTime lastLogonOldestDate = DateTime.UtcNow.AddSeconds(-this.lastLogonTimestampOffset);

                foreach (SearchResultEntry r in col.OfType<SearchResultEntry>())
                {
                    IList<string> objectClasses = r.Attributes[ActiveDirectoryChangeTrigger.ObjectClassAttribute].GetValues(typeof(string)).OfType<string>().ToList();

                    if (!this.config.ObjectClasses.Intersect(objectClasses, StringComparer.OrdinalIgnoreCase).Any())
                    {
                        continue;
                    }

                    if (objectClasses.Contains("computer", StringComparer.OrdinalIgnoreCase) && !this.config.ObjectClasses.Contains("computer", StringComparer.OrdinalIgnoreCase))
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

                    Logger.WriteLine("AD change: {0}", r.DistinguishedName);
                    Logger.WriteLine("LL: {0}", LogLevel.Debug, date1.ToLocalTime());
                    Logger.WriteLine("TS: {0}", LogLevel.Debug, date2.ToLocalTime());
                    Logger.WriteLine("BP: {0}", LogLevel.Debug, date3.ToLocalTime());

                    this.hasChanges = true;
                    return;
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
            finally
            {
                col = null;
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
            this.checkTimer = new Timer
            {
                AutoReset = true,
                Interval  = minimumTriggerInterval * 1000
            };

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

            this.stopped = true;
        }

        public string Name => "AD change detection";

        public override string ToString()
        {
            return $"{this.Name}: {this.config?.HostName}";
        }
    }
}