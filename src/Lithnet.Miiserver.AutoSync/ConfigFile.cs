using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "lithnet-autosync")]
    public class ConfigFile
    {
        [DataMember(Name = "management-agents")]
        public List<MAConfigParameters> ManagementAgents { get; set; }
    }
}
