using System.Collections.Generic;
using System.Windows.Data;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class MAControllerConfigurationCollectionViewModel : ListViewModel<MAControllerConfigurationViewModel, MAControllerConfiguration>
    {
        public string DisplayName => "Management agent controllers";
        
        private ConfigFileViewModel config;

        public MAControllerConfigurationCollectionViewModel(MAControllerConfigurationCollection items, ConfigFileViewModel configFile)
            : base((IList<MAControllerConfiguration>)items, MAControllerConfigurationCollectionViewModel.ViewModelResolver)
        {
            this.config = configFile;
            this.DisplayIcon = App.GetImageResource("SettingsGroup.ico");
            this.AddIsDirtyProperty(nameof(this.Description));
            this.SortedItems = new CollectionViewSource();
            this.SortedItems.Source = this.ViewModels;
            this.SortedItems.SortDescriptions.Add(new System.ComponentModel.SortDescription("SortName", System.ComponentModel.ListSortDirection.Ascending));
        }

        private static MAControllerConfigurationViewModel ViewModelResolver(MAControllerConfiguration model)
        {
            return new MAControllerConfigurationViewModel(model);
        }

        public CollectionViewSource SortedItems { get; }

        public string Description
        {
            get => this.config.Description;
            set => this.config.Description = value;
        }
    }
}
