using System;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class FimServicePendingImportTriggerViewModel : MAExecutionTriggerViewModel
    {
        private FimServicePendingImportTrigger typedModel;
        
        public FimServicePendingImportTriggerViewModel(FimServicePendingImportTrigger model)
            : base(model)
        {
            this.typedModel = model;
        }
      
        public string Type => this.Model.Type;

        public string Description => this.Model.Description;
     
        [AlsoNotifyFor("Description")]
        public string HostName
        {
            get => this.typedModel.HostName;
            set => this.typedModel.HostName = value;
        }

        public TimeSpan Interval
        {
            get => this.typedModel.Interval;
            set => this.typedModel.Interval = value;
        }
    }
}
