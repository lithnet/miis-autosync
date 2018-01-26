using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionQueue
    {
        public static long CurrentQueueID;

        private BlockingCollection<ExecutionParameters> queue;
        private ExecutionParameterCollection internalList;
        private MAControllerConfiguration config;
        private ControllerLogger logger;
        private MAStatus status;
        private IReadOnlyDictionary<string, RunConfiguration> runProfiles;

        public ExecutionParameters StagedRun { get; set; }

        public ExecutionQueue(MAControllerConfiguration config, ControllerLogger logger, MAStatus status, IReadOnlyDictionary<string, RunConfiguration> runProfiles)
        {
            this.config = config;
            this.internalList = new ExecutionParameterCollection();
            this.queue = new BlockingCollection<ExecutionParameters>(this.internalList);
            this.logger = logger;
            this.status = status;
            this.runProfiles = runProfiles;
        }

        public void Reset()
        {
            this.queue.CompleteAdding();
        }

        public IEnumerable<ExecutionParameters> GetConsumingEnumerable(CancellationToken t)
        {
            return this.queue.GetConsumingEnumerable(t);
        }

        public void Add(string runProfileName, string source)
        {
            this.Add(new ExecutionParameters(runProfileName), source);
        }

        public void Add(ExecutionParameters p, string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p.RunProfileName))
                {
                    if (p.RunProfileType == MARunProfileType.None)
                    {
                        this.logger.LogInfo($"Dropping pending action request from '{source}' as no run profile name or run profile type was specified");
                        return;
                    }

                    if (p.PartitionID != Guid.Empty)
                    {
                        p.RunProfileName = this.config.GetRunProfileName(p.RunProfileType, p.PartitionID);

                        if (p.RunProfileName == null)
                        {
                            this.logger.LogInfo($"Dropping {p.RunProfileType} request from '{source}' as no matching run profile could be found in the specified partition {p.PartitionID}");
                            return;
                        }
                    }
                    else
                    {
                        p.RunProfileName = this.config.GetRunProfileName(p.RunProfileType, p.PartitionName);
                    }

                    if (string.IsNullOrWhiteSpace(p.RunProfileName))
                    {
                        this.logger.LogInfo($"Dropping {p.RunProfileType} request from '{source}' as no matching run profile could be found in the management agent partition {p.PartitionName}");
                        return;
                    }
                }

                this.SetExclusiveMode(p);

                if (this.StagedRun == p)
                {
                    this.logger.LogInfo($"{p.RunProfileName} requested by {source} was ignored because the run profile was already staged");
                    return;
                }

                if (this.internalList.Contains(p))
                {
                    if (p.RunImmediate && this.internalList.Count > 1)
                    {
                        this.logger.LogInfo($"Moving {p.RunProfileName} to the front of the execution queue");
                        this.internalList.MoveToFront(p);
                    }
                    else
                    {
                        this.logger.LogInfo($"{p.RunProfileName} requested by {source} was ignored because the run profile was already queued");
                    }

                    return;
                }

                p.QueueID = Interlocked.Increment(ref ExecutionQueue.CurrentQueueID);

                this.logger.Trace($"Got queue request for {p.RunProfileName} with id {p.QueueID}");

                if (p.RunImmediate)
                {
                    this.queue.Add(p);
                    this.internalList.MoveToFront(p);
                    this.logger.LogInfo($"Added {p.RunProfileName} to the front of the execution queue (triggered by: {source})");
                }
                else
                {
                    this.queue.Add(p);
                    this.logger.LogInfo($"Added {p.RunProfileName} to the execution queue (triggered by: {source})");
                }

                //this.counters.CurrentQueueLength.Increment();

                this.UpdateExecutionQueueState();

                this.logger.LogInfo($"Current queue: {this.GetQueueItemNames()}");
            }
            catch (OperationCanceledException)
            { }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"An unexpected error occurred while adding the pending action {p?.RunProfileName}. The event has been discarded");
            }
        }

        private void UpdateExecutionQueueState()
        {
            this.status.ExecutionQueue = this.GetQueueItemNames();
        }

        public string GetQueueItemNames(string currentlyExecuting = null)
        {
            // ToArray is implemented by BlockingCollection and allows an approximate copy of the data to be made in 
            // the event an add or remove is in progress. Other functions such as ToList are generic and can cause
            // collection modified exceptions when enumerating the values

            string queuedNames = string.Join(",", this.internalList.ToArray().Select(t => t.RunProfileName));

            if (currentlyExecuting != null)
            {
                string current = currentlyExecuting + "*";

                if (string.IsNullOrWhiteSpace(queuedNames))
                {
                    return current;
                }
                else
                {
                    return string.Join(",", current, queuedNames);
                }
            }
            else
            {
                return queuedNames;
            }
        }

        private void SetExclusiveMode(ExecutionParameters action)
        {
            if (Program.ActiveConfig.Settings.RunMode == RunMode.Exclusive)
            {
                action.Exclusive = true;
            }
            else if (Program.ActiveConfig.Settings.RunMode == RunMode.Supported)
            {
                if (this.runProfiles.IsSyncStep(action.RunProfileName))
                {
                    action.Exclusive = true;
                }
            }
        }
    }
}
