using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Forms;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class SettingsViewModel : ViewModelBase<Settings>
    {
        private const string PlaceholderPassword = "{5A4A203E-EBB9-4D47-A3D4-CD6055C6B4FF}";

        private static char[] separators = new char[] {',', ';'};

        public SettingsViewModel(Settings model)
            : base(model)
        {
            this.Commands.Add("SelectPath", new DelegateCommand(t => this.SelectPath(), u => this.CanSelectPath()));

            if (this.RunHistoryAge.TotalSeconds <= 0)
            {
                this.RunHistoryAge = new TimeSpan(1, 0, 0, 0);
            }

            if (this.Model.MailIgnoreReturnCodes == null)
            {
                this.Model.MailIgnoreReturnCodes = new HashSet<string>() {"success", "completed-no-objects"};
            }

            this.AddIsDirtyProperty(nameof(this.RunHistoryClear));
            this.AddIsDirtyProperty(nameof(this.RunHistoryAge));
            this.AddIsDirtyProperty(nameof(this.RunHistoryPath));
            this.AddIsDirtyProperty(nameof(this.RunHistorySave));
            this.AddIsDirtyProperty(nameof(this.MailEnabled));
            this.AddIsDirtyProperty(nameof(this.MailUseAppConfig));
            this.AddIsDirtyProperty(nameof(this.MailHost));
            this.AddIsDirtyProperty(nameof(this.MailPort));
            this.AddIsDirtyProperty(nameof(this.MailUseSsl));
            this.AddIsDirtyProperty(nameof(this.MailUseDefaultCredentials));
            this.AddIsDirtyProperty(nameof(this.MailUsername));
            this.AddIsDirtyProperty(nameof(this.MailPassword));
            this.AddIsDirtyProperty(nameof(this.MailTo));
            this.AddIsDirtyProperty(nameof(this.MailFrom));
            this.AddIsDirtyProperty(nameof(this.MailMaxErrors));
            this.AddIsDirtyProperty(nameof(this.MailSendAllErrorInstances));
            this.AddIsDirtyProperty(nameof(this.MailIgnoreReturnCodes));
            this.AddIsDirtyProperty(nameof(this.RunMode));

            this.DisplayIcon = App.GetImageResource("SettingsFile.ico");

        }

        public string DisplayName => "Settings";

        public bool RunHistoryClear
        {
            get => this.Model.RunHistoryClear;
            set => this.Model.RunHistoryClear = value;
        }

        public TimeSpan RunHistoryAge
        {
            get => this.Model.RunHistoryAge;
            set => this.Model.RunHistoryAge = value;
        }

        public string RunHistoryPath
        {
            get => this.Model.RunHistoryPath;
            set => this.Model.RunHistoryPath = value;
        }

        public bool RunHistorySave
        {
            get => this.Model.RunHistorySave;
            set => this.Model.RunHistorySave = value;
        }

        public bool MailEnabled
        {
            get => this.Model.MailEnabled;
            set => this.Model.MailEnabled = value;
        }

        public bool MailUseAppConfig
        {
            get => this.Model.MailUseAppConfig;
            set => this.Model.MailUseAppConfig = value;
        }

        public string MailHost
        {
            get => this.Model.MailHost;
            set => this.Model.MailHost = value;
        }

        public int MailPort
        {
            get => this.Model.MailPort;
            set => this.Model.MailPort = value <= 0 ? 25 : value;
        }

        public bool MailUseSsl
        {
            get => this.Model.MailUseSsl;
            set => this.Model.MailUseSsl = value;
        }

        public bool MailUseDefaultCredentials
        {
            get => this.Model.MailUseDefaultCredentials;
            set => this.Model.MailUseDefaultCredentials = value;
        }

        public bool MailCredentialFieldsEnabled => !this.MailUseDefaultCredentials;

        public string MailUsername
        {
            get => this.Model.MailUsername;
            set => this.Model.MailUsername = value;
        }

        public string MailPassword
        {
            get
            {
                if (this.Model.MailPassword == null || !this.Model.MailPassword.HasValue)
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
                    this.Model.MailPassword = null;
                }

                if (value == PlaceholderPassword)
                {
                    return;
                }

                this.Model.MailPassword = new ProtectedString(value);
            }
        }

        public string MailTo
        {
            get
            {
                if (this.Model.MailTo?.Count > 0)
                {
                    return string.Join(";", this.Model.MailTo);
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.Model.MailTo = new HashSet<string>();
                }
                else
                {
                    this.Model.MailTo = new HashSet<string>(value.Split(separators, StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        public string MailFrom
        {
            get => this.Model.MailFrom;
            set => this.Model.MailFrom = value;
        }

        public int MailMaxErrors
        {
            get => this.Model.MailMaxErrors;
            set => this.Model.MailMaxErrors = value;
        }

        public bool MailSendAllErrorInstances
        {
            get => this.Model.MailSendAllErrorInstances;
            set => this.Model.MailSendAllErrorInstances = value;
        }

        public bool MailSendOnlyNewErrors
        {
            get => !this.MailSendAllErrorInstances;
            set => this.MailSendAllErrorInstances = !value;
        }

        public string MailIgnoreReturnCodes
        {
            get
            {
                if (this.Model.MailIgnoreReturnCodes?.Count > 0)
                {
                    return string.Join(";", this.Model.MailIgnoreReturnCodes);
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.Model.MailIgnoreReturnCodes = new HashSet<string>();
                }
                else
                {
                    this.Model.MailIgnoreReturnCodes = new HashSet<string>(value.Split(separators, StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        [AlsoNotifyFor(nameof(IsRunModeSupported), nameof(IsRunModeExclusive), nameof(IsRunModeUnsupported))]
        public RunMode RunMode
        {
            get => this.Model.RunMode;
            set => this.Model.RunMode = value;
        }

        public bool IsRunModeSupported
        {
            get => this.RunMode == RunMode.Supported;
            set
            {
                if (value)
                {
                    this.RunMode = RunMode.Supported;

                }
            }
        }

        public bool IsRunModeExclusive
        {
            get => this.RunMode == RunMode.Exclusive;
            set
            {
                if (value)
                {
                    this.RunMode = RunMode.Exclusive;

                }
            }
        }

        public bool IsRunModeUnsupported
        {
            get => this.RunMode == RunMode.Unsupported;
            set
            {
                if (value)
                {
                    this.RunMode = RunMode.Unsupported;

                }
            }
        }



        private void SelectPath()
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            dialog.SelectedPath = this.RunHistoryPath;


            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            this.RunHistoryPath = dialog.SelectedPath;
        }

        private bool CanSelectPath()
        {
            return this.RunHistorySave && this.RunHistoryClear;
        }
    }
}
