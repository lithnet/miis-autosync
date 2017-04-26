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
    public class IntervalExecutionTriggerViewModel
    {
        private IntervalExecutionTrigger model;

        public IntervalExecutionTriggerViewModel(IntervalExecutionTrigger model)
        {
            this.model = model;
            this.Commands = new CommandMap();
        }

        public string RunProfileName
        {
            get => this.model.RunProfileName;
            set => this.model.RunProfileName = value;
        }

        public MARunProfileType RunProfileTargetType
        {
            get => this.model.RunProfileTargetType;
            set => this.model.RunProfileTargetType = value;
        }

        public TimeSpan Interval
        {
            get => this.model.Interval;
            set => this.model.Interval = value;
        }

        public CommandMap Commands { get; private set; }
    }
}
