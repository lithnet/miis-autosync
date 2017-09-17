using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using Microsoft.Win32;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class MAConfigParametersViewModel : ViewModelBase<MAConfigParameters>
    {
        private List<Type> allowedTypes;

        private int originalVersion;


        public MAConfigParametersViewModel(MAConfigParameters model)
            : base(model)
        {
            this.Triggers = new MAExecutionTriggersViewModel(model.Triggers, this);

            this.SubscribeToErrors(this.Triggers);

            this.Commands.Add("AddTrigger", new DelegateCommand(t => this.AddTrigger(), u => this.CanAddTrigger()));
            this.Commands.Add("RemoveTrigger", new DelegateCommand(t => this.RemoveTrigger(), u => this.CanRemoveTrigger()));
            this.Commands.Add("Browse", new DelegateCommand(t => this.Browse()));
            this.Commands.Add("New", new DelegateCommand(t => this.New()));
            this.Commands.Add("Edit", new DelegateCommand(t => this.Edit(), u => this.CanEdit()));

            this.originalVersion = this.Model.Version;

            this.AddIsDirtyProperty(nameof(this.MAControllerPath));
            this.AddIsDirtyProperty(nameof(this.ScheduledImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.FullSyncRunProfileName));
            this.AddIsDirtyProperty(nameof(this.FullImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.ExportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.DeltaSyncRunProfileName));
            this.AddIsDirtyProperty(nameof(this.DeltaImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.ConfirmingImportRunProfileName));
            this.AddIsDirtyProperty(nameof(this.AutoImportIntervalMinutes));
            this.AddIsDirtyProperty(nameof(this.ScheduleImports));
            this.AddIsDirtyProperty(nameof(this.AutoImportScheduling));
            this.AddIsDirtyProperty(nameof(this.Triggers));
            this.AddIsDirtyProperty(nameof(this.LockManagementAgents));
            this.AddIsDirtyProperty(nameof(this.Disabled));
            this.AddIsDirtyProperty(nameof(this.IsDeleted));

            this.IsDirtySet += this.MAConfigParametersViewModel_IsDirtySet;

            this.Triggers.CollectionChanged += this.Triggers_CollectionChanged;

            foreach (MAExecutionTriggerViewModel item in this.Triggers)
            {
                item.IsDirtySet += this.MAConfigParametersViewModel_IsDirtySet;
            }

            this.DisplayIcon = App.GetImageResource("Settings.ico");

            this.PopulateMenuItems();
        }

        private void PopulateMenuItems()
        {
            if (this.IsMissing)
            {
                this.MenuItems = new ObservableCollection<MenuItemViewModelBase>();

                this.MenuItems.Add(new MenuItemViewModel()
                {
                    Header = "Remove missing management agent...",
                    Icon = App.GetImageResource("Cancel.ico"),
                    Command = new DelegateCommand(t => this.Remove(), t => this.CanRemove()),
                });
            }
        }

        private bool CanRemove()
        {
            return this.IsMissing;
        }

        private void Remove()
        {
            MessageBoxResult result = MessageBox.Show("This will permanently remove all the configuration for this management agent. Are you sure you want to continue?",
                "Confirm removal of configuration",
                MessageBoxButton.OKCancel, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                ((ManagementAgentsViewModel) this.Parent).Remove(this);
                this.IsDeleted = true;
            }
        }

        private void MAConfigParametersViewModel_IsDirtySet(object sender, PropertyChangedEventArgs e)
        {
            this.IncrementVersion();
        }

        private void Triggers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.IncrementVersion();
        }

        internal void Commit()
        {
            this.originalVersion = this.Version;
            this.IsDirty = false;
            foreach (MAExecutionTriggerViewModel item in this.Triggers)
            {
                item.IsDirty = false;
            }
        }

        private void IncrementVersion()
        {
            if (this.Model.Version > this.originalVersion)
            {
                return;
            }

            this.Model.Version++;
            Trace.WriteLine($"{this.ManagementAgentName} config version change from {this.originalVersion} to {this.Model.Version}");

            this.RaisePropertyChanged(nameof(this.Version));
            this.RaisePropertyChanged(nameof(this.IsNew));
            this.RaisePropertyChanged(nameof(this.DisplayName));
        }

        public bool IsDeleted { get; set; }

        [DependsOn(nameof(IsMissing), nameof(IsNew), nameof(Disabled), nameof(Version))]
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
                else if (this.Disabled)
                {
                    name += " (disabled)";
                }

                return name;
            }
        }

        public ObservableCollection<MenuItemViewModelBase> MenuItems { get; set; }

        public bool IsEnabled => !this.IsMissing && !this.Disabled;

        public string ManagementAgentName => this.Model.ManagementAgentName ?? "Unknown MA";

        public Guid ManagementAgentID => this.Model.ManagementAgentID;

        public string SortName => $"{this.Disabled}{this.ManagementAgentName}";

        public bool IsMissing => this.Model.IsMissing;

        public bool IsNew => this.Version == 0;

        public string MAControllerPath
        {
            get => this.Model.MAControllerPath;
            set => this.Model.MAControllerPath = value;

        }

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
                ConfigClient c = App.GetDefaultConfigClient();
                c.InvokeThenClose(u => items.AddRange(c.GetManagementAgentRunProfileNames(this.ManagementAgentID, includeMultiStep)));
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Unable to enumerate run profiles");
                Trace.WriteLine(ex.ToString());
            }

            return items;
        }

        public MAExecutionTriggersViewModel Triggers { get; private set; }

        public string LockManagementAgents
        {
            get => App.ToDelimitedString(this.Model.LockManagementAgents);
            set => this.Model.LockManagementAgents = App.FromDelimitedString(value);
        }

        private bool CanAddTrigger()
        {
            return !this.Model.IsMissing;
        }

        private void AddTrigger()
        {
            try
            {
                AddTriggerWindow window = new AddTriggerWindow { DataContext = this };
                this.GetAllowedTypesForMa();
                this.SelectedTrigger = this.AllowedTriggers?.FirstOrDefault();

                if (window.ShowDialog() == true)
                {
                    if (this.SelectedTrigger == null)
                    {
                        return;
                    }

                    ConfigClient c = App.GetDefaultConfigClient();
                    IMAExecutionTrigger instance = c.InvokeThenClose(t => t.CreateTriggerForManagementAgent(this.SelectedTrigger.FullName, this.ManagementAgentID));
                    this.Triggers.Add(instance, true);
                    this.Triggers.Find(instance).IsDirtySet += this.MAConfigParametersViewModel_IsDirtySet;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to create the trigger\n{ex.Message}", "Unable to create trigger");
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
                        this.GetAllowedTypesForMa();
                    }
                }

                return this.allowedTypes;
            }
        }

        public Type SelectedTrigger { get; set; }

        private void GetAllowedTypesForMa()
        {
            this.allowedTypes = new List<Type>();
            IList<string> allowedTypeNames = null;

            try
            {
                ConfigClient c = App.GetDefaultConfigClient();
                allowedTypeNames = c.InvokeThenClose(t => t.GetAllowedTriggerTypesForMA(this.ManagementAgentID));
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Unable to get allowed triggers");
                Trace.WriteLine(ex.ToString());
                return;
            }

            foreach (Type mytype in typeof(IMAExecutionTrigger).Assembly.GetTypes()
                .Where(mytype => mytype.GetInterfaces().Contains(typeof(IMAExecutionTrigger))))
            {
                if (allowedTypeNames.Contains(mytype.FullName))
                {
                    if (MAExecutionTrigger.SingleInstanceTriggers.Contains(mytype))
                    {
                        if (this.Triggers.Any(t => t.Model.GetType() == mytype))
                        {
                            continue;
                        }
                    }

                    this.allowedTypes.Add(mytype);
                }
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
                    selected.IsDirtySet -= this.MAConfigParametersViewModel_IsDirtySet;
                }
            }
        }

        private bool CanRemoveTrigger()
        {
            return this.Triggers.Any(t => t.IsSelected);
        }

        private void New()
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "ps1",
                OverwritePrompt = true,
                Filter = "PowerShell script|*.ps1"
            };

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
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(this.MAControllerPath) ?? Environment.CurrentDirectory;
                    openFileDialog.FileName = Path.GetFileName(this.MAControllerPath) ?? "*.ps1";
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
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