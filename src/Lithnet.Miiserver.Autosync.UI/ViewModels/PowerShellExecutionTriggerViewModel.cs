using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Lithnet.Common.Presentation;
using Microsoft.Win32;
using PropertyChanged;
using NLog;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class PowerShellExecutionTriggerViewModel : MAExecutionTriggerViewModel
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PowerShellExecutionTrigger typedModel;

        private const string PlaceholderPassword = "{B0C5980F-11D1-4CED-8BA0-6D09FF248C6F}";

        public PowerShellExecutionTriggerViewModel(PowerShellExecutionTrigger model)
            : base(model)
        {
            this.typedModel = model;
            this.Commands.Add("Browse", new DelegateCommand(t => this.Browse(), t => this.CanEditFiles));
            this.Commands.Add("New", new DelegateCommand(t => this.New(), t => this.CanEditFiles));
            this.Commands.Add("Edit", new DelegateCommand(t => this.Edit(), u => this.CanEditFiles && this.CanEdit()));
            this.AddIsDirtyProperty(nameof(this.ScriptPath));
            this.AddIsDirtyProperty(nameof(this.Interval));
            this.AddIsDirtyProperty(nameof(this.ExceptionBehaviour));
            this.AddIsDirtyProperty(nameof(this.Username));
            this.AddIsDirtyProperty(nameof(this.Password));
            this.AddIsDirtyProperty(nameof(this.Disabled));
        }

        [AlsoNotifyFor("Description")]
        public bool Disabled
        {
            get => this.typedModel.Disabled;
            set => this.typedModel.Disabled = value;
        }

        [AlsoNotifyFor("Description")]
        public string ScriptPath
        {
            get => this.typedModel.ScriptPath;
            set => this.typedModel.ScriptPath = value;
        }

        public TimeSpan Interval
        {
            get => this.typedModel.Interval;
            set => this.typedModel.Interval = value;
        }

        public ExecutionErrorBehaviour ExceptionBehaviour
        {
            get => this.typedModel.ExceptionBehaviour;
            set => this.typedModel.ExceptionBehaviour = value;
        }

        public string Username
        {
            get => this.typedModel.Username;
            set => this.typedModel.Username = value;
        }

        public string Password
        {
            get
            {
                if (this.typedModel.Password == null || !this.typedModel.Password.HasValue)
                {
                    return null;
                }
                else
                {
                    return PlaceholderPassword;
                }
            }
            set
            {
                if (value == null)
                {
                    this.typedModel.Password = null;
                }

                if (value == PlaceholderPassword)
                {
                    return;
                }

                this.typedModel.Password = new ProtectedString(value);
            }
        }

        public string Type => this.Model.Type;

        public string Description => this.Model.Description;

        public bool CanEditFiles => ConnectionManager.ConnectedToLocalHost;

        public bool ShowRemoteEditWarning => !ConnectionManager.ConnectedToLocalHost;

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
                File.WriteAllText(dialog.FileName, Properties.Resources.PowerShellTriggerScript);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save the file\n{ex.Message}", "Unable to save");
                return;
            }

            this.ScriptPath = dialog.FileName;

            this.Edit();
        }

        private bool CanEdit()
        {
            return File.Exists(this.ScriptPath);
        }

        private void Edit()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(this.ScriptPath) { Verb = "Edit" };
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

            if (File.Exists(this.ScriptPath))
            {
                try
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(this.ScriptPath);
                    openFileDialog.FileName = Path.GetFileName(this.ScriptPath);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "error parsing file path");
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
                this.ScriptPath = openFileDialog.FileName;
            }
        }

        protected override void ValidatePropertyChange(string propertyName)
        {
            if (propertyName == nameof(this.ScriptPath))
            {
                if (System.IO.File.Exists(this.ScriptPath))
                {
                    this.RemoveError(nameof(this.ScriptPath));
                }
                else
                {
                    this.AddError(nameof(this.ScriptPath), "The specified file was not found");
                }
            }

            base.ValidatePropertyChange(propertyName);
        }
    }
}
