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
    public class IntervalExecutionTriggerViewModel : ViewModelBase<IntervalExecutionTrigger>
    {
        public IntervalExecutionTriggerViewModel(IntervalExecutionTrigger model)
            :base(model)
        {
        }

        public string RunProfileName
        {
            get => this.Model.RunProfileName;
            set => this.Model.RunProfileName = value;
        }

        public MARunProfileType RunProfileTargetType
        {
            get => this.Model.RunProfileTargetType;
            set => this.Model.RunProfileTargetType = value;
        }

        public TimeSpan Interval
        {
            get => this.Model.Interval;
            set => this.Model.Interval = value;
        }
    }
}
