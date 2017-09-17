using System.Collections.Generic;
using System.Windows.Data;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class ManagementAgentsViewModel : ListViewModel<MAConfigParametersViewModel, MAConfigParameters>
    {
        public string DisplayName => "Management agent configuration";
        
        private ConfigFileViewModel config;

        public ManagementAgentsViewModel(MAConfigParametersCollection items, ConfigFileViewModel configFile)
            : base((IList<MAConfigParameters>)items, ManagementAgentsViewModel.ViewModelResolver)
        {
            this.config = configFile;
            this.DisplayIcon = App.GetImageResource("SettingsGroup.ico");
            this.AddIsDirtyProperty(nameof(this.Description));
            this.SortedItems = new CollectionViewSource();
            this.SortedItems.Source = this.ViewModels;
            this.SortedItems.SortDescriptions.Add(new System.ComponentModel.SortDescription("SortName", System.ComponentModel.ListSortDirection.Ascending));
        }

        private static MAConfigParametersViewModel ViewModelResolver(MAConfigParameters model)
        {
            return new MAConfigParametersViewModel(model);
        }

        public CollectionViewSource SortedItems { get; }

        public string Description
        {
            get => this.config.Description;
            set => this.config.Description = value;
        }
    }
}
