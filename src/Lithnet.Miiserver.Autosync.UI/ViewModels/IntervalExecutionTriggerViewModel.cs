using System;
using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class IntervalExecutionTriggerViewModel : MAExecutionTriggerViewModel
    {
        private IntervalExecutionTrigger typedModel;

        private MAControllerConfigurationViewModel config;

        public IntervalExecutionTriggerViewModel(IntervalExecutionTrigger model, MAControllerConfigurationViewModel config)
            : base(model)
        {
            this.typedModel = model;
            this.config = config;
            this.AddIsDirtyProperty(nameof(this.RunProfileName));
            this.AddIsDirtyProperty(nameof(this.Interval));
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
                    this.AddError(nameof(this.RunProfileName), "An interval execution trigger must have a run profile specified");
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
