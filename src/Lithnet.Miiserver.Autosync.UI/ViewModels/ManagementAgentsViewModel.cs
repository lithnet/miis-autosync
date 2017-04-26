using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.Autosync.UI
{
    [ImplementPropertyChanged]
    internal class ManagementAgentsViewModel
    {
        private MAConfigParameters model;

        public ManagementAgentsViewModel(MAConfigParameters model)
        {
            this.model = model;
        }

        public string MAName => this.model?.ManagementAgent?.Name ?? "Unknown MA";

        public string ScheduledImportRunProfileName
        {
            get => this.model.ScheduledImportRunProfileName;
            set => this.model.ScheduledImportRunProfileName = value;
        }

        public string FullSyncRunProfileName
        {
            get => this.model.FullSyncRunProfileName;
            set => this.model.FullSyncRunProfileName = value;
        }

        public string FullImportRunProfileName
        {
            get => this.model.FullImportRunProfileName;
            set => this.model.FullImportRunProfileName = value;
        }

        public string ExportRunProfileName
        {
            get => this.model.ExportRunProfileName;
            set => this.model.ExportRunProfileName = value;
        }

        public bool DisableDefaultTriggers
        {
            get => this.model.DisableDefaultTriggers;
            set => this.model.DisableDefaultTriggers = value;
        }

        public string DeltaSyncRunProfileName
        {
            get => this.model.DeltaSyncRunProfileName;
            set => this.model.DeltaSyncRunProfileName = value;
        }

        public string DeltaImportRunProfileName
        {
            get => this.model.DeltaImportRunProfileName;
            set => this.model.DeltaImportRunProfileName = value;
        }

        public string ConfirmingImportRunProfileName
        {
            get => this.model.ConfirmingImportRunProfileName;
            set => this.model.ConfirmingImportRunProfileName = value;
        }

        public bool Disabled
        {
            get => this.model.Disabled;
            set => this.model.Disabled = value;
        }

        public int AutoImportIntervalMinutes
        {
            get => this.model.AutoImportIntervalMinutes;
            set => this.model.AutoImportIntervalMinutes = value;
        }

        public bool ScheduleImports
        {
            get => this.model.AutoImportScheduling != AutoImportScheduling.Disabled;
            set => this.model.AutoImportScheduling = value ? AutoImportScheduling.Enabled : AutoImportScheduling.Disabled;
        }

        public AutoImportScheduling AutoImportScheduling
        {
            get => this.model.AutoImportScheduling;
            set => this.model.AutoImportScheduling = value;
        }

        public IEnumerable<string> RunProfileNames
        {
            get
            {
                yield return "DI";
                yield return "DS";
                yield return "EALL";
                yield return "FI";
                yield return "FS";
                // return this.model.ManagementAgent.RunProfiles.Select(t => t.Key);
            }
        }
        
        public void DoAutoDiscovery()
        {
            this.model = MAConfigDiscovery.DoAutoRunProfileDiscovery(this.model.ManagementAgent);
        }
    }
}
