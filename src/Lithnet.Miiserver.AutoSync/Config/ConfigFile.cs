using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Lithnet.Miiserver.Client;
using System.Linq;
using System.Xml;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "lithnet-autosync")]
    public class ConfigFile
    {
        private const int CurrentSchemaVersion = 1;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        [DataMember(Name = "management-agents")]
        public MAControllerConfigurationCollection ManagementAgents { get; set; }

        [DataMember(Name = "settings")]
        public Settings Settings { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "version")]
        public int Version { get; set; }

        [DataMember(Name = "schema-version")]
        public int SchemaVersion { get; set; }

        public ConfigFile()
        {
            this.ManagementAgents = new MAControllerConfigurationCollection();
            this.Settings = new Settings();
        }

        internal void ValidateManagementAgents()
        {
            foreach (MAControllerConfiguration config in this.ManagementAgents)
            {
                config.ResolveManagementAgent();
            }

            this.AddMissingManagementAgents();
        }

        private Guid? GetPartitionFromRunProfile(string runProfileName, ManagementAgent ma)
        {
            if (runProfileName == null)
            {
                return null;
            }

            if (!ma.RunProfiles.ContainsKey(runProfileName))
            {
                return null;
            }

            RunConfiguration r = ma.RunProfiles[runProfileName];

            return r.RunSteps?.FirstOrDefault()?.Partition;
        }

        private bool DoSchemaUpdate(string file)
        {
            if (this.SchemaVersion == 0)
            {
                return this.DoSchemaUpdatev1(file);
            }

            return false;
        }

        private bool DoSchemaUpdatev1(string file)
        {
            logger.Info("Configuration file upgrade in progress");

            XmlDocument d = new XmlDocument();
            var mgr = new XmlNamespaceManager(d.NameTable);
            mgr.AddNamespace("a", "http://schemas.datacontract.org/2004/07/Lithnet.Miiserver.AutoSync");
            d.Load(file);

            foreach (XmlNode node in d.SelectNodes("/a:lithnet-autosync/a:management-agents/a:management-agent", mgr))
            {
                string maName = node.SelectSingleNode("a:name", mgr)?.InnerText;
                string maidstr = node.SelectSingleNode("a:id", mgr)?.InnerText;
                Guid maID = maidstr == null ? Guid.Empty : new Guid(maidstr);

                Guid? id = Global.FindManagementAgent(maName, maID);

                if (id == null)
                {
                    continue;
                }

                ManagementAgent ma = ManagementAgent.GetManagementAgent(id.Value);

                logger.Info($"Processing management agent controller {ma.Name}");

                MAControllerConfiguration c = this.ManagementAgents.GetItemOrDefault(id.Value);

                foreach (PartitionConfiguration p in c.Partitions.ActiveConfigurations)
                {
                    p.AutoImportEnabled = node.SelectSingleNode("a:auto-import-scheduling", mgr)?.InnerText == "enabled";
                    p.AutoImportIntervalMinutes = int.Parse(node.SelectSingleNode("a:auto-import-interval", mgr)?.InnerText ?? "0");
                }

                string rpName = node.SelectSingleNode("a:run-profile-confirming-import", mgr)?.InnerText;
                Guid? pid = this.GetPartitionFromRunProfile(rpName, ma);

                if (pid != null)
                {
                    PartitionConfiguration partition = c.Partitions.GetItemOrNull(pid.Value);

                    if (partition != null)
                    {
                        partition.ConfirmingImportRunProfileName = rpName;
                        logger.Trace($"Migrating run profile {nameof(partition.ConfirmingImportRunProfileName)}-{partition.ConfirmingImportRunProfileName} to partition {partition.Name}");
                    }
                }

                rpName = node.SelectSingleNode("a:run-profile-delta-import", mgr)?.InnerText;
                pid = this.GetPartitionFromRunProfile(rpName, ma);
                if (pid != null)
                {
                    PartitionConfiguration partition = c.Partitions.GetItemOrNull(pid.Value);

                    if (partition != null)
                    {
                        partition.DeltaImportRunProfileName = rpName;
                        logger.Trace($"Migrating run profile {nameof(partition.DeltaImportRunProfileName)}-{partition.DeltaImportRunProfileName} to partition {partition.Name}");
                    }
                }

                rpName = node.SelectSingleNode("a:run-profile-delta-sync", mgr)?.InnerText;
                pid = this.GetPartitionFromRunProfile(rpName, ma);
                if (pid != null)
                {
                    PartitionConfiguration partition = c.Partitions.GetItemOrNull(pid.Value);

                    if (partition != null)
                    {
                        partition.DeltaSyncRunProfileName = rpName;
                        logger.Trace($"Migrating run profile {nameof(partition.DeltaSyncRunProfileName)}-{partition.DeltaSyncRunProfileName} to partition {partition.Name}");
                    }
                }

                rpName = node.SelectSingleNode("a:run-profile-export", mgr)?.InnerText;
                pid = this.GetPartitionFromRunProfile(rpName, ma);
                if (pid != null)
                {
                    PartitionConfiguration partition = c.Partitions.GetItemOrNull(pid.Value);

                    if (partition != null)
                    {
                        partition.ExportRunProfileName = rpName;
                        logger.Trace($"Migrating run profile {nameof(partition.ExportRunProfileName)}-{partition.ExportRunProfileName} to partition {partition.Name}");
                    }
                }

                rpName = node.SelectSingleNode("a:run-profile-full-import", mgr)?.InnerText;
                pid = this.GetPartitionFromRunProfile(rpName, ma);
                if (pid != null)
                {
                    PartitionConfiguration partition = c.Partitions.GetItemOrNull(pid.Value);

                    if (partition != null)
                    {
                        partition.FullImportRunProfileName = rpName;
                        logger.Trace($"Migrating run profile {nameof(partition.FullImportRunProfileName)}-{partition.FullImportRunProfileName} to partition {partition.Name}");
                    }
                }

                rpName = node.SelectSingleNode("a:run-profile-full-sync", mgr)?.InnerText;
                pid = this.GetPartitionFromRunProfile(rpName, ma);
                if (pid != null)
                {
                    PartitionConfiguration partition = c.Partitions.GetItemOrNull(pid.Value);

                    if (partition != null)
                    {
                        partition.FullSyncRunProfileName = rpName;
                        logger.Trace($"Migrating run profile {nameof(partition.FullSyncRunProfileName)}-{partition.FullSyncRunProfileName} to partition {partition.Name}");
                    }
                }

                rpName = node.SelectSingleNode("a:run-profile-scheduled-import", mgr)?.InnerText;
                pid = this.GetPartitionFromRunProfile(rpName, ma);
                if (pid != null)
                {
                    PartitionConfiguration partition = c.Partitions.GetItemOrNull(pid.Value);

                    if (partition != null)
                    {
                        partition.ScheduledImportRunProfileName = rpName;
                        logger.Trace($"Migrating run profile {nameof(partition.ScheduledImportRunProfileName)}-{partition.ScheduledImportRunProfileName} to partition {partition.Name}");
                    }
                }
            }

            this.SchemaVersion = 1;
            return true;
        }

        internal static ConfigFile Load(string file)
        {
            ConfigFile f = Serializer.Read<ConfigFile>(file);
            f.ValidateManagementAgents();

            if (f.DoSchemaUpdate(file))
            {
//#warning Save not enabled
                ConfigFile.Save(f, file);
            }

            return f;
        }

        public static void Save(ConfigFile config, string filename)
        {
            config.SchemaVersion = ConfigFile.CurrentSchemaVersion;
            Serializer.Save(filename, config);
        }

        private void AddMissingManagementAgents()
        {
            foreach (ManagementAgent ma in ManagementAgent.GetManagementAgents())
            {
                bool found = false;

                foreach (MAControllerConfiguration config in this.ManagementAgents)
                {
                    if (config.ManagementAgentID == ma.ID)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    MAControllerConfiguration p = new MAControllerConfiguration(ma.Name, ma.ID);
                    p.Disabled = true;
                    p.ResolvePartitions(ma);
                    MAConfigDiscovery.AddDefaultTriggers(p, ma);

                    this.ManagementAgents.Add(p);
                }
            }
        }
    }
}
