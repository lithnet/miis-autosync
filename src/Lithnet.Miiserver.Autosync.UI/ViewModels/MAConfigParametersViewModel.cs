using System;
using System.Collections.Generic;
using System.Linq;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;

namespace Lithnet.Miiserver.Autosync.UI
{
    public class MAConfigParametersViewModel : ViewModelBase<MAConfigParameters>
    {
        public MAConfigParametersViewModel(MAConfigParameters model)
            : base(model)
        {
        }

        public string MAName => this.Model?.ManagementAgentName ?? "Unknown MA";

        public string ScheduledImportRunProfileName
        {
            get => this.Model.ScheduledImportRunProfileName;
            set => this.Model.ScheduledImportRunProfileName = value;
        }

        public string FullSyncRunProfileName
        {
            get => this.Model.FullSyncRunProfileName;
            set => this.Model.FullSyncRunProfileName = value;
        }

        public string FullImportRunProfileName
        {
            get => this.Model.FullImportRunProfileName;
            set => this.Model.FullImportRunProfileName = value;
        }

        public string ExportRunProfileName
        {
            get => this.Model.ExportRunProfileName;
            set => this.Model.ExportRunProfileName = value;
        }

        public string DeltaSyncRunProfileName
        {
            get => this.Model.DeltaSyncRunProfileName;
            set => this.Model.DeltaSyncRunProfileName = value;
        }

        public string DeltaImportRunProfileName
        {
            get => this.Model.DeltaImportRunProfileName;
            set => this.Model.DeltaImportRunProfileName = value;
        }

        public string ConfirmingImportRunProfileName
        {
            get => this.Model.ConfirmingImportRunProfileName;
            set => this.Model.ConfirmingImportRunProfileName = value;
        }

        public bool Disabled
        {
            get => this.Model.Disabled;
            set => this.Model.Disabled = value;
        }

        public int AutoImportIntervalMinutes
        {
            get => this.Model.AutoImportIntervalMinutes;
            set => this.Model.AutoImportIntervalMinutes = value;
        }

        public bool ScheduleImports
        {
            get => this.Model.AutoImportScheduling != AutoImportScheduling.Disabled;
            set => this.Model.AutoImportScheduling = value ? AutoImportScheduling.Enabled : AutoImportScheduling.Disabled;
        }

        public AutoImportScheduling AutoImportScheduling
        {
            get => this.Model.AutoImportScheduling;
            set => this.Model.AutoImportScheduling = value;
        }

        public IEnumerable<string> RunProfileNames
        {
            get
            {
                return this.Model.ManagementAgent.RunProfiles.Select(t => t.Key);
            }
        }
        
        public void DoAutoDiscovery()
        {
            //this.model = MAConfigDiscovery.DoAutoRunProfileDiscovery(this.model.ManagementAgent);
        }
    }
}
