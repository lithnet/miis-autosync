using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using Lithnet.Miiserver.Client;
using Microsoft.Win32;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class MAConfigParametersViewModel : ViewModelBase<MAConfigParameters>, IEventCallBack
    {
        private List<Type> allowedTypes;

        private EventClient client;

        public MAConfigParametersViewModel(MAConfigParameters model)
            : base(model)
        {
            this.Triggers = new MAExecutionTriggersViewModel(model.Triggers, this);
            this.SubscribeToErrors(this.Triggers);

            this.IgnorePropertyHasChanged.Add(nameof(this.ExecutingRunProfile));
            this.IgnorePropertyHasChanged.Add(nameof(this.Message));
            this.IgnorePropertyHasChanged.Add(nameof(this.ExecutionQueue));
            this.IgnorePropertyHasChanged.Add(nameof(this.LastRunProfileResult));
            this.IgnorePropertyHasChanged.Add(nameof(this.LastRunProfileName));
            this.IgnorePropertyHasChanged.Add(nameof(this.LastRun));
            this.IgnorePropertyHasChanged.Add(nameof(this.State));

            this.Commands.Add("AddTrigger", new DelegateCommand(t => this.AddTrigger(), u => this.CanAddTrigger()));
            this.Commands.Add("RemoveTrigger", new DelegateCommand(t => this.RemoveTrigger(), u => this.CanRemoveTrigger()));
            this.Commands.Add("Browse", new DelegateCommand(t => this.Browse()));
            this.Commands.Add("New", new DelegateCommand(t => this.New()));
            this.Commands.Add("Edit", new DelegateCommand(t => this.Edit(), u => this.CanEdit()));

            this.Commands.Add("Start", new DelegateCommand(t => this.Start(), u => this.CanStart()));
            this.Commands.Add("Stop", new DelegateCommand(t => this.Stop(), u => this.CanStop()));
            this.Commands.Add("Pause", new DelegateCommand(t => this.Pause(), u => this.CanPause()));
            this.Commands.Add("Resume", new DelegateCommand(t => this.Resume(), u => this.CanResume()));

            this.SubscribeToStateChanges(model);
        }

        private void Stop()
        {
            try
            {
                ConfigClient c = new ConfigClient();
                c.Stop(this.ManagementAgentName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not stop the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanStop()
        {
            return this.State != ExecutorState.Stopped && this.State != ExecutorState.Stopping && this.State != ExecutorState.Disabled;
        }

        private void Start()
        {
            try
            {
                ConfigClient c = new ConfigClient();
                c.Start(this.ManagementAgentName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not start the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanStart()
        {
            return this.State == ExecutorState.Stopped;
        }

        private void Pause()
        {
            try
            {
                ConfigClient c = new ConfigClient();
                c.Pause(this.ManagementAgentName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not pause the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanPause()
        {
            return !MAStatus.IsControlState(this.State);
        }

        private void Resume()
        {
            try
            {
                ConfigClient c = new ConfigClient();
                c.Resume(this.ManagementAgentName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not resume the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanResume()
        {
            return this.State == ExecutorState.Paused;
        }

        private void SubscribeToStateChanges(MAConfigParameters model)
        {
            if (!model.IsMissing)
            {
                InstanceContext i = new InstanceContext(this);
                this.client = new EventClient(i);
                this.client.Register(this.ManagementAgentName);
                MAStatus status = this.client.GetFullUpdate(this.ManagementAgentName);

                if (status != null)
                {
                    this.MAStatusChanged(status);
                }
            }
        }

        public BitmapImage StatusIcon
        {
            get
            {
                switch (this.State)
                {
                    case ExecutorState.Disabled:
                        return App.GetImageResource("Stop.png");

                    case ExecutorState.Idle:
                        return App.GetImageResource("Clock1.png");

                    case ExecutorState.Paused:
                        return App.GetImageResource("Pause.png");

                    case ExecutorState.Processing:
                    case ExecutorState.Running:
                        return App.GetImageResource("Run.png");

                    case ExecutorState.Pausing:
                    case ExecutorState.Resuming:
                    case ExecutorState.Starting:
                    case ExecutorState.Waiting:
                    case ExecutorState.Stopping:
                        return App.GetImageResource("Hourglass.png");

                    case ExecutorState.Stopped:
                        return App.GetImageResource("Stop.png");

                    default:
                        return null;
                }
            }
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

        public string ManagementAgentName => this.Model.ManagementAgentName ?? "Unknown MA";

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
            get => this.Model.ScheduledImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.ScheduledImportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string FullSyncRunProfileName
        {
            get => this.Model.FullSyncRunProfileName ?? App.NullPlaceholder;
            set => this.Model.FullSyncRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string FullImportRunProfileName
        {
            get => this.Model.FullImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.FullImportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string ExportRunProfileName
        {
            get => this.Model.ExportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.ExportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string DeltaSyncRunProfileName
        {
            get => this.Model.DeltaSyncRunProfileName ?? App.NullPlaceholder;
            set => this.Model.DeltaSyncRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string DeltaImportRunProfileName
        {
            get => this.Model.DeltaImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.DeltaImportRunProfileName = value == App.NullPlaceholder ? null : value;
        }

        public string ConfirmingImportRunProfileName
        {
            get => this.Model.ConfirmingImportRunProfileName ?? App.NullPlaceholder;
            set => this.Model.ConfirmingImportRunProfileName = value == App.NullPlaceholder ? null : value;
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
                yield return "(none)";

                if (this.Model?.ManagementAgent?.RunProfiles == null)
                {
                    yield break;
                }

                foreach (var i in this.Model.ManagementAgent.RunProfiles)
                {
                    yield return i.Key;
                }
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
                this.SelectedTrigger = this.AllowedTriggers?.FirstOrDefault();

                if (window.ShowDialog() == true)
                {
                    if (this.SelectedTrigger == null)
                    {
                        return;
                    }

                    IMAExecutionTrigger instance = (IMAExecutionTrigger)Activator.CreateInstance(this.SelectedTrigger, this.Model.ManagementAgent);
                    this.Triggers.Add(instance, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred.\n{ex.Message}", "Unable to create trigger");
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

        public string ExecutionQueue { get; private set; }

        public string Message { get; private set; }

        public string ExecutingRunProfile { get; private set; }

        [AlsoNotifyFor(nameof(LastRun))]
        public string LastRunProfileResult { get; private set; }

        [AlsoNotifyFor(nameof(LastRun))]
        public string LastRunProfileName { get; private set; }

        public string LastRun => this.LastRunProfileName == null ? null : $"{this.LastRunProfileName}: {this.LastRunProfileResult}";

        public ExecutorState State { get; private set; }

        public void MAStatusChanged(MAStatus status)
        {
            this.Message = status.Message;
            this.ExecutingRunProfile = status.ExecutingRunProfile;
            this.ExecutionQueue = status.ExecutionQueue;
            this.LastRunProfileResult = status.LastRunProfileResult;
            this.LastRunProfileName = status.LastRunProfileName;
            this.State = status.State;

            Trace.WriteLine($"{status.MAName} : {status.LastRunProfileName} : {status.LastRunProfileResult} : {this.LastRun}");
        }
    }
}
