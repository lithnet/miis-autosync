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
    public abstract class MAExecutionTriggerViewModel : ViewModelBase<IMAExecutionTrigger>
    {
        protected MAExecutionTriggerViewModel(IMAExecutionTrigger model)
            : base(model)
        {
        }
    }
}
