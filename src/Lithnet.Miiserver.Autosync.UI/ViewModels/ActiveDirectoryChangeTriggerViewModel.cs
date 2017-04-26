using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class ActiveDirectoryChangeTriggerViewModel
    {
        private ActiveDirectoryChangeTrigger model;

        public ActiveDirectoryChangeTriggerViewModel(ActiveDirectoryChangeTrigger model)
        {
            this.model = model;
            this.Commands = new CommandMap();
        }

        public string RunProfileName
        {
            get => this.model.HostName;
            set => this.model.HostName = value;
        }

        public TimeSpan MaximumTriggerInterval
        {
            get => this.model.MaximumTriggerInterval;
            set => this.model.MaximumTriggerInterval = value;
        }

        public TimeSpan LastLogonTimestampOffset
        {
            get => this.model.LastLogonTimestampOffset;
            set => this.model.LastLogonTimestampOffset = value;
        }

        public string BaseDN
        {
            get => this.model.BaseDN;
            set => this.model.BaseDN = value;
        }

        public NetworkCredential Credentials
        {
            get => this.model.Credentials;
            set => this.model.Credentials = value;
        }

        public string Username
        {
            get => this.model.Credentials?.UserName;
            set
            {
                if (this.model.Credentials == null)
                {
                    this.model.Credentials = new NetworkCredential();
                }

                this.model.Credentials.UserName = value;
            }
        }

        public string Password
        {
            get => this.model.Credentials?.Password;
            set
            {
                if (this.model.Credentials == null)
                {
                    this.model.Credentials = new NetworkCredential();
                }

                this.model.Credentials.Password = value;
            }
        }

        public bool Disabled
        {
            get => this.model.Disabled;
            set => this.model.Disabled = value;
        }

        public string HostName
        {
            get => this.model.HostName;
            set => this.model.HostName = value;
        }

        public string Name => this.model.Name;

        public List<string> ObjectClasses
        {
            get => this.model.ObjectClasses?.ToList();
            set => this.model.ObjectClasses = value?.ToArray();
        }

        public CommandMap Commands { get; private set; }
    }
}
