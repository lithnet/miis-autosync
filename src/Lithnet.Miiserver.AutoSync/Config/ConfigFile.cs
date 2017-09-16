using System.Collections.Generic;
using System.Runtime.Serialization;
using Lithnet.Miiserver.Client;
using System.Linq;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "lithnet-autosync")]
    public class ConfigFile
    {
        [DataMember(Name = "management-agents")]
        public MAConfigParametersCollection ManagementAgents { get; set; }

        [DataMember(Name = "settings")]
        public Settings Settings { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "version")]
        public int Version { get; set; }

        public ConfigFile()
        {
            this.ManagementAgents = new MAConfigParametersCollection();
            this.Settings = new Settings();
        }

        internal void ValidateManagementAgents()
        {
            foreach (MAConfigParameters config in this.ManagementAgents)
            {
                config.ResolveManagementAgent();
            }

            this.AddMissingManagementAgents();
        }
        internal static ConfigFile Load(string file)
        {
            ConfigFile f = Serializer.Read<ConfigFile>(file);
            f.ValidateManagementAgents();

            return f;
        }

        public static void Save(ConfigFile config, string filename)
        {
            Serializer.Save(filename, config);
        }

        

        private void AddMissingManagementAgents()
        {
            foreach (ManagementAgent ma in ManagementAgent.GetManagementAgents())
            {
                bool found = false;

                foreach (MAConfigParameters config in this.ManagementAgents)
                {
                    if (config.ManagementAgentID == ma.ID)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    MAConfigParameters p = new MAConfigParameters(ma.Name, ma.ID);
                    p.Disabled = true;
                    p.AutoImportIntervalMinutes = 60;
                    MAConfigDiscovery.DoAutoRunProfileDiscovery(p, ma);
                    MAConfigDiscovery.AddDefaultTriggers(p, ma);

                    this.ManagementAgents.Add(p);
                }
            }
        }
    }
}
