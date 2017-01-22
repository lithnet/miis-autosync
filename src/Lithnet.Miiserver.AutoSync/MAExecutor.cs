using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    using System.Text;

    public class MAExecutor
    {
        protected static object GlobalStaggeredExecutionLock;
        protected static ManualResetEvent GlobalExclusiveOperationLock;
        protected static object GlobalSynchronizationStepLock;
        protected static List<WaitHandle> AllMaLocalOperationLocks;

        public static event SyncCompleteEventHandler SyncComplete;
        public delegate void SyncCompleteEventHandler(object sender, SyncCompleteEventArgs e);
        private ManagementAgent ma;
        private BlockingCollection<ExecutionParameters> pendingActions;
        private ExecutionParameterCollection pendingActionList;

        private ManualResetEvent localOperationLock;
        private System.Timers.Timer importCheckTimer;
        private System.Timers.Timer unmanagedChangesCheckTimer;
        private TimeSpan importInterval;

        private Dictionary<string, string> perProfileLastRunStatus;

        public MAConfigParameters Configuration { get; }

        public string ExecutingRunProfile { get; private set; }

        private List<IMAExecutionTrigger> ExecutionTriggers { get; }

        private MAController controller;

        private CancellationTokenSource token;

        private Task internalTask;

        static MAExecutor()
        {
            MAExecutor.GlobalSynchronizationStepLock = new object();
            MAExecutor.GlobalStaggeredExecutionLock = new object();
            MAExecutor.GlobalExclusiveOperationLock = new ManualResetEvent(true);
            MAExecutor.AllMaLocalOperationLocks = new List<WaitHandle>();
        }

        public MAExecutor(ManagementAgent ma, MAConfigParameters profiles)
        {
            this.ma = ma;
            this.pendingActionList = new ExecutionParameterCollection();
            this.pendingActions = new BlockingCollection<ExecutionParameters>(this.pendingActionList);
            this.perProfileLastRunStatus = new Dictionary<string, string>();
            this.ExecutionTriggers = new List<IMAExecutionTrigger>();
            this.Configuration = profiles;
            this.token = new CancellationTokenSource();
            this.controller = new MAController(ma);
            this.localOperationLock = new ManualResetEvent(true);
            MAExecutor.AllMaLocalOperationLocks.Add(this.localOperationLock);
            MAExecutor.SyncComplete += this.MAExecutor_SyncComplete;
            this.SetupImportSchedule();
            this.SetupUnmanagedChangesCheckTimer();
        }

        private void SetupUnmanagedChangesCheckTimer()
        {
            this.unmanagedChangesCheckTimer = new System.Timers.Timer();
            this.unmanagedChangesCheckTimer.Elapsed += this.UnmanagedChangesCheckTimer_Elapsed;
            this.unmanagedChangesCheckTimer.AutoReset = true;
            this.unmanagedChangesCheckTimer.Interval = Global.RandomizeOffset(Settings.UnmanagedChangesCheckInterval.TotalMilliseconds);
            this.unmanagedChangesCheckTimer.Start();
        }

        private void UnmanagedChangesCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.CheckAndQueueUnmanagedChanges();
        }

        private void SetupImportSchedule()
        {
            if (this.Configuration.AutoImportScheduling != AutoImportScheduling.Disabled)
            {
                if (this.Configuration.AutoImportScheduling == AutoImportScheduling.Enabled ||
                    (this.ma.ImportAttributeFlows.Select(t => t.ImportFlows).Count() >= this.ma.ExportAttributeFlows.Select(t => t.ExportFlows).Count()))
                {
                    this.importCheckTimer = new System.Timers.Timer();
                    this.importCheckTimer.Elapsed += this.ImportCheckTimer_Elapsed;
                    int importSeconds = this.Configuration.AutoImportIntervalMinutes > 0 ? this.Configuration.AutoImportIntervalMinutes * 60 : MAExecutionTriggerDiscovery.GetTriggerInterval(this.ma);
                    this.importInterval = new TimeSpan(0, 0, Global.RandomizeOffset(importSeconds));
                    this.importCheckTimer.Interval = this.importInterval.TotalMilliseconds;
                    this.importCheckTimer.AutoReset = true;
                    this.Log($"Starting import interval timer. Imports will be queued if they have not been run for {this.importInterval}");
                    this.importCheckTimer.Start();
                }
                else
                {
                    this.Log($"Import schedule not enabled");
                }
            }
            else
            {
                this.Log($"Import schedule disabled");
            }
        }

        private void ImportCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.ScheduledImportRunProfileName), "Import timer");
        }

        private void ResetImportTimerOnImport()
        {
            if (this.importCheckTimer != null)
            {
                this.Log($"Resetting import timer for {this.importInterval}");
                this.importCheckTimer.Stop();
                this.importCheckTimer.Start();
            }
        }

        public void AttachTrigger(params IMAExecutionTrigger[] triggers)
        {
            if (triggers == null)
            {
                throw new ArgumentNullException(nameof(triggers));
            }

            foreach (IMAExecutionTrigger trigger in triggers)
            {
                this.ExecutionTriggers.Add(trigger);
            }
        }

        private void StartTriggers()
        {
            foreach (IMAExecutionTrigger t in this.ExecutionTriggers)
            {
                try
                {
                    this.Log($"Registering execution trigger '{t.Name}'");
                    t.TriggerExecution += this.notifier_TriggerExecution;
                    t.Start();
                }
                catch (Exception ex)
                {
                    this.Log($"Could not start execution trigger {t.Name}");
                    Logger.WriteException(ex);
                }
            }
        }

        private void QueueFollowupActions(RunDetails d)
        {
            this.QueueFollowUpActionsExport(d);
            this.QueueFollowUpActionsImport(d);
            this.QueueFollowUpActionsSync(d);
        }

        private void QueueFollowUpActionsExport(RunDetails d)
        {
            if (this.CanConfirmExport())
            {
                if (MAExecutor.HasUnconfirmedExports(d))
                {
                    this.Trace($"Unconfirmed exports in last run");
                    this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.ConfirmingImportRunProfileName), d.RunProfileName, true);
                }
            }
        }

        private void QueueFollowUpActionsImport(RunDetails d)
        {
            if (MAExecutor.HasStagedImports(d))
            {
                this.Trace($"Staged imports in last run");
                this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.DeltaSyncRunProfileName), d.RunProfileName, true);
            }
        }

        private void QueueFollowUpActionsSync(RunDetails d)
        {
            SyncCompleteEventHandler registeredHandlers = MAExecutor.SyncComplete;

            if (registeredHandlers == null)
            {
                this.Trace($"No sync event handlers were registered");
                return;
            }

            foreach (StepDetails s in d.StepDetails)
            {
                if (!s.StepDefinition.IsSyncStep)
                {
                    continue;
                }

                foreach (OutboundFlowCounters item in s.OutboundFlowCounters)
                {
                    if (!item.HasChanges)
                    {
                        this.Trace($"No outbound changes detected for {item.ManagementAgent}");
                        continue;
                    }

                    SyncCompleteEventArgs args = new SyncCompleteEventArgs
                    {
                        SendingMAName = this.ma.Name,
                        TargetMA = item.MAID
                    };

                    this.Trace($"Sending outbound change notification for MA {item.ManagementAgent}");
                    registeredHandlers(this, args);
                }
            }
        }

        private void Log(string message)
        {
            Logger.WriteLine($"{this.ma.Name}: {message}");
        }

        private void Trace(string message)
        {
            System.Diagnostics.Trace.WriteLine($"{this.ma.Name}: {message}");
        }

        private void Execute(ExecutionParameters e)
        {
            try
            {
                this.token.Token.ThrowIfCancellationRequested();

                this.Trace($"LOCK: WAIT: GlobalExclusiveOp");
                MAExecutor.GlobalExclusiveOperationLock.WaitOne();
                this.Trace($"LOCK: CLEARED: GlobalExclusiveOp");

                this.token.Token.ThrowIfCancellationRequested();

                if (!this.controller.ShouldExecute(e.RunProfileName))
                {
                    this.Log($"Controller indicated that run profile {e.RunProfileName} should not be executed");
                    return;
                }

                this.WaitOnUnmanagedRun();

                this.token.Token.ThrowIfCancellationRequested();

                if (e.Exclusive)
                {
                    this.Log($"Entering exclusive mode for {e.RunProfileName}");
                    this.Trace("LOCK: SET: GlobalExclusiveOp");
                    // Signal all executors to wait before running their next job
                    MAExecutor.GlobalExclusiveOperationLock.Reset();

                    this.token.Token.ThrowIfCancellationRequested();

                    this.Log("Waiting for all MAs to complete");
                    // Wait for all  MAs to finish their current job
                    this.Trace($"LOCK: WAIT: AllLocalOps");
                    WaitHandle.WaitAll(MAExecutor.AllMaLocalOperationLocks.ToArray());
                    this.Trace($"LOCK: CLEARED: AllLocalOps");

                    this.token.Token.ThrowIfCancellationRequested();
                }

                // If another operation in this executor is already running, then wait for it to finish
                this.Trace($"LOCK: WAIT: LocalOp");
                this.localOperationLock.WaitOne();
                this.Trace($"LOCK: CLEARED: LocalOp");

                this.token.Token.ThrowIfCancellationRequested();

                // Signal the local lock that an event is running
                this.Trace($"LOCK: SET: LocalOp");
                this.localOperationLock.Reset();

                this.token.Token.ThrowIfCancellationRequested();

                this.Trace($"LOCK: WAIT: StaggeredExecution");
                // Grab the staggered execution lock, and hold for x seconds
                // This ensures that no MA can start within x seconds of another MA
                // to avoid deadlock conditions
                lock (MAExecutor.GlobalStaggeredExecutionLock)
                {
                    this.Trace($"LOCK: CLEARED: StaggeredExecution");
                    this.Trace($"LOCK: SET: StaggeredExecution");

                    this.token.Token.ThrowIfCancellationRequested();

                    Thread.Sleep(Settings.ExecutionStaggerInterval);
                }

                this.Trace($"LOCK: UNSET: StaggeredExecution");

                this.token.Token.ThrowIfCancellationRequested();

                if (this.ma.RunProfiles[e.RunProfileName].RunSteps.Any(t => t.IsImportStep))
                {
                    this.Trace($"Import step detected. Resetting timer");
                    this.ResetImportTimerOnImport();
                }

                try
                {
                    int count = 0;

                    while (count <= Settings.RetryCount || Settings.RetryCount < 0)
                    {
                        this.token.Token.ThrowIfCancellationRequested();

                        count++;
                        this.Log($"Executing {e.RunProfileName}");
                        string result = this.ma.ExecuteRunProfile(e.RunProfileName, this.token.Token);
                        this.Log($"{e.RunProfileName} returned {result}");

                        if (Settings.RetryCodes.Contains(result))
                        {
                            if (count > Settings.RetryCount && Settings.RetryCount >= 0)
                            {
                                this.Log($"Aborting run profile after {count} attempts");
                                break;
                            }

                            int interval = Global.RandomizeOffset(Settings.RetrySleepInterval.TotalMilliseconds * count);
                            this.Trace($"Sleeping thread for {interval}ms before retry");
                            Thread.Sleep(interval);
                            this.Log("Retrying operation");
                        }
                        else
                        {
                            break; 
                        }
                    }

                    this.token.Token.ThrowIfCancellationRequested();

                    Thread.Sleep(Settings.PostRunInterval);
                }
                catch (MAExecutionException ex)
                {
                    this.Log($"{e.RunProfileName} returned {ex.Result}");
                }

                this.token.Token.ThrowIfCancellationRequested();

                this.Trace($"Getting run results");
                using (RunDetails r = this.ma.GetLastRun())
                {
                    this.Trace($"Got run results");
                    this.PerformPostRunActions(r);
                }
            }
            catch (OperationCanceledException)
            {
                this.Trace($"Aborting execution of {e.RunProfileName} as cancellation was requested");
            }
            catch (System.Management.Automation.RuntimeException ex)
            {
                if (this.token.IsCancellationRequested)
                {
                    this.Trace($"Aborting execution of {e.RunProfileName} as cancellation was requested");
                    return;
                }

                UnexpectedChangeException changeException = ex.InnerException as UnexpectedChangeException;

                if (changeException != null)
                {
                    this.ProcessUnexpectedChangeException(changeException);
                }
                else
                {
                    this.Log($"Executor encountered an error executing run profile {this.ExecutingRunProfile}");
                    Logger.WriteException(ex);
                }
            }
            catch (UnexpectedChangeException ex)
            {
                if (this.token.IsCancellationRequested)
                {
                    this.Trace($"Aborting execution of {e.RunProfileName} as cancellation was requested");
                    return;
                }

                this.ProcessUnexpectedChangeException(ex);
            }
            catch (Exception ex)
            {
                this.Log($"Executor encountered an error executing run profile {this.ExecutingRunProfile}");
                Logger.WriteException(ex);
            }
            finally
            {
                // Reset the local lock so the next operation can run
                this.Trace($"LOCK: UNSET: LocalOp");
                this.localOperationLock.Set();

                if (e.Exclusive)
                {
                    this.Trace($"LOCK: UNSET: GlobalExclusive");
                    // Reset the global lock so pending operations can run
                    MAExecutor.GlobalExclusiveOperationLock.Set();
                }
            }
        }

        private void WaitOnUnmanagedRun()
        {
            if (this.ma.IsIdle())
            {
                return;
            }

            this.Trace($"Unmanaged run in progress");
            this.Trace($"LOCK: SET: LocalOp");
            this.localOperationLock.Reset();

            try
            {
                this.Log($"Waiting on unmanaged run {this.ma.ExecutingRunProfileName} to finish");

                if (this.ma.RunProfiles[this.ma.ExecutingRunProfileName].RunSteps.Any(t => t.IsSyncStep))
                {
                    this.Log($"Getting exclusive sync lock for unmanaged run");
                    this.Trace("LOCK: WAIT: Sync");
                    lock (MAExecutor.GlobalSynchronizationStepLock)
                    {
                        this.Trace("LOCK: CLEARED: Sync");
                        this.Trace("LOCK: SET: Sync");

                        if (this.token.IsCancellationRequested)
                        {
                            return;
                        }

                        this.ma.Wait(this.token.Token);
                    }

                    this.Trace("LOCK: UNSET: Sync");
                }
                else
                {
                    this.ma.Wait(this.token.Token);
                }

                if (this.token.IsCancellationRequested)
                {
                    return;
                }

                using (RunDetails ur = this.ma.GetLastRun())
                {
                    this.PerformPostRunActions(ur);
                }
            }
            finally
            {
                this.Trace($"LOCK: UNSET: LocalOp");
                this.localOperationLock.Set();
                this.Trace("Unmanaged run complete");
            }
        }

        private void PerformPostRunActions(RunDetails r)
        {
            this.TrySendMail(r);
            this.controller.ExecutionComplete(r);
            this.QueueFollowupActions(r);
        }

        private void ProcessUnexpectedChangeException(UnexpectedChangeException ex)
        {
            if (ex.ShouldTerminateService)
            {
                this.Log($"Controller indicated that service should immediately stop. Run profile {this.ExecutingRunProfile}");
                if (AutoSyncService.ServiceInstance == null)
                {
                    Environment.Exit(1);
                }
                else
                {
                    AutoSyncService.ServiceInstance.Stop();
                }
            }
            else
            {
                this.Log($"Controller indicated that management agent executor should stop further processing on this MA. Run Profile {this.ExecutingRunProfile}");
                this.Stop();
            }
        }

        public Task Start()
        {
            if (this.Configuration.Disabled)
            {
                throw new Exception("Cannot start executor as it is disabled");
            }

            Logger.WriteSeparatorLine('-');

            this.Log($"Starting executor");

            Logger.WriteRaw($"{this}\n");

            Logger.WriteSeparatorLine('-');

            this.internalTask = new Task(() =>
            {
                try
                {
                    this.Init();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    this.Log($"The MAExecutor encountered a unrecoverable error");
                    Logger.WriteLine(ex.Message);
                    Logger.WriteLine(ex.StackTrace);
                }
            }, this.token.Token);

            this.internalTask.Start();

            return this.internalTask;
        }

        public void Stop()
        {
            this.Log($"Stopping MAExecutor");
            this.token?.Cancel();

            this.importCheckTimer?.Stop();

            foreach (IMAExecutionTrigger x in this.ExecutionTriggers)
            {
                x.Stop();
            }

            this.Log($"Stopped execution triggers");

            if (this.internalTask != null && !this.internalTask.IsCompleted)
            {
                this.Log($"Waiting for cancellation to complete");
                this.internalTask.Wait();
                this.Log($"Cancellation completed");
            }

            this.internalTask = null;
        }

        private void Init()
        {
            if (!this.ma.IsIdle())
            {
                try
                {
                    this.Trace("LOCK: SET: LocalOp");
                    this.localOperationLock.Reset();
                    this.Log($"Waiting for current job to finish");
                    this.ma.Wait(this.token.Token);
                }
                finally
                {
                    this.Trace("LOCK: UNSET: LocalOp");
                    this.localOperationLock.Set();
                }
            }

            this.token.Token.ThrowIfCancellationRequested();

            this.CheckAndQueueUnmanagedChanges();

            this.token.Token.ThrowIfCancellationRequested();

            this.StartTriggers();

            try
            {
                foreach (ExecutionParameters action in this.pendingActions.GetConsumingEnumerable(this.token.Token))
                {
                    this.token.Token.ThrowIfCancellationRequested();

                    try
                    {
                        this.ExecutingRunProfile = action.RunProfileName;

                        this.SetExclusiveMode(action);

                        if (this.IsSyncStepOrFimMADeltaImport(action.RunProfileName))
                        {
                            this.Trace("LOCK: WAIT: Sync");
                            lock (MAExecutor.GlobalSynchronizationStepLock)
                            {
                                this.Trace("LOCK: CLEARED: Sync");
                                this.Trace("LOCK: SET: Sync");
                                this.Execute(action);
                            }

                            this.Trace("LOCK: UNSET: Sync");
                        }
                        else
                        {
                            this.Execute(action);
                        }
                    }
                    finally
                    {
                        this.ExecutingRunProfile = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SetExclusiveMode(ExecutionParameters action)
        {
            // If the debug flags for running serial sync operations are set, then mark the operation as exclusive
            if (Settings.RunAllExclusive)
            {
                action.Exclusive = true;
            }
            else if (Settings.RunSyncExclusive)
            {
                if (this.IsSyncStep(action.RunProfileName))
                {
                    action.Exclusive = true;
                }
            }
        }

        private bool IsSyncStepOrFimMADeltaImport(string runProfileName)
        {
            if (this.IsSyncStep(runProfileName))
            {
                return true;
            }

            if (this.ma.Category == "FIM")
            {
                if (this.ma.RunProfiles[runProfileName].RunSteps.Any(t => t.Type == RunStepType.DeltaImport))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSyncStep(string runProfileName)
        {
            return this.ma.RunProfiles[runProfileName].RunSteps.Any(t => t.IsSyncStep);
        }

        private void CheckAndQueueUnmanagedChanges()
        {
            try
            {
                this.Trace("Checking for unmanaged changes");
                // If another operation in this executor is already running, then wait for it to finish
                this.Trace("LOCK: WAIT: LocalOp");
                this.localOperationLock.WaitOne();
                this.Trace("LOCK: CLEARED: LocalOp");

                // Signal the local lock that an event is running
                this.Trace("LOCK: SET: LocalOp");
                this.localOperationLock.Reset();

                if (this.ShouldExportPendingChanges())
                {
                    this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.ExportRunProfileName), "Pending export check");
                }

                if (this.ShouldConfirmExport())
                {
                    this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.ConfirmingImportRunProfileName), "Unconfirmed export check");
                }

                if (this.ma.HasPendingImports())
                {
                    this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.DeltaSyncRunProfileName), "Staged import check");
                }
            }
            finally
            {
                this.Trace("LOCK: UNSET: LocalOp");
                // Reset the local lock so the next operation can run
                this.localOperationLock.Set();
            }
        }

        private void MAExecutor_SyncComplete(object sender, SyncCompleteEventArgs e)
        {
            if (e.TargetMA != this.ma.ID)
            {
                this.Trace($"Ignoring sync complete message from {e.SendingMAName} for {e.TargetMA}");

                return;
            }

            this.Trace($"Got sync complete message from {e.SendingMAName} for {e.TargetMA}");

            ExecutionParameters p = new ExecutionParameters(this.Configuration.ExportRunProfileName);
            this.AddPendingActionIfNotQueued(p, "Synchronization on " + e.SendingMAName);
        }

        private void notifier_TriggerExecution(object sender, ExecutionTriggerEventArgs e)
        {
            IMAExecutionTrigger trigger = (IMAExecutionTrigger)sender;

            if (string.IsNullOrWhiteSpace(e.Parameters.RunProfileName))
            {
                if (e.Parameters.RunProfileType == MARunProfileType.None)
                {
                    this.Log($"Received empty run profile from trigger {trigger.Name}");
                    return;
                }
            }

            this.AddPendingActionIfNotQueued(e.Parameters, trigger.Name);
        }

        private void AddPendingActionIfNotQueued(ExecutionParameters p, string source, bool runNext = false)
        {
            if (string.IsNullOrWhiteSpace(p.RunProfileName))
            {
                if (p.RunProfileType == MARunProfileType.None)
                {
                    this.Trace($"Dropping pending action request as no run profile name or run profile type was specified");
                    return;
                }

                p.RunProfileName = this.Configuration.GetRunProfileName(p.RunProfileType);
            }

            if (this.pendingActions.Contains(p))
            {
                if (runNext && this.pendingActions.Count > 1)
                {
                    this.Log($"Moving {p.RunProfileName} to the front of the execution queue");
                    this.pendingActionList.MoveToFront(p);
                }
                else
                {
                    this.Trace($"Ignoring queue request for {p.RunProfileName} as it already exists in the queue");
                }

                return;
            }

            // Removing this as it may caused changes to go unseen. Eg an import is in progress, 
            // a snapshot is taken, but new items become available during the import of the snapshot

            //if (p.RunProfileName.Equals(this.ExecutingRunProfile, StringComparison.OrdinalIgnoreCase))
            //{
            //    this.Trace($"Ignoring queue request for {p.RunProfileName} as it is currently executing");
            //    return;
            //}

            this.Trace($"Got queue request for {p.RunProfileName}");

            if (runNext)
            {
                this.pendingActions.Add(p);
                this.pendingActionList.MoveToFront(p);
                this.Log($"Added {p.RunProfileName} to the front of the execution queue (triggered by: {source})");
            }
            else
            {
                this.pendingActions.Add(p);
                this.Log($"Added {p.RunProfileName} to the execution queue (triggered by: {source})");
            }

            this.Log($"Current queue {this.GetQueueItemNames()}");
        }

        private string GetQueueItemNames()
        {
            string queuedNames = string.Join(",", this.pendingActions.ToList().Select(t => t.RunProfileName));

            if (this.ExecutingRunProfile != null)
            {
                return string.Join(",", this.ExecutingRunProfile + "*", queuedNames);
            }
            else
            {
                return queuedNames;
            }
        }

        private static bool HasUnconfirmedExports(RunDetails d)
        {
            if (d.StepDetails == null)
            {
                return false;
            }

            foreach (StepDetails s in d.StepDetails)
            {
                if (s.StepDefinition.IsImportStep)
                {
                    // If an import is present, before an export step, a confirming import is not required
                    return false;
                }

                if (s.StepDefinition.Type == RunStepType.Export)
                {
                    // If we get here, an export step has been found that it more recent than any import step
                    // that may be in the run profile
                    return s.ExportCounters?.HasChanges ?? false;
                }
            }

            return false;
        }

        private static bool HasStagedImports(RunDetails d)
        {
            if (d.StepDetails == null)
            {
                return false;
            }

            foreach (StepDetails s in d.StepDetails)
            {
                if (s.StepDefinition.IsSyncStep)
                {
                    // If a sync is present, before an import step, a sync is not required
                    return false;
                }

                if (s.StepDefinition.IsImportStep)
                {
                    // If we get here, an import step has been found that it more recent than any sync step
                    // that may be in the run profile
                    return s.StagingCounters?.HasChanges ?? false;
                }
            }

            return false;
        }

        private static bool HasUnconfirmedExports(StepDetails s)
        {
            return s?.ExportCounters?.HasChanges ?? false;
        }

        private bool HasUnconfirmedExportsInLastRun()
        {
            return MAExecutor.HasUnconfirmedExports(this.ma.GetLastRun()?.StepDetails?.FirstOrDefault());
        }

        private bool ShouldExportPendingChanges()
        {
            return this.CanExport() && this.ma.HasPendingExports();
        }

        private bool CanExport()
        {
            return !string.IsNullOrWhiteSpace(this.Configuration.ExportRunProfileName);
        }

        private bool CanConfirmExport()
        {
            return !string.IsNullOrWhiteSpace(this.Configuration.ConfirmingImportRunProfileName);
        }

        private bool ShouldConfirmExport()
        {
            return this.CanConfirmExport() && this.HasUnconfirmedExportsInLastRun();
        }

        private void TrySendMail(RunDetails r)
        {
            try
            {
                this.SendMail(r);
            }
            catch (Exception ex)
            {
                this.Log($"Send mail failed");
                Logger.WriteException(ex);
            }
        }

        private void SendMail(RunDetails r)
        {
            if (this.perProfileLastRunStatus.ContainsKey(r.RunProfileName))
            {
                if (this.perProfileLastRunStatus[r.RunProfileName] == r.LastStepStatus)
                {
                    if (Settings.MailSendOncePerStateChange)
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

            if (!MAExecutor.ShouldSendMail(r))
            {
                return;
            }

            MessageSender.SendMessage($"{r.MAName} {r.RunProfileName}: {r.LastStepStatus}", MessageBuilder.GetMessageBody(r));
        }

        private static bool ShouldSendMail(RunDetails r)
        {
            if (!MessageSender.CanSendMail())
            {
                return false;
            }

            return Settings.MailIgnoreReturnCodes == null || !Settings.MailIgnoreReturnCodes.Contains(r.LastStepStatus, StringComparer.OrdinalIgnoreCase);
        }


        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("--- Configuration ---");
            builder.AppendLine(this.Configuration.ToString());

            if (this.importCheckTimer?.Interval > 0 || this.Configuration.AutoImportScheduling != AutoImportScheduling.Disabled)
            {
                builder.AppendLine("--- Schedules ---");

                if (this.importCheckTimer?.Interval > 0)
                {
                    builder.AppendLine($"Maximum allowed interval between imports: {new TimeSpan(0, 0, 0, 0, (int)this.importCheckTimer.Interval)}");
                }

                if (this.Configuration.AutoImportScheduling != AutoImportScheduling.Disabled && this.Configuration.AutoImportIntervalMinutes > 0)
                {
                    builder.AppendLine($"Scheduled import interval: {new TimeSpan(0, this.Configuration.AutoImportIntervalMinutes, 0)}");
                }
            }

            builder.AppendLine();

            builder.AppendLine("--- Triggers ---");

            foreach (IMAExecutionTrigger trigger in this.ExecutionTriggers)
            {
                Logger.WriteLine(trigger.ToString());
            }

            return builder.ToString();
        }
    }
}