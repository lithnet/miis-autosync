using System;
using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class IntervalExecutionTriggerViewModel : MAExecutionTriggerViewModel
    {
        private IntervalExecutionTrigger typedModel;

        private MAConfigParametersViewModel config;

        public IntervalExecutionTriggerViewModel(IntervalExecutionTrigger model, MAConfigParametersViewModel config)
            :base(model)
        {
            this.typedModel = model;
            this.config = config;
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
        public TimeSpan Interval
        {
            get => this.typedModel.Interval;
            set => this.typedModel.Interval = value;
        }

        public IEnumerable<string> RunProfileNames => this.config.RunProfileNames;
    }
}
