using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.Autosync.UI.Windows;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class MAConfigParametersViewModel : ViewModelBase<MAConfigParameters>
    {
        private List<Type> allowedTypes;

        public MAConfigParametersViewModel(MAConfigParameters model)
            : base(model)
        {
            this.Triggers = new MAExecutionTriggersViewModel(model.Triggers, this);
            this.Commands.Add("AddTrigger", new DelegateCommand(t => this.AddTrigger(), u => this.CanAddTrigger()));
            this.Commands.Add("RemoveTrigger", new DelegateCommand(t => this.RemoveTrigger(), u => this.CanRemoveTrigger()));
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

        private bool CanAddTrigger()
        {
            return !this.Model.IsMissing;
        }

        private void AddTrigger()
        {
            try
            {
                AddTriggerWindow window = new AddTriggerWindow();
                window.DataContext = this;
                this.GetAllowedTypesForMa(this.Model.ManagementAgent);

                if (window.ShowDialog() == true)
                {
                    if (this.SelectedTrigger == null)
                    {
                        return;
                    }

                    IMAExecutionTrigger instance = (IMAExecutionTrigger) Activator.CreateInstance(this.SelectedTrigger, this.Model.ManagementAgent);
                    this.Triggers.Add(instance, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occured.\n{ex.Message}", "Unable to create trigger");
                Trace.WriteLine(ex);
            }
        }

        public IEnumerable<Type> AllowedTriggers
        {
            get
            {
                if (this.allowedTypes == null)
                {
                    this.GetAllowedTypesForMa(this.Model.ManagementAgent);
                }

                return this.allowedTypes;
            }
        }

        public Type SelectedTrigger { get; set; }

        private void GetAllowedTypesForMa(ManagementAgent ma)
        {
            this.allowedTypes = new List<Type>();

            if (ActiveDirectoryChangeTrigger.CanCreateForMA(ma))
            {
                if (!this.Triggers.Any(t => t is ActiveDirectoryChangeTriggerViewModel))
                {
                    this.allowedTypes.Add(typeof(ActiveDirectoryChangeTrigger));
                }
            }

            if (FimServicePendingImportTrigger.CanCreateForMA(ma))
            {
                if (!this.Triggers.Any(t => t is FimServicePendingImportTriggerViewModel))
                {
                    this.allowedTypes.Add(typeof(FimServicePendingImportTrigger));
                }
            }

            if (IntervalExecutionTrigger.CanCreateForMA(ma))
            {
                this.allowedTypes.Add(typeof(IntervalExecutionTrigger));
            }

            if (PowerShellExecutionTrigger.CanCreateForMA(ma))
            {
                this.allowedTypes.Add(typeof(PowerShellExecutionTrigger));
            }

            if (ScheduledExecutionTrigger.CanCreateForMA(ma))
            {
                this.allowedTypes.Add(typeof(ScheduledExecutionTrigger));
            }
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
