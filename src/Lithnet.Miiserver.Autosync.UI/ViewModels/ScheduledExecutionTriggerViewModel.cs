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
    public class ScheduledExecutionTriggerViewModel
    {
        private ScheduledExecutionTrigger model;

        public ScheduledExecutionTriggerViewModel(ScheduledExecutionTrigger model)
        {
            this.model = model;
            this.Commands = new CommandMap();
        }

        public string RunProfileName
        {
            get
            {
                return this.model.RunProfileName;
            }
            set
            {
                this.model.RunProfileName = value;
            }
        }

        public DateTime StartDateTime
        {
            get
            {
                return this.model.StartDateTime;
            }
            set
            {
                this.model.StartDateTime = value;
            }
        }

        public TimeSpan Interval
        {
            get
            {
                return this.model.Interval;
            }
            set
            {
                this.model.Interval = value;
            }
        }

        public CommandMap Commands { get; private set; }
    }
}
