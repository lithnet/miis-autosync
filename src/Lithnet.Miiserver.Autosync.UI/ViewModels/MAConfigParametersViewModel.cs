using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class MAConfigParametersViewModel : ViewModelBase<MAConfigParameters>
    {
        public MAConfigParametersViewModel(MAConfigParameters model)
            : base(model)
        {
            this.Triggers = new MAExecutionTriggersViewModel(model.Triggers);
            this.Commands.Add("AddTrigger", new DelegateCommand(t => this.AddTrigger()));
            this.Commands.Add("RemoveTrigger", new DelegateCommand(t => this.RemoveTrigger(), u=> this.CanRemoveTrigger()));
        }

        public string ManagementAgentName => this.Model?.ManagementAgentName ?? "Unknown MA";

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
        
        public MAExecutionTriggersViewModel Triggers { get; private set; }

        private void AddTrigger()
        {
        }

        private void RemoveTrigger()
        {
            MAExecutionTriggerViewModel selected = this.Triggers.FirstOrDefault(t => t.IsSelected);

            if (selected != null)
            {
                if (MessageBox.Show("Are you sure you want to remove the selected trigger?", "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.OK) == MessageBoxResult.OK)
                {
                    this.Triggers.Remove(selected);
                }
            }
        }

        private bool CanRemoveTrigger()
        {
            return this.Triggers.Any(t => t.IsSelected);
        }

        public void DoAutoDiscovery()
        {
            //this.model = MAConfigDiscovery.DoAutoRunProfileDiscovery(this.model.ManagementAgent);
        }
    }
}
