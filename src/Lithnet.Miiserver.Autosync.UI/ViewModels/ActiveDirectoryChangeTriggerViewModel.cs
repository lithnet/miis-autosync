using System;
using System.Collections.ObjectModel;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class ActiveDirectoryChangeTriggerViewModel : MAExecutionTriggerViewModel
    {
        private ActiveDirectoryChangeTrigger typedModel;

        private const string PlaceholderPassword = "{5A4A203E-EBB9-4D47-A3D4-CD6055C6B4FF}";

        public ActiveDirectoryChangeTriggerViewModel(ActiveDirectoryChangeTrigger model)
            :base (model)
        {
            this.typedModel = model;
            this.ObjectClasses = new ObservableCollection<string>(this.typedModel.ObjectClasses);
        }

        public TimeSpan MinimumIntervalBetweenEvents
        {
            get => this.typedModel.MinimumIntervalBetweenEvents;
            set => this.typedModel.MinimumIntervalBetweenEvents = value;
        }

        public TimeSpan LastLogonTimestampOffset
        {
            get => this.typedModel.LastLogonTimestampOffset;
            set => this.typedModel.LastLogonTimestampOffset = value;
        }

        public string BaseDN
        {
            get => this.typedModel.BaseDN;
            set => this.typedModel.BaseDN = value;
        }

        public bool UseServiceAccountCredentials
        {
            get => !this.UseExplicitCredentials;
            set => this.UseExplicitCredentials = !value;
        }

        public bool UseExplicitCredentials
        {
            get => this.typedModel.UseExplicitCredentials;
            set => this.typedModel.UseExplicitCredentials = value;
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

        public bool Disabled
        {
            get => this.typedModel.Disabled;
            set => this.typedModel.Disabled = value;
        }

        [AlsoNotifyFor("Description")]
        public string HostName
        {
            get => this.typedModel.HostName;
            set => this.typedModel.HostName = value;
        }

        public string Name => this.Model.DisplayName;

        public ObservableCollection<string> ObjectClasses { get; set; }
    }
}
