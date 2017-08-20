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

        private ConfigFileViewModel config;

        public ManagementAgentsViewModel(MAConfigParametersCollection items, ConfigFileViewModel configFile)
            : base((IList<MAConfigParameters>)items, ManagementAgentsViewModel.ViewModelResolver)
        {
            this.model = items;
            this.config = configFile;
            this.DisplayIcon = App.GetImageResource("SettingsGroup.ico");
            this.AddIsDirtyProperty(nameof(this.Description));
        }

        private static MAConfigParametersViewModel ViewModelResolver(MAConfigParameters model)
        {
            return new MAConfigParametersViewModel(model);
        }

        public string Description
        {
            get => this.config.Description;
            set => this.config.Description = value;
        }
    }
}
