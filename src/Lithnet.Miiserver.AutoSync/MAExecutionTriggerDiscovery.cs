using System;
using System.Collections.Generic;
using System.Linq;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;
using System.IO;
using System.Xml;

namespace Lithnet.Miiserver.AutoSync
{
    public static class MAExecutionTriggerDiscovery
    {
        private const int DefaultIntervalMinutes = 15;

        public static IList<IMAExecutionTrigger> GetExecutionTriggers(ManagementAgent ma, MAConfigParameters config, IEnumerable<object> configItems)
        {
            List<IMAExecutionTrigger> triggers = new List<IMAExecutionTrigger>();

            triggers.AddRange(MAExecutionTriggerDiscovery.GetDefaultTriggers(ma, config, configItems));

            IList<IMAExecutionTrigger> customTriggers = MAExecutionTriggerDiscovery.GetCustomTriggers(ma) ?? MAExecutionTriggerDiscovery.DoAutoTriggerDiscovery(ma, config);

            if (customTriggers != null)
            {
                triggers.AddRange(customTriggers);
            }

            return triggers;
        }

        private static IList<IMAExecutionTrigger> GetCustomTriggers(ManagementAgent ma)
        {
            List<IMAExecutionTrigger> triggers = new List<IMAExecutionTrigger>();

            foreach (string filename in Directory.EnumerateFiles(Settings.ConfigPath, $"Trigger-{Global.CleanMAName(ma.Name)}-*.ps1", SearchOption.TopDirectoryOnly))
            {
                PowerShellExecutionTrigger t = new PowerShellExecutionTrigger
                {
                    ScriptPath = Path.Combine(Settings.ConfigPath, filename)
                };

                triggers.Add(t);
            }

            return triggers.Count > 0 ? triggers : null;
        }

        private static IList<IMAExecutionTrigger> DoAutoTriggerDiscovery(ManagementAgent ma, MAConfigParameters config)
        {
            List<IMAExecutionTrigger> triggers = new List<IMAExecutionTrigger>();

            if (config.AutoImportScheduling == AutoImportScheduling.Disabled)
            {
                return triggers;
            }

            if ((ma.Category == "FIM" || ma.Category == "AD") && config.AutoImportScheduling != AutoImportScheduling.Enabled)
            {
                return triggers;
            }

            Logger.WriteLine("Performing trigger auto-discovery for MA {0}", ma.Name);

            //if (IsSourceMA(ma) || config.AutoImportScheduling == AutoImportScheduling.Enabled)
            //{
            //    IntervalExecutionTrigger t2 = new IntervalExecutionTrigger(MARunProfileType.DeltaImport, MAExecutionTriggerDiscovery.GetTriggerInterval(ma));
            //    triggers.Add(t2);
            //}

            return triggers;
        }
        
        internal static int GetTriggerInterval(ManagementAgent ma)
        {
            int deltaCount = 0;
            int deltaTotalTime = 0;

            int fullCount = 0;
            int fullTotalTime = 0;

            try
            {
                IList<RunDetails> rh = ma.GetRunHistory(200).ToList();

                foreach (RunDetails rd in rh)
                {
                    if (rd.EndTime == null || rd.StartTime == null || rd.StepDetails == null)
                    {
                        continue;
                    }

                    foreach (StepDetails sd in rd.StepDetails)
                    {
                        if (sd.StepDefinition == null || sd.EndDate == null || sd.StartDate == null)
                        {
                            continue;
                        }

                        if (sd.StepDefinition.Type == RunStepType.DeltaImport)
                        {
                            TimeSpan ts = sd.EndDate.Value - sd.StartDate.Value;

                            deltaCount++;
                            deltaTotalTime = deltaTotalTime + (int)ts.TotalSeconds;
                        }
                        else if (sd.StepDefinition.Type == RunStepType.FullImport)
                        {
                            TimeSpan ts = sd.EndDate.Value - sd.StartDate.Value;

                            fullCount++;
                            fullTotalTime = fullTotalTime + (int)ts.TotalSeconds;
                        }
                    }

                    if (deltaCount > 50)
                    {
                        break;
                    }
                }
            }
            catch (XmlException ex)
            {
                Logger.WriteLine("There was an error searching the run history. The run history may be corrupted. Using default intervals");
                Logger.WriteException(ex);
            }

            if (deltaCount == 0 && fullCount > 0)
            {
                return ((fullTotalTime / fullCount)) + (MAExecutionTriggerDiscovery.DefaultIntervalMinutes * 60);
            }
            else if (deltaCount == 0 && fullCount == 0)
            {
                return MAExecutionTriggerDiscovery.DefaultIntervalMinutes * 60;
            }

            return ((deltaTotalTime / deltaCount)) + (MAExecutionTriggerDiscovery.DefaultIntervalMinutes * 60);
        }

