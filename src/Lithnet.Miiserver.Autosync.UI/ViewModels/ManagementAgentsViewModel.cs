using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class ManagementAgentsViewModel : ListViewModel<MAConfigParametersViewModel, MAConfigParameters>
    {
        public string DisplayName => "Management agent configuration";

        private MAConfigParametersCollection model;

        public ManagementAgentsViewModel(MAConfigParametersCollection items)
            : base((IList<MAConfigParameters>)items, ManagementAgentsViewModel.ViewModelResolver)
        {
            this.model = items;
            this.DisplayIcon = App.GetImageResource("SettingsGroup.ico");

        }

        private static MAConfigParametersViewModel ViewModelResolver(MAConfigParameters model)
        {
            return new MAConfigParametersViewModel(model);
        }

        public string Description
        {
            get => this.model.Description;
            set => this.model.Description = value;
        }
    }
}
