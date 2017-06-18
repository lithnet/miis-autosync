using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "lithnet-autosync")]
    public class ConfigFile
    {
        [DataMember(Name = "management-agents")]
        public List<MAConfigParameters> ManagementAgents { get; set; }

        [DataMember(Name = "settings")]
        public Settings Settings { get; set; }

        public static ConfigFile Load(string file)
        {
            ConfigFile f = Serializer.Read<ConfigFile>(file);

            foreach (MAConfigParameters m in f.ManagementAgents)
            {
                m.ResolveManagementAgent();
            }

            return f;
        }
    }
}
