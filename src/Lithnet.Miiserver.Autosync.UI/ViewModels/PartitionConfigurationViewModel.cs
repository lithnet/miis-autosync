using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lithnet.Common.Presentation;
using PropertyChanged;
using NLog;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class PartitionConfigurationViewModel : ViewModelBase<PartitionConfiguration>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private int originalVersion;

        private MAControllerConfigurationViewModel maconfig;

        public PartitionConfigurationViewModel(PartitionConfiguration model, MAControllerConfigurationViewModel maconfig)
            : base(model)
        {
            this.originalVersion = this.Model.Version;
            this.maconfig = maconfig;
            this.AddIsDirtyProperty(nameof(this.ScheduledImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.FullSyncRunProfileName));
            this.AddIsDirtyProperty(nameof(this.FullImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.ExportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.DeltaSyncRunProfileName));
            this.AddIsDirtyProperty(nameof(this.DeltaImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.ConfirmingImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.AutoImportIntervalMinutes));
            this.AddIsDirtyProperty(nameof(this.ScheduleImports));
        }

        internal void Commit()
        {
            this.originalVersion = this.Version;
            this.IsDirty = false;
        }

        private void IncrementVersion()
        {
            if (this.Model.Version > this.originalVersion)
            {
                return;
            }

            this.Model.Version++;
            logger.Trace($"{this.Name} config version change from {this.originalVersion} to {this.Model.Version}");

            this.RaisePropertyChanged(nameof(this.Version));
            this.RaisePropertyChanged(nameof(this.IsNew));
            this.RaisePropertyChanged(nameof(this.DisplayName));
        }

        [DependsOn(nameof(IsMissing), nameof(IsNew), nameof(Version))]
        public string DisplayName
        {
            get
            {
                string name = this.Model.Name ?? this.Model.ID.ToString();

                if (this.IsMissing)
                {
                    name += " (missing)";
                    return name;
                }

                if (this.IsNew)
                {
                    name += " (unconfigured)";
                }
                
                return name;
            }
        }

        public string Name => this.Model.Name ?? "Unknown MA";

        public Guid ID => this.Model.ID;
   
        public bool IsMissing => this.Model.IsMissing;

        public bool IsNew => this.Version == 0;

        public string ScheduledImportRunProfileName
        {
            get => this.Model.ScheduledImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.ScheduledImportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string ScheduledImportRunProfileToolTip => "Called automatically by AutoSync when the 'Schedule an import if it has been longer than x minutes since the last import' operation option has been specified";

        [AlsoNotifyFor(nameof(IsNew))]
        public int Version => this.Model.Version;

        public string FullSyncRunProfileName
        {
            get => this.Model.FullSyncRunProfileName ?? App.NullPlaceholder;
            set => this.Model.FullSyncRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string FullSyncRunProfileToolTip => "Not called by AutoSync automatically. Only called by custom PowerShell script triggers";

        public string FullImportRunProfileName
        {
            get => this.Model.FullImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.FullImportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string FullImportRunProfileToolTip => "Not called by AutoSync automatically. Only called by custom PowerShell script triggers";

        public string ExportRunProfileName
        {
            get => this.Model.ExportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.ExportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string ExportRunProfileToolTip => "Called automatically by AutoSync when staged exports are detected on a management agent";

        public string DeltaSyncRunProfileName
        {
            get => this.Model.DeltaSyncRunProfileName ?? App.NullPlaceholder;
            set => this.Model.DeltaSyncRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string DeltaSyncRunProfileToolTip => "Called automatically by AutoSync after an import operation has been performed";

        public string DeltaImportRunProfileName
        {
            get => this.Model.DeltaImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.DeltaImportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string DeltaImportRunProfileToolTip => "Called by certain triggers when changes have been detected in a connected system. If the management agent doesn't support delta imports, specify a full import profile";

        public string ConfirmingImportRunProfileName
        {
            get => this.Model.ConfirmingImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.ConfirmingImportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string ConfirmingImportRunProfileToolTip => "Called automatically by AutoSync after an export has been performed";


        public int AutoImportIntervalMinutes
        {
            get => this.Model.AutoImportIntervalMinutes;
            set => this.Model.AutoImportIntervalMinutes = value;
        }

        public bool ScheduleImports
        {
            get => this.Model.AutoImportEnabled;
            set => this.Model.AutoImportEnabled = value;
        }

        public IEnumerable<string> RunProfileNames => this.GetRunProfileNames(true);

        public IEnumerable<string> SingleStepRunProfileNames => this.GetRunProfileNames(false);

        private IEnumerable<string> GetRunProfileNames(bool includeMultiStep)
        {
            List<string> items = new List<string>();
            items.Add("(none)");

            if (this.IsMissing)
            {
                return items;
            }

            try
            {
                ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                c.InvokeThenClose(u => items.AddRange(c.GetManagementAgentRunProfileNamesForPartition(this.maconfig.ManagementAgentID, this.ID, includeMultiStep)));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unable to enumerate run profiles");
            }

            return items;
        }
    }
}