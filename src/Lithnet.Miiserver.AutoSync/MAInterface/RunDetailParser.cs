using System;
using System.Collections.Generic;
using System.Linq;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal class RunDetailParser
    {
        private MAControllerConfiguration config;

        private ControllerLogger logger;

        private Dictionary<string, string> perProfileLastRunStatus;

        public RunDetailParser(MAControllerConfiguration config, ControllerLogger logger)
        {
            this.config = config;
            this.logger = logger;
            this.perProfileLastRunStatus = new Dictionary<string, string>();
        }

        public void ThrowOnThresholdsExceeded(RunDetails r)
        {
            if (this.config.StagingThresholds == null)
            {
                return;
            }

            foreach (StepDetails s in r.StepDetails)
            {
                if (this.config.StagingThresholds.Adds > 0)
                {
                    if (s.StagingCounters.StageAdd >= this.config.StagingThresholds.Adds)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageAdd} adds which triggered the threshold of {this.config.StagingThresholds.Adds}", r);
                    }
                }

                if (this.config.StagingThresholds.Deletes > 0)
                {
                    if (s.StagingCounters.StageDelete >= this.config.StagingThresholds.Deletes)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageDelete} deletes which triggered the threshold of {this.config.StagingThresholds.Deletes}", r);
                    }
                }

                if (this.config.StagingThresholds.Renames > 0)
                {
                    if (s.StagingCounters.StageRename >= this.config.StagingThresholds.Renames)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageRename} renames which triggered the threshold of {this.config.StagingThresholds.Renames}", r);
                    }
                }

                if (this.config.StagingThresholds.Updates > 0)
                {
                    if (s.StagingCounters.StageUpdate >= this.config.StagingThresholds.Updates)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageUpdate} updates which triggered the threshold of {this.config.StagingThresholds.Updates}", r);
                    }
                }

                if (this.config.StagingThresholds.DeleteAdds > 0)
                {
                    if (s.StagingCounters.StageDeleteAdd >= this.config.StagingThresholds.DeleteAdds)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageDeleteAdd} delete/adds which triggered the threshold of {this.config.StagingThresholds.DeleteAdds}", r);
                    }
                }

                if (this.config.StagingThresholds.Changes > 0)
                {
                    if (s.StagingCounters.StagingChanges >= this.config.StagingThresholds.Changes)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StagingChanges} total changes which triggered the threshold of {this.config.StagingThresholds.Changes}", r);
                    }
                }
            }
        }
        
        public void TrySendMail(RunDetails r)
        {
            try
            {
                this.SendMail(r);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Send mail failed");
            }
        }

        public void SendMail(RunDetails r)
        {
            if (!ShouldSendMail(r))
            {
                return;
            }

            if (this.perProfileLastRunStatus.ContainsKey(r.RunProfileName))
            {
                if (this.perProfileLastRunStatus[r.RunProfileName] == r.LastStepStatus)
                {
                    if (!Program.ActiveConfig.Settings.MailSendAllErrorInstances)
                    {
                        // The last run returned the same return code. Do not send again.
                        return;
                    }
                }
                else
                {
                    this.perProfileLastRunStatus[r.RunProfileName] = r.LastStepStatus;
                }
            }
            else
            {
                this.perProfileLastRunStatus.Add(r.RunProfileName, r.LastStepStatus);
            }

            MessageSender.SendMessage($"{r.MAName} {r.RunProfileName}: {r.LastStepStatus}", MessageBuilder.GetMessageBody(r));
        }

        public static void SendThresholdExceededMail(RunDetails r, string message)
        {
            if (!MessageSender.CanSendMail())
            {
                return;
            }

            MessageSender.SendMessage($"{r.MAName} {r.RunProfileName}: Controller stopped: Threshold exceeded", MessageBuilder.GetMessageBody(r, message));
        }

        public static bool ShouldSendMail(RunDetails r)
        {
            if (!MessageSender.CanSendMail())
            {
                return false;
            }

            return Program.ActiveConfig.Settings.MailIgnoreReturnCodes == null ||
                   !Program.ActiveConfig.Settings.MailIgnoreReturnCodes.Contains(r.LastStepStatus, StringComparer.OrdinalIgnoreCase);
        }

        public bool WasFollowupAlreadyPerformed(RunDetails d, int i, Tuple<PartitionConfiguration, MARunProfileType> result)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                StepDetails f = d.StepDetails[j];

                if (f.StepDefinition == null)
                {
                    this.logger.LogWarn($"Step detail was missing the step definition");
                    continue;
                }

                PartitionConfiguration p = this.config.Partitions.GetActiveItemOrNull(f.StepDefinition.Partition);

                if (p == null || p.ID != result.Item1.ID)
                {
                    // not for this partition
                    continue;
                }

                if (f.StepDefinition.IsImportStep && result.Item2 == MARunProfileType.DeltaImport)
                {
                    // the required import follow up has already happened
                    return true;
                }

                if (f.StepDefinition.IsExportStep && result.Item2 == MARunProfileType.Export)
                {
                    // the required export has already happened
                    return true;
                }

                if (f.StepDefinition.IsSyncStep && result.Item2 == MARunProfileType.DeltaSync)
                {
                    // the required sync has already happened
                    return true;
                }
            }

            return false;
        }
        
        public Tuple<PartitionConfiguration, MARunProfileType> GetImportExportFollowUpActions(StepDetails s)
        {
            if (s.StepDefinition == null)
            {
                this.logger.LogWarn($"Step detail was missing the step definition");
                return null;
            }

            if (s.StepDefinition.Type == RunStepType.Export)
            {
                return this.GetExportFollowUpAction(s);
            }

            if (s.StepDefinition.Type == RunStepType.FullImport || s.StepDefinition.Type == RunStepType.DeltaImport)
            {
                return this.GetImportFollowUpAction(s);
            }

            return null;
        }

        public Tuple<PartitionConfiguration, MARunProfileType> GetImportFollowUpAction(StepDetails s)
        {
            if (!s.StepDefinition.IsImportStep)
            {
                return null;
            }

            if (!s.StagingCounters?.HasChanges ?? false)
            {
                this.logger.Trace($"No staged imports in step {s.StepNumber}");
                return null;
            }

            // has staged imports
            this.logger.Trace($"Staged imports in step {s.StepNumber}");

            string partitionName = s.StepDefinition.Partition;

            if (partitionName == null)
            {
                this.logger.LogWarn($"Partition in step {s.StepNumber} was blank");
                return null;
            }

            PartitionConfiguration p = this.config.Partitions.GetActiveItemOrNull(partitionName);

            if (p == null)
            {
                this.logger.LogWarn($"Could not find the partition {partitionName}");
                return null;
            }

            return new Tuple<PartitionConfiguration, MARunProfileType>(p, MARunProfileType.DeltaSync);
        }

        public Tuple<PartitionConfiguration, MARunProfileType> GetExportFollowUpAction(StepDetails s)
        {
            if (!s.StepDefinition.IsExportStep)
            {
                return null;
            }

            if (!s.ExportCounters?.HasChanges ?? false)
            {
                this.logger.Trace($"No unconfirmed exports in step {s.StepNumber}");
                return null;
            }

            // has unconfirmed exports
            this.logger.Trace($"Unconfirmed exports in step {s.StepNumber}");

            string partitionName = s.StepDefinition.Partition;

            if (partitionName == null)
            {
                this.logger.LogWarn($"Partition in step {s.StepNumber} was blank");
                return null;
            }

            PartitionConfiguration p = this.config.Partitions.GetActiveItemOrNull(partitionName);

            if (p == null)
            {
                this.logger.LogWarn($"Could not find the partition {partitionName}");
                return null;
            }

            return new Tuple<PartitionConfiguration, MARunProfileType>(p, MARunProfileType.DeltaImport);
        }
    }
}
