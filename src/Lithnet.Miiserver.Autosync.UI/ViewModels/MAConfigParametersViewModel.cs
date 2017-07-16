using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using Lithnet.Miiserver.Client;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
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
            this.Commands.Add("Browse", new DelegateCommand(t => this.Browse()));
            this.Commands.Add("New", new DelegateCommand(t => this.New()));
            this.Commands.Add("Edit", new DelegateCommand(t => this.Edit(), u => this.CanEdit()));
        }

        public string DisplayName
        {
            get
            {
                string name = this.Model.ManagementAgentName ?? "Unknown MA";

                if (this.IsMissing)
                {
                    name += " (missing)";
                    return name;
                }

                if (this.IsNew)
                {
                    name += " (unconfigured)";
                }

                if (this.Disabled)
                {
                    name += " (disabled)";
                }

                return name;
            }
        }

        public bool IsEnabled => !this.IsMissing && !this.Disabled;

        public string ManagementAgentName => this.Model.ManagementAgentName;

        public bool IsMissing => this.Model.IsMissing;

        public bool IsNew
        {
            get => this.Model.IsNew;
            set => this.Model.IsNew = value;
        }
        
        public string MAControllerPath
        {
            get => this.Model.MAControllerPath;
            set => this.Model.MAControllerPath = value;

        }

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
                return this.Model.ManagementAgent?.RunProfiles.Select(t => t.Key);
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
                    if (!this.IsMissing)
                    {
                        this.GetAllowedTypesForMa(this.Model.ManagementAgent);
                    }
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
        
        private void New()
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.AddExtension = true;
            dialog.DefaultExt = "ps1";
            dialog.OverwritePrompt = true;
            dialog.Filter = "PowerShell script|*.ps1";

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                File.WriteAllText(dialog.FileName, Properties.Resources.PowerShellControllerScript);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save the file\n{ex.Message}", "Unable to save");
                return;
            }

            this.MAControllerPath = dialog.FileName;

            this.Edit();
        }

        private bool CanEdit()
        {
            return File.Exists(this.MAControllerPath);
        }

        private void Edit()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(this.MAControllerPath) { Verb = "Edit" };
                Process newProcess = new Process { StartInfo = startInfo };
                newProcess.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the file\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Browse()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (File.Exists(this.MAControllerPath))
            {
                try
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(this.MAControllerPath);
                    openFileDialog.FileName = Path.GetFileName(this.MAControllerPath);
                }
                catch
                {
                }
            }
            else
            {
                openFileDialog.FileName = "*.ps1";
            }

            openFileDialog.AddExtension = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.DefaultExt = "ps1";
            openFileDialog.Filter = "PowerShell script|*.ps1";

            if (openFileDialog.ShowDialog() == true)
            {
                this.MAControllerPath = openFileDialog.FileName;
            }
        }
    }
}
