using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class ActiveDirectoryChangeTriggerViewModel : ViewModelBase<ActiveDirectoryChangeTrigger>
    {
        public ActiveDirectoryChangeTriggerViewModel(ActiveDirectoryChangeTrigger model)
            :base (model)
        {
        }

        public string RunProfileName
        {
            get => this.Model.HostName;
            set => this.Model.HostName = value;
        }

        public TimeSpan MaximumTriggerInterval
        {
            get => this.Model.MinimumIntervalBetweenEvents;
            set => this.Model.MinimumIntervalBetweenEvents = value;
        }

        public TimeSpan LastLogonTimestampOffset
        {
            get => this.Model.LastLogonTimestampOffset;
            set => this.Model.LastLogonTimestampOffset = value;
        }

        public string BaseDN
        {
            get => this.Model.BaseDN;
            set => this.Model.BaseDN = value;
        }

        //public NetworkCredential Credentials
        //{
        //    get => this.model.Credentials;
        //    set => this.model.Credentials = value;
        //}

        //public string Username
        //{
        //    get => this.model.Credentials?.UserName;
        //    set
        //    {
        //        if (this.model.Credentials == null)
        //        {
        //            this.model.Credentials = new NetworkCredential();
        //        }

        //        this.model.Credentials.UserName = value;
        //    }
        //}

        //public string Password
        //{
        //    get => this.model.Credentials?.Password;
        //    set
        //    {
        //        if (this.model.Credentials == null)
        //        {
        //            this.model.Credentials = new NetworkCredential();
        //        }

        //        this.model.Credentials.Password = value;
        //    }
        //}

        public bool Disabled
        {
            get => this.Model.Disabled;
            set => this.Model.Disabled = value;
        }

        public string HostName
        {
            get => this.Model.HostName;
            set => this.Model.HostName = value;
        }

        public string Name => this.Model.Name;

        public List<string> ObjectClasses
        {
            get => this.Model.ObjectClasses?.ToList();
            set => this.Model.ObjectClasses = value?.ToArray();
        }
    }
}
