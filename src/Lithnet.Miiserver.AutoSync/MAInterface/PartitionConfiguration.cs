using System;
using System.Runtime.Serialization;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class PartitionConfiguration
    {
        private Guid id;

        internal PartitionConfiguration(Partition p)
        {
            this.UpdateConfiguration(p);
            this.AutoImportIntervalMinutes = 60;
        }

        internal void UpdateConfiguration(Partition p)
        {
            this.Name = p.Name;
            this.IsSelected = p.Selected;
            this.IsMissing = false;
            this.ID = p.ID;
        }

        internal PartitionConfigurationCollection Parent { get; set; }

        [DataMember(Name = "auto-import-enabled")]
        public bool AutoImportEnabled { get; set; }

        [DataMember(Name = "auto-import-interval")]
        public int AutoImportIntervalMinutes { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "id")]
        public Guid ID
        {
            get => this.id;
            set
            {
                if (this.id == value)
                {
                    return;
                }

                this.Parent?.UpdateKey(this, value);
                this.id = value;
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

        [DataMember(Name = "is-missing")]
        public bool IsMissing { get; set; }

        [DataMember(Name = "version")]
        public int Version { get; set; }

        [DataMember(Name = "is-selected")]
        public bool IsSelected { get; set; }

        public bool IsActive => !this.IsMissing && this.IsSelected;
    }
}
