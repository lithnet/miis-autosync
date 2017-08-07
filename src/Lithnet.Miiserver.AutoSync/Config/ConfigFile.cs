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
        public List<MAConfigParameters> ManagementAgents { get; set; }

        [DataMember(Name = "settings")]
        public Settings Settings { get; set; }

        public ConfigFile()
        {
            this.ManagementAgents = new List<MAConfigParameters>();
            this.Settings = new Settings();
        }

        public void ValidateManagementAgents()
        {
            foreach (MAConfigParameters config in this.ManagementAgents)
            {
                config.ResolveManagementAgent();
            }

            this.AddMissingManagementAgents();

            this.ManagementAgents = this.ManagementAgents.OrderBy(t => t.Disabled).ThenBy(t => t.ManagementAgentName).ToList();
        }

        public static ConfigFile Load(string file)
        {
            ConfigFile f = Serializer.Read<ConfigFile>(file);
            f.ValidateManagementAgents();

            return f;
        }

        public static void Save(ConfigFile config, string filename)
        {
            ConfigFile.MarkManagementAgentsAsConfigured(config);
            Serializer.Save(filename, config);
        }

        private static void MarkManagementAgentsAsConfigured(ConfigFile config)
        {
            foreach (MAConfigParameters p in config.ManagementAgents)
            {
                p.IsNew = false;
            }
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
                    MAConfigParameters p = new MAConfigParameters(ma);
                    p.IsNew = true;
                    p.Disabled = true;
                    p.AutoImportIntervalMinutes = 60;
                    MAConfigDiscovery.DoAutoRunProfileDiscovery(p);
                    MAConfigDiscovery.AddDefaultTriggers(p);

                    this.ManagementAgents.Add(p);
                }
            }
        }
    }
}
