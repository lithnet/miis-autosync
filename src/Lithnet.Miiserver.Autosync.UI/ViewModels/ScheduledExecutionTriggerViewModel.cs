using System;
using System.Collections.Generic;
using System.Globalization;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
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
            this.AddIsDirtyProperty(nameof(this.RunProfileName));
            this.AddIsDirtyProperty(nameof(this.Interval));
            this.AddIsDirtyProperty(nameof(this.StartDateTime));
            this.AddIsDirtyProperty(nameof(this.Exclusive));
        }

        public string Type => this.Model.Type;

        public string Description => this.Model.Description;

        public bool Exclusive
        {
            get => this.typedModel.Exclusive;
            set => this.typedModel.Exclusive = value;
        }

        [AlsoNotifyFor("Description")]
        public string RunProfileName
        {
            get => this.typedModel.RunProfileName ?? App.NullPlaceholder;
            set => this.typedModel.RunProfileName = value == App.NullPlaceholder ? null : value;
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

        protected override void ValidatePropertyChange(string propertyName)
        {
            if (propertyName == nameof(this.RunProfileName))
            {
                if (propertyName == nameof(this.RunProfileName))
                {
                    if (string.IsNullOrEmpty(this.RunProfileName) || this.RunProfileName == App.NullPlaceholder)
                    {
                        this.AddError(nameof(this.RunProfileName), "A scheduled execution trigger must have a run profile specified");
                    }
                    else
                    {
                        this.RemoveError(nameof(this.RunProfileName));
                    }
                }
            }

            base.ValidatePropertyChange(propertyName);
        }
    }
}
