using System;
using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class MAExecutionTriggersViewModel : ListViewModel<MAExecutionTriggerViewModel, IMAExecutionTrigger>
    {
        private MAConfigParametersViewModel config;

        public MAExecutionTriggersViewModel(IList<IMAExecutionTrigger> items, MAConfigParametersViewModel config)
        {
            this.config = config;
            this.SetCollectionViewModel(items, this.ViewModelResolver);
        }

        private MAExecutionTriggerViewModel ViewModelResolver(IMAExecutionTrigger model)
        {
            if (model is ActiveDirectoryChangeTrigger)
            {
                return new ActiveDirectoryChangeTriggerViewModel((ActiveDirectoryChangeTrigger)model);
            }

            if (model is FimServicePendingImportTrigger)
            {
                return new FimServicePendingImportTriggerViewModel((FimServicePendingImportTrigger)model);
            }

            if (model is IntervalExecutionTrigger)
            {
                return new IntervalExecutionTriggerViewModel((IntervalExecutionTrigger)model, this.config);
            }

            if (model is PowerShellExecutionTrigger)
            {
                return new PowerShellExecutionTriggerViewModel((PowerShellExecutionTrigger)model);
            }

            if (model is ScheduledExecutionTrigger)
            {
                return new ScheduledExecutionTriggerViewModel((ScheduledExecutionTrigger)model, this.config);
            }

            throw new InvalidOperationException("Unknown model");
        }
    }
}
