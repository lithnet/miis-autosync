using System.Collections.Generic;
using System.Linq;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public static class MAConfigDiscovery
    {
        internal static void DoAutoRunProfileDiscovery(MAConfigParameters config)
        {
            Logger.WriteLine("{0}: Performing run profile auto-discovery", config.ManagementAgentName);
            ManagementAgent ma = config.ManagementAgent;

            bool match = false;

            foreach (RunConfiguration profile in ma.RunProfiles.Values)
            {
                if (profile.RunSteps.Count == 0)
                {
                    continue;
                }

                if (profile.RunSteps.Count > 1)
                {
                    Logger.WriteLine("{0}: Ignoring multi-step run profile {1}", LogLevel.Debug, ma.Name, profile.Name);
                    continue;
                }

                if (profile.RunSteps.Any(t => t.IsTestRun))
                {
                    Logger.WriteLine("{0}: Ignoring test run profile {1}", LogLevel.Debug, ma.Name, profile.Name);
                    continue;
                }

                if (profile.RunSteps.Any(t => t.IsCombinedStep))
                {
                    Logger.WriteLine("{0}: Ignoring combined step profile {1}", LogLevel.Debug, ma.Name, profile.Name);
                    continue;
                }

                RunStep step = profile.RunSteps.First();

                if (step.ObjectLimit > 0)
                {
                    Logger.WriteLine("{0}: Ignoring limited step run profile {1}", LogLevel.Debug, ma.Name, profile.Name);
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

                Logger.WriteLine("{0}: Found {1} profile {2}", ma.Name, step.Type, profile.Name);
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

        internal static void AddDefaultTriggers(MAConfigParameters config)
        {
            ManagementAgent ma = config.ManagementAgent;

            switch (ma.Category)
            {
                case "FIM":
                    FimServicePendingImportTrigger t1 = new FimServicePendingImportTrigger(ma);
                    config.Triggers.Add(t1);
                    break;

                case "ADAM":
                case "AD":
                    ActiveDirectoryChangeTrigger t2 = new ActiveDirectoryChangeTrigger(ma);
                    config.Triggers.Add(t2);
                    break;
            }
        }
    }
}
