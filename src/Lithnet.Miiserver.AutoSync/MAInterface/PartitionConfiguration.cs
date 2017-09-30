using Lithnet.Miiserver.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class PartitionConfiguration
    {
        internal PartitionConfiguration(Partition p)
        {
            this.UpdateConfiguration(p);
            this.AutoImportIntervalMinutes = 60;
        }

        internal void UpdateConfiguration(Partition p)
        {
            this.Name = p.Name;
            this.ID = p.ID;
            this.IsSelected = p.Selected;
            this.IsMissing = false;
        }

        [DataMember(Name = "auto-import-enabled")]
        public bool AutoImportEnabled { get; set; }

        [DataMember(Name = "auto-import-interval")]
        public int AutoImportIntervalMinutes { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "id")]
        public Guid ID { get; set; }

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
