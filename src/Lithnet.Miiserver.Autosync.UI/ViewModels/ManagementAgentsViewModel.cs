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
    internal class ManagementAgentsViewModel : ListViewModel<MAConfigParametersViewModel, MAConfigParameters>
    {
        public ManagementAgentsViewModel(IList<MAConfigParameters> items)
            :base (items, ManagementAgentsViewModel.ViewModelResolver)
        {
            
        }

        private static MAConfigParametersViewModel ViewModelResolver(MAConfigParameters model)
        {
            return new MAConfigParametersViewModel(model);
        }
    }
}
