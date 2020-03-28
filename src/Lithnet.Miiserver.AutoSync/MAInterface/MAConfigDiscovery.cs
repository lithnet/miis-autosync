using System;
using System.Linq;
using Lithnet.Miiserver.Client;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class MAConfigDiscovery
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        internal static void DoAutoRunProfileDiscovery(PartitionConfiguration config, ManagementAgent ma)
        {
            logger.Trace($"{ma.Name}: Performing run profile auto-discovery for partition {config.Name}");

            bool match = false;

            foreach (RunConfiguration profile in ma.RunProfiles.Values)
            {
                if (profile.RunSteps.Count == 0)
                {
                    continue;
                }

                if (profile.RunSteps.Count > 1)
                {
                    logger.Trace($"{ma.Name}: Ignoring multi-step run profile {profile.Name}");
                    continue;
                }

                if (profile.RunSteps.Any(t => t.IsTestRun))
                {
                    logger.Trace($"{ma.Name}: Ignoring test run profile {profile.Name}");
                    continue;
                }

                if (profile.RunSteps.Any(t => t.IsCombinedStep))
                {
                    logger.Trace($"{ma.Name}: Ignoring combined step profile {profile.Name}");
                    continue;
                }

                RunStep step = profile.RunSteps.First();

                if (step.ObjectLimit > 0)
                {
                    logger.Trace($"{ma.Name}: Ignoring limited step run profile {profile.Name}");
                    continue;
                }

                if (config.ID != step.Partition)
                {
                    logger.Trace($"{ma.Name}: Ignoring profile for other partition {profile.Name}");
                    continue;
                }

                switch (step.Type)
                {
                    case RunStepType.Export:
                        config.ExportRunProfileName = profile.Name;
                        break;

                    case RunStepType.FullImport:
                        config.FullImportRunProfileName = profile.Name;
                        break;

                    case RunStepType.DeltaImport:
                        config.ScheduledImportRunProfileName = profile.Name;
                        config.DeltaImportRunProfileName = profile.Name;
                        break;

                    case RunStepType.DeltaSynchronization:
                        config.DeltaSyncRunProfileName = profile.Name;
                        break;

                    case RunStepType.FullSynchronization:
                        config.FullSyncRunProfileName = profile.Name;
                        break;

                    default:
                    case RunStepType.Unknown:
                        continue;
                }

                logger.Trace($"{ma.Name}: Found {step.Type} profile {profile.Name}");
                match = true;
            }

            if (match)
            {
                if (!string.IsNullOrWhiteSpace(config.ScheduledImportRunProfileName))
                {
                    config.ConfirmingImportRunProfileName = config.ScheduledImportRunProfileName;
                }
                else
                {
                    config.ConfirmingImportRunProfileName = config.FullImportRunProfileName;
                    config.ScheduledImportRunProfileName = config.FullImportRunProfileName;
                    config.DeltaImportRunProfileName = config.FullImportRunProfileName;
                }
            }
        }


        internal static void DoAutoRunProfileDiscovery(MAControllerConfiguration config, ManagementAgent ma)
        {
            foreach (PartitionConfiguration p in config.Partitions)
            {
                MAConfigDiscovery.DoAutoRunProfileDiscovery(p, ma);
            }
        }

        internal static void AddDefaultTriggers(MAControllerConfiguration config, ManagementAgent ma)
        {
            switch (ma.Category)
            {
                case "FIM":
                    FimServicePendingImportTrigger t1 = new FimServicePendingImportTrigger(ma);
                    config.Triggers.Add(t1);
                    break;

                case "ADAM":
                case "AD":
                case "AD GAL":
                    ActiveDirectoryChangeTrigger t2 = new ActiveDirectoryChangeTrigger(ma);
                    config.Triggers.Add(t2);
                    break;
            }
        }
    }
}
