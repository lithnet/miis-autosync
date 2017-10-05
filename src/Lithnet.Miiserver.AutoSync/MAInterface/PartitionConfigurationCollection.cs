using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [CollectionDataContract(Name = "partitions", ItemName = "partition")]
    public class PartitionConfigurationCollection : KeyedCollection<Guid, PartitionConfiguration>
    {
        public PartitionConfigurationCollection()
            : base()
        {
        }

        public PartitionConfiguration GetActiveItemOrNull(string partitionName)
        {
            PartitionConfiguration c = this.GetItemOrNull(partitionName);

            if (c == null || !c.IsActive)
            {
                return null;
            }

            return c;
        }

        public PartitionConfiguration GetActiveItemOrNull(Guid partitionID)
        {
            PartitionConfiguration c = this.GetItemOrNull(partitionID);

            if (c == null || !c.IsActive)
            {
                return null;
            }

            return c;
        }

        public PartitionConfiguration GetItemOrNull(Guid partitionID)
        {
            if (this.Contains(partitionID))
            {
                return this[partitionID];
            }
            else
            {
                return null;
            }
        }

        public PartitionConfiguration GetItemOrNull(string name)
        {
            return this.FirstOrDefault(t => t.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        public PartitionConfiguration GetDefaultOrFirstActivePartition()
        {
            return this.GetItemOrNull("default") ?? this.ActiveConfigurations.FirstOrDefault();
        }

        protected override Guid GetKeyForItem(PartitionConfiguration item)
        {
            return item.ID;
        }

        public IEnumerable<PartitionConfiguration> ActiveConfigurations
        {
            get
            {
                return this.Items.Where(t => t.IsActive);
            }
        }
    }
}
