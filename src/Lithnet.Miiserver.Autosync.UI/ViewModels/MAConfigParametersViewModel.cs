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
    internal class MAConfigParametersViewModel
    {
        private MAConfigParameters model;

        public MAConfigParametersViewModel(MAConfigParameters model)
        {
            this.model = model;
        }

        public string MAName
        {
            get
            {
                return this.model?.ManagementAgent?.Name ?? "Unknown MA";
            }
        }

        public string ScheduledImportRunProfileName
        {
            get
            {
                return this.model.ScheduledImportRunProfileName;
            }
            set
            {
                this.model.ScheduledImportRunProfileName = value;
            }
        }

        public string FullSyncRunProfileName
        {
            get
            {
                return this.model.FullSyncRunProfileName;
            }
            set
            {
                this.model.FullSyncRunProfileName = value;
            }
        }

        public string FullImportRunProfileName
        {
            get
            {
                return this.model.FullImportRunProfileName;
            }
            set
            {
                this.model.FullImportRunProfileName = value;
            }
        }

        public string ExportRunProfileName
        {
            get
            {
                return this.model.ExportRunProfileName;
            }
            set
            {
                this.model.ExportRunProfileName = value;
            }
        }

        public bool DisableDefaultTriggers
        {
            get
            {
                return this.model.DisableDefaultTriggers;
            }
            set
            {
                this.model.DisableDefaultTriggers = value;
            }
        }

        public string DeltaSyncRunProfileName
        {
            get
            {
                return this.model.DeltaSyncRunProfileName;
            }
            set
            {
                this.model.DeltaSyncRunProfileName = value;
            }
        }

        public string DeltaImportRunProfileName
        {
            get
            {
                return this.model.DeltaImportRunProfileName;
            }
            set
            {
                this.model.DeltaImportRunProfileName = value;
            }
        }

        public string ConfirmingImportRunProfileName
        {
            get
            {
                return this.model.ConfirmingImportRunProfileName;
            }
            set
            {
                this.model.ConfirmingImportRunProfileName = value;
            }
        }

        public bool Disabled
        {
            get
            {
                return this.model.Disabled;
            }
            set
            {
                this.model.Disabled = value;
            }
        }

        public int AutoImportIntervalMinutes
        {
            get
            {
                return this.model.AutoImportIntervalMinutes;
            }
            set
            {
                this.model.AutoImportIntervalMinutes = value;
            }
        }

        public bool ScheduleImports
        {
            get
            {
                return this.model.AutoImportScheduling != AutoImportScheduling.Disabled;
            }
            set
            {
                this.model.AutoImportScheduling = value ? AutoImportScheduling.Enabled : AutoImportScheduling.Disabled;
            }
        }

        public AutoImportScheduling AutoImportScheduling
        {
            get
            {
                return this.model.AutoImportScheduling;
            }
            set
            {
                this.model.AutoImportScheduling = value;
            }
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
