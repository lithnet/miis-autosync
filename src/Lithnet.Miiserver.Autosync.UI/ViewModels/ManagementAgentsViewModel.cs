using System.Collections.Generic;
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