        private static IEnumerable<IMAExecutionTrigger> GetDefaultTriggers(ManagementAgent ma, MAConfigParameters config, IEnumerable<object> configItems)
        {
            List<IMAExecutionTrigger> triggers = new List<IMAExecutionTrigger>();

            if (config.DisableDefaultTriggers)
            {
                return triggers;
            }

            switch (ma.Category)
            {
                case "FIM":
                    FimServicePendingImportTrigger t1 = new FimServicePendingImportTrigger(MAExecutionTriggerDiscovery.GetFimServiceHostName(ma));
                    triggers.Add(t1);
                    break;

                case "ADAM":
                case "AD":
                    ActiveDirectoryChangeTrigger listenerConfig = configItems.OfType<ActiveDirectoryChangeTrigger>().FirstOrDefault();

                    if (listenerConfig == null)
                    {
                        listenerConfig = MAExecutionTriggerDiscovery.GetADConfiguration(ma);
                    }

                    triggers.Add(listenerConfig);
                    break;

                default:
                    break;
            }

            return triggers;
        }

        private static bool IsSourceMA(ManagementAgent ma)
        {
            string madata = ma.ExportManagementAgent();

            XmlDocument d = new XmlDocument();
            d.LoadXml(madata);

            int eafCount = d.SelectNodes("/export-ma/ma-data/export-attribute-flow/export-flow-set/export-flow")?.Count ?? 0;
            int iafCount = d.SelectNodes($"/export-ma/mv-data/import-attribute-flow/import-flow-set/import-flows/import-flow[@src-ma='{ma.ID.ToString("B").ToUpper()}']")?.Count ?? 0;

            return iafCount > eafCount;
        }

        private static string GetFimServiceHostName(ManagementAgent ma)
        {
            XmlNode privateData = ma.GetPrivateData();

            return privateData.SelectSingleNode("fimma-configuration/connection-info/serviceHost")?.InnerText;
        }

        private static ActiveDirectoryChangeTrigger GetADConfiguration(ManagementAgent ma)
        {
            ActiveDirectoryChangeTrigger config = new ActiveDirectoryChangeTrigger();

            string privateData = ma.ExportManagementAgent();

            XmlDocument d = new XmlDocument();
            d.LoadXml(privateData);


            XmlNode partitionNode = d.SelectSingleNode("/export-ma/ma-data/ma-partition-data/partition[selected=1 and custom-data/adma-partition-data[is-domain=1]]");

            config.HostName = d.SelectSingleNode("/export-ma/ma-data/private-configuration/adma-configuration/forest-name")?.InnerText;
            config.BaseDN = partitionNode?.SelectSingleNode("custom-data/adma-partition-data/dn")?.InnerText;
            config.ObjectClasses = partitionNode?.SelectNodes("filter/object-classes/object-class")?.OfType<XmlElement>().Where(t => t.InnerText != "container" && t.InnerText != "domainDNS" && t.InnerText != "organizationalUnit").Select(u => u.InnerText).ToArray();
            config.LastLogonTimestampOffset = new TimeSpan(0, 0, 300);

            return config;
        }
    }
}
