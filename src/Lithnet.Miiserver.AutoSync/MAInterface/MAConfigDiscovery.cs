using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class MAConfigDiscovery
    {
        internal static void DoAutoRunProfileDiscovery(MAConfigParameters config, ManagementAgent ma)
        {
            Trace.WriteLine($"{config.ManagementAgentName}: Performing run profile auto-discovery");

            bool match = false;

            foreach (RunConfiguration profile in ma.RunProfiles.Values)
            {
                if (profile.RunSteps.Count == 0)
                {
                    continue;
                }

                if (profile.RunSteps.Count > 1)
                {
                    Trace.WriteLine($"{ma.Name}: Ignoring multi-step run profile {profile.Name}");
                    continue;
                }

                if (profile.RunSteps.Any(t => t.IsTestRun))
                {
                    Trace.WriteLine($"{ma.Name}: Ignoring test run profile {profile.Name}");
                    continue;
                }

                if (profile.RunSteps.Any(t => t.IsCombinedStep))
                {
                    Trace.WriteLine($"{ma.Name}: Ignoring combined step profile {profile.Name}");
                    continue;
                }

                RunStep step = profile.RunSteps.First();

                if (step.ObjectLimit > 0)
                {
                    Trace.WriteLine($"{ma.Name}: Ignoring limited step run profile {profile.Name}");
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

                Trace.WriteLine($"{ma.Name}: Found {step.Type} profile {profile.Name}");
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

        internal static void AddDefaultTriggers(MAConfigParameters config, ManagementAgent ma)
        {
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
