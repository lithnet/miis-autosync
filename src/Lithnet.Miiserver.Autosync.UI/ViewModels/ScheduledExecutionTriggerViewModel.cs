using System;
using System.Collections.Generic;
using System.Globalization;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class ScheduledExecutionTriggerViewModel : MAExecutionTriggerViewModel
    {
        private ScheduledExecutionTrigger typedModel;

        private MAConfigParametersViewModel config;

        public ScheduledExecutionTriggerViewModel(ScheduledExecutionTrigger model, MAConfigParametersViewModel config)
            : base(model)
        {
            this.config = config;
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
        public DateTime StartDateTime
        {
            get => this.typedModel.StartDateTime;
            set => this.typedModel.StartDateTime = value;
        }

        [AlsoNotifyFor("Description")]
        public TimeSpan Interval
        {
            get => this.typedModel.Interval;
            set => this.typedModel.Interval = value;
        }

        public CultureInfo Culture => CultureInfo.CurrentCulture;

        public IEnumerable<string> RunProfileNames => this.config.RunProfileNames;
    }
}
