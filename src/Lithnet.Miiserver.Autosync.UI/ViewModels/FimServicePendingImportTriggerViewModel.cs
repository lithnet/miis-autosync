using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;
using Microsoft.Win32;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class FimServicePendingImportTriggerViewModel : ViewModelBase<FimServicePendingImportTrigger>
    {
        public FimServicePendingImportTriggerViewModel(FimServicePendingImportTrigger model)
            : base(model)
        {
        }

        public string RunProfileName
        {
            get => this.Model.HostName;
            set => this.Model.HostName = value;
        }

        public TimeSpan Interval
        {
            get => this.Model.Interval;
            set => this.Model.Interval = value;
        }
    }
}
