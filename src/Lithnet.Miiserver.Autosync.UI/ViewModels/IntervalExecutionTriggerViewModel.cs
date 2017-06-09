using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;
using Microsoft.Win32;
using PropertyChanged;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class IntervalExecutionTriggerViewModel : MAExecutionTriggerViewModel
    {
        private IntervalExecutionTrigger typedModel;

        public IntervalExecutionTriggerViewModel(IntervalExecutionTrigger model)
            :base(model)
        {
            this.typedModel = model;
        }
        
        public string Type => this.Model.Type;

        public string Description => this.Model.Description;

        [AlsoNotifyFor("Description")]
        public string RunProfileName
        {
            get => this.typedModel.RunProfileName;
            set => this.typedModel.RunProfileName = value;
        }

        [AlsoNotifyFor("Description")]
        public MARunProfileType RunProfileTargetType
        {
            get => this.typedModel.RunProfileTargetType;
            set => this.typedModel.RunProfileTargetType = value;
        }

        [AlsoNotifyFor("Description")]
        public TimeSpan Interval
        {
            get => this.typedModel.Interval;
            set => this.typedModel.Interval = value;
        }
    }
}
