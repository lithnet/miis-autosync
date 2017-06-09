using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class MAExecutionTriggersViewModel : ListViewModel<MAExecutionTriggerViewModel, IMAExecutionTrigger> 
    {
        public MAExecutionTriggersViewModel(IList<IMAExecutionTrigger> items)
            : base(items, MAExecutionTriggersViewModel.ViewModelResolver)
        {
        }

        private static MAExecutionTriggerViewModel ViewModelResolver(IMAExecutionTrigger model)
        {
            if (model is ActiveDirectoryChangeTrigger)
            {
                return new ActiveDirectoryChangeTriggerViewModel((ActiveDirectoryChangeTrigger)model);
            }

            if (model is FimServicePendingImportTrigger)
            {
                return new FimServicePendingImportTriggerViewModel((FimServicePendingImportTrigger) model);
            }

            if (model is IntervalExecutionTrigger)
            {
                return new IntervalExecutionTriggerViewModel((IntervalExecutionTrigger)model);
            }

            if (model is PowerShellExecutionTrigger)
            {
                return new PowerShellExecutionTriggerViewModel((PowerShellExecutionTrigger)model);
            }

            if (model is ScheduledExecutionTrigger)
            {
                return new ScheduledExecutionTriggerViewModel((ScheduledExecutionTrigger) model);
            }

            throw new InvalidOperationException("Unknown model");
        }
    }
}
