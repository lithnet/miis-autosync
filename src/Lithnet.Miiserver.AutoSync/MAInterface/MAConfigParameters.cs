using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Lithnet.Miiserver.Client;
using System.Text;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "management-agent")]
    [KnownType(typeof(ActiveDirectoryChangeTrigger))]
    [KnownType(typeof(FimServicePendingImportTrigger))]
    [KnownType(typeof(IntervalExecutionTrigger))]
    [KnownType(typeof(PowerShellExecutionTrigger))]
    [KnownType(typeof(ScheduledExecutionTrigger))]
    public class MAConfigParameters
    {
        private ManagementAgent ma;

        [DataMember(Name = "id")]
        public Guid ManagementAgentID { get; set; }

        [DataMember(Name = "name")]
        public string ManagementAgentName { get; set; }

        public bool IsMissing { get; set; }

        [DataMember(Name = "unconfigured", EmitDefaultValue = false)]
        public bool IsNew { get; set; }

        [DataMember(Name = "version")]
        public int Version { get; set; }

        public void ResolveManagementAgent()
        {
            try
            {
                Guid? id = Global.FindManagementAgent(this.ManagementAgentName, this.ManagementAgentID);

                if (id.HasValue)
                {
                    this.ManagementAgent = ManagementAgent.GetManagementAgent(id.Value);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Exception finding management agent {this.ManagementAgentID}/{this.ManagementAgentName}");
                Logger.WriteException(ex);
            }

            Logger.WriteLine($"Management agent could not be found. Name: '{this.ManagementAgentName}'. ID: '{this.ManagementAgentID}'");

            this.IsMissing = true;
        }

        public ManagementAgent ManagementAgent
        {
            get => this.ma;
            private set
            {
                this.ma = value;
                this.ManagementAgentID = this.ma?.ID ?? Guid.Empty;
                this.ManagementAgentName = this.ma?.Name;
            }
        }

        [DataMember(Name = "run-profile-confirming-import")]
        public string ConfirmingImportRunProfileName { get; set; }

        [DataMember(Name = "run-profile-delta-sync")]
        public string DeltaSyncRunProfileName { get; set; }

        [DataMember(Name = "run-profile-full-sync")]
        public string FullSyncRunProfileName { get; set; }

        [DataMember(Name = "run-profile-full-import")]
        public string FullImportRunProfileName { get; set; }

        [DataMember(Name = "run-profile-scheduled-import")]
        public string ScheduledImportRunProfileName { get; set; }

        [DataMember(Name = "run-profile-delta-import")]
        public string DeltaImportRunProfileName { get; set; }

        [DataMember(Name = "run-profile-export")]
        public string ExportRunProfileName { get; set; }

        [DataMember(Name = "controller-script-path")]
        public string MAControllerPath { get; set; }

        [DataMember(Name = "disabled")]
        public bool Disabled { get; set; }

        [DataMember(Name = "auto-import-scheduling")]
        public AutoImportScheduling AutoImportScheduling { get; set; }

        [DataMember(Name = "auto-import-interval")]
        public int AutoImportIntervalMinutes { get; set; }

        public MAConfigParameters(ManagementAgent ma)
        {
            this.ManagementAgent = ma;
            this.Triggers = new List<IMAExecutionTrigger>();
        }

        [DataMember(Name = "triggers")]
        public List<IMAExecutionTrigger> Triggers { get; private set; }
        
        [DataMember(Name = "lock-managementagents")]
        public HashSet<string> LockManagementAgents { get; set; }

        internal bool CanExport => this.ExportRunProfileName != null;

        internal bool CanImport => this.ScheduledImportRunProfileName != null || this.FullImportRunProfileName != null;

        internal bool CanAutoRun => this.DeltaSyncRunProfileName != null ||
                                    this.ConfirmingImportRunProfileName != null ||
                                    this.FullSyncRunProfileName != null ||
                                    this.FullImportRunProfileName != null ||
                                    this.ScheduledImportRunProfileName != null ||
                                    this.ExportRunProfileName != null;

        internal string GetRunProfileName(MARunProfileType type)
        {
            switch (type)
            {
                case MARunProfileType.DeltaImport:
                    return this.ScheduledImportRunProfileName;

                case MARunProfileType.FullImport:
                    return this.FullImportRunProfileName;

                case MARunProfileType.Export:
                    return this.ExportRunProfileName;

                case MARunProfileType.DeltaSync:
                    return this.DeltaSyncRunProfileName;

                case MARunProfileType.FullSync:
                    return this.FullSyncRunProfileName;

                default:
                case MARunProfileType.None:
                    throw new ArgumentException("Unknown run profile type");
            }
        }

        public override string ToString()
        {
            return this.ManagementAgentName ?? this.ManagementAgentID.ToString();
        }
    }
}
