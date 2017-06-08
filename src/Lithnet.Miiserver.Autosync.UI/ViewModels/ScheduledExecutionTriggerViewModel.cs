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
    public class ScheduledExecutionTriggerViewModel : ViewModelBase<ScheduledExecutionTrigger>
    {
        public ScheduledExecutionTriggerViewModel(ScheduledExecutionTrigger model)
            : base(model)
        {
        }

        public string RunProfileName
        {
            get => this.Model.RunProfileName;
            set => this.Model.RunProfileName = value;
        }

        public DateTime StartDateTime
        {
            get => this.Model.StartDateTime;
            set => this.Model.StartDateTime = value;
        }

        public TimeSpan Interval
        {
            get => this.Model.Interval;
            set => this.Model.Interval = value;
        }
    }
}
