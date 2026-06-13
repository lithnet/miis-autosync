using System;
using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ScheduledExecutionTriggerViewModel : MAExecutionTriggerViewModel
    {
        private ScheduledExecutionTrigger typedModel;

        private MAControllerConfigurationViewModel config;

        public ScheduledExecutionTriggerViewModel(ScheduledExecutionTrigger model, MAControllerConfigurationViewModel config)
            : base(model)
        {
            this.config = config;
            this.typedModel = model;
            this.AddIsDirtyProperty(nameof(this.RunProfileName));
            this.AddIsDirtyProperty(nameof(this.Interval));
            this.AddIsDirtyProperty(nameof(this.StartDateTime));
            this.AddIsDirtyProperty(nameof(this.Exclusive));
            this.AddIsDirtyProperty(nameof(this.RunImmediate));
            this.AddIsDirtyProperty(nameof(this.Disabled));
        }

        public string Type => this.Model.Type;

        public string Description => this.Model.Description;

        [AlsoNotifyFor("Description")]
        public bool Disabled
        {
            get => this.typedModel.Disabled;
            set => this.typedModel.Disabled = value;
        }

        public bool Exclusive
        {
            get => this.typedModel.Exclusive;
            set => this.typedModel.Exclusive = value;
        }

        public bool RunImmediate
        {
            get => this.typedModel.RunImmediate;
            set => this.typedModel.RunImmediate = value;
        }

        [AlsoNotifyFor("Description")]
        public string RunProfileName
        {
            get => this.typedModel.RunProfileName ?? App.NullPlaceholder;
            set => this.typedModel.RunProfileName = value == App.NullPlaceholder ? null : value;
        }

        [AlsoNotifyFor("Description", nameof(StartDate), nameof(StartHour), nameof(StartMinute))]
        public DateTime StartDateTime
        {
            get => this.typedModel.StartDateTime;
            set => this.typedModel.StartDateTime = value;
        }

        // The view edits the start date with a date picker and the time of day with separate
        // hour and minute inputs; all three read and write the single StartDateTime value.
        public DateTime? StartDate
        {
            get => this.StartDateTime.Date;
            set
            {
                if (value.HasValue)
                {
                    this.StartDateTime = value.Value.Date + this.StartDateTime.TimeOfDay;
                }
            }
        }

        public int StartHour
        {
            get => this.StartDateTime.Hour;
            set => this.StartDateTime = this.StartDateTime.Date + new TimeSpan(value, this.StartDateTime.Minute, 0);
        }

        public int StartMinute
        {
            get => this.StartDateTime.Minute;
            set => this.StartDateTime = this.StartDateTime.Date + new TimeSpan(this.StartDateTime.Hour, value, 0);
        }

        [AlsoNotifyFor("Description")]
        public TimeSpan Interval
        {
            get => this.typedModel.Interval;
            set => this.typedModel.Interval = value;
        }

        public IEnumerable<string> RunProfileNames => this.config.RunProfileNames;

        protected override void ValidatePropertyChange(string propertyName)
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

            base.ValidatePropertyChange(propertyName);
        }
    }
}
