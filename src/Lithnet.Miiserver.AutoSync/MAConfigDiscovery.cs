using System.Collections.Generic;
using System.Linq;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public static class MAConfigDiscovery
    {
        public static MAConfigParameters GetConfig(ManagementAgent ma, IEnumerable<object> configItems)
        {
            foreach(object o in configItems)
            {
                MAConfigParameters p = o as MAConfigParameters;

                if (p != null)
                {
                    return p;
                }
            }

            return MAConfigDiscovery.DoAutoRunProfileDiscovery(ma);
        }

        private static MAConfigParameters DoAutoRunProfileDiscovery(ManagementAgent ma)
        {
            Logger.WriteLine("{0}: Performing run profile auto-discovery", ma.Name);

            MAConfigParameters config = new MAConfigParameters();
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

                return config;
            }
            else
            {
                return new MAConfigParameters() { Disabled = true };
            }
        }
    }
}
