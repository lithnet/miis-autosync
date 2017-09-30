using System.Linq;

using System.Collections.Generic;
using System.Windows.Data;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class PartitionConfigurationCollectionViewModel : ListViewModel<PartitionConfigurationViewModel, PartitionConfiguration>
    {
        private MAControllerConfigurationViewModel config;

        public PartitionConfigurationCollectionViewModel(PartitionConfigurationCollection items, MAControllerConfigurationViewModel config)
            : base((IList<PartitionConfiguration>)items, t => PartitionConfigurationCollectionViewModel.ViewModelResolver(t, config))
        {
            this.config = config;

            if (this.ViewModels.Count > 0)
            {
                this.ViewModels[0].IsSelected = true;
            }
        }

        private static PartitionConfigurationViewModel ViewModelResolver(PartitionConfiguration model, MAControllerConfigurationViewModel config)
        {
            return new PartitionConfigurationViewModel(model, config);
        }
    }
}
