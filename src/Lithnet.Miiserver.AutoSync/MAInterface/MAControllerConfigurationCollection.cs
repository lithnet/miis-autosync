using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [CollectionDataContract(Name = "management-agents", ItemName = "management-agent")]
    public class MAControllerConfigurationCollection : KeyedCollection<string, MAControllerConfiguration>
    {
        public MAControllerConfigurationCollection()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public MAControllerConfiguration GetItemOrDefault(string name)
        {
            if (this.Contains(name))
            {
                return this[name];
            }
            else
            {
                return null;
            }
        }

        public MAControllerConfiguration GetItemOrDefault(Guid maid)
        {
            return this.FirstOrDefault(t => t.ManagementAgentID == maid);
        }

        protected override string GetKeyForItem(MAControllerConfiguration item)
        {
            return item.ManagementAgentName;
        }
    }
}
