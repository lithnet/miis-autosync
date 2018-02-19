using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionController
    {
        private bool disposed;

        private static long waitingExclusiveOpID;

        private static ConcurrentDictionary<Guid, SemaphoreSlim> allMaLocalOperationLocks;

        private static event EventHandler<SyncCompleteEventArgs> SyncComplete;

        internal event EventHandler<RunProfileExecutionCompleteEventArgs> RunProfileExecutionComplete;

        internal event EventHandler<BeforeExecutionStartEventArgs> BeforeExecutionStartImport;

        private int lastRunNumber;

        private ControllerLogger logger;

        private MAControllerConfiguration config;

        private ExecutionQueue queue;

        private ManagementAgent ma;

        private RunDetailParser runDetailParser;

        private MAStatus state;

        private static SemaphoreSlim globalStaggeredExecutionLock;

        private static ManualResetEvent globalExclusiveOperationLock;

        private static SemaphoreSlim globalExclusiveOperationLockSemaphore;

        private static SemaphoreSlim globalExclusiveOperationRunningLock;

        private static SemaphoreSlim globalExclusiveOperationRunningLockSemaphore;

        private static SemaphoreSlim globalSynchronizationStepLock;

        private SemaphoreSlim localOperationLock;

        private CancellationTokenSource jobCancellationTokenSource;

        private CancellationTokenSource controllerCancellationTokenSource;

        private MAControllerScript controllerScript;

        private MAControllerPerfCounters counters;

        private string ManagementAgentName => this.ma?.Name;

        private Guid ManagementAgentID => this.ma?.ID ?? Guid.Empty;

        static ExecutionController()
        {
            globalSynchronizationStepLock = new SemaphoreSlim(1, 1);
            globalStaggeredExecutionLock = new SemaphoreSlim(1, 1);
            globalExclusiveOperationLockSemaphore = new SemaphoreSlim(1, 1);
            globalExclusiveOperationLock = new ManualResetEvent(true);
            globalExclusiveOperationRunningLockSemaphore = new SemaphoreSlim(1, 1);
            globalExclusiveOperationRunningLock = new SemaphoreSlim(1, 1);
            allMaLocalOperationLocks = new ConcurrentDictionary<Guid, SemaphoreSlim>();
        }

        public ExecutionController(ManagementAgent ma, MAStatus state, MAControllerConfiguration config, ControllerLogger logger, ExecutionQueue queue, MAControllerPerfCounters counters, CancellationTokenSource controllerCancellationTokenSource)
        {
            this.ma = ma;
            this.state = state;

            if (allMaLocalOperationLocks.ContainsKey(this.ma.ID))
            {
                this.localOperationLock = allMaLocalOperationLocks[this.ma.ID];
            }
            else
            {
                this.localOperationLock = new SemaphoreSlim(1, 1);
                allMaLocalOperationLocks.TryAdd(this.ma.ID, this.localOperationLock);
            }

            SyncComplete += this.ExecutionController_SyncComplete;
            this.controllerCancellationTokenSource = controllerCancellationTokenSource;
            this.config = config;
            this.controllerScript = new MAControllerScript(config);
            this.logger = logger;
            this.runDetailParser = new RunDetailParser(config, logger);
            this.queue = queue;
            this.counters = counters;
        }

        public bool TryCancelRun(bool ignoreException)
        {
            this.ThrowOnDisposed();

            try
            {
                if (this.ma != null && !this.ma.IsIdle())
                {
                    this.logger.LogInfo("Requesting sync engine to terminate run");

                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            this.ma.Stop();
                        }
                        catch (Exception)
                        {
                            if (!ignoreException)
                            {
                                throw;
                            }
                        }
                    });
                }
                else
                {
                    this.logger.LogInfo("Canceling current job");
                    this.jobCancellationTokenSource?.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Cannot cancel run");
                return false;
            }

            return true;
        }

        public void InvokeControllerScriptAndExecute(ExecutionParameters action)
        {
            this.ThrowOnDisposed();

            if (this.controllerScript.SupportsShouldExecute)
            {
                this.state.Message = "Asking controller script for execution permission";

                if (!this.controllerScript.ShouldExecute(action.RunProfileName))
                {
                    this.logger.LogWarn($"Controller script indicated that run profile {action.RunProfileName} should not be executed");
                    return;
                }
            }

            this.TakeLocksAndExecute(action);
        }

        private void TakeLocksAndExecute(ExecutionParameters action)
        {
            ConcurrentBag<SemaphoreSlim> otherLocks = null;
            bool hasLocalLock = false;
            bool hasGlobalRunningLock = false;
            Stopwatch totalWaitTimer = Stopwatch.StartNew();

            try
            {
                this.WaitOnUnmanagedRun();
                this.state.ExecutionState = ControllerState.Waiting;
                this.jobCancellationTokenSource = this.CreateCancellationTokenForJob();

                if (action.Exclusive)
                {
                    this.TakeExclusiveLock();
                }
                else
                {
                    this.WaitForExclusiveRunLockHolder();

                    if (RegistrySettings.LockMode == 1)
                    {
                        this.YieldToOlderQueuedOperations(action);
                    }
                }

                if (action.Exclusive)
                {
                    if (RegistrySettings.LockMode > 0)
                    {
                        hasGlobalRunningLock = this.YieldAndTakeExclusiveRunLock();
                    }
                    else
                    {
                        hasGlobalRunningLock = this.TakeExclusiveRunLock();
                    }

                    this.WaitOnOtherMAs();
                }

                if (ExecutionController.StepRequiresSyncLock(this.ma, action.RunProfileName))
                {
                    if (!action.Exclusive)
                    {
                        this.TakeSyncLock();
                    }
                }

                otherLocks = this.GetForeignLocks();

                if (action.Exclusive)
                {
                    hasLocalLock = this.TakeLocalLock();
                }
                else
                {
                    hasLocalLock = this.TakeLocalLockWithSemaphore();
                }

                this.StaggerStart();
                this.logger.LogInfo($"Locks obtained in {totalWaitTimer.Elapsed:hh\\:mm\\:ss}");
                this.counters.AddWaitTimeTotal(totalWaitTimer.Elapsed);

                this.Execute(action, this.jobCancellationTokenSource);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.state.UpdateExecutionStatus(ControllerState.Idle, null, null);

                if (hasLocalLock)
                {
                    LockController.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock), this.ManagementAgentName);
                }

                if (otherLocks?.Any() ?? false)
                {
                    foreach (SemaphoreSlim e in otherLocks)
                    {
                        LockController.ReleaseLock(e, "foreign localOperationLock", this.ManagementAgentName);
                    }

                    this.state.HasForeignLock = false;
                }

                if (this.state.HasSyncLock)
                {
                    LockController.ReleaseLock(globalSynchronizationStepLock, nameof(globalSynchronizationStepLock), this.ManagementAgentName);
                    this.state.HasSyncLock = false;
                }
                
                if (hasGlobalRunningLock)
                {
                    LockController.ReleaseLock(globalExclusiveOperationRunningLock, nameof(globalExclusiveOperationRunningLock), this.ManagementAgentName);
                }

                if (this.state.HasExclusiveLock)
                {
                    // Reset the global lock so pending operations can run
                    waitingExclusiveOpID = 0;
                    LockController.ReleaseLock(globalExclusiveOperationLock, nameof(globalExclusiveOperationLock), this.ManagementAgentName);
                    this.state.HasExclusiveLock = false;
                }
            }
        }

        private void WaitForExclusiveLockHolder()
        {
            this.state.Message = "Waiting for x-lock holder to finish";
            LockController.Wait(ExecutionController.globalExclusiveOperationLock, nameof(ExecutionController.globalExclusiveOperationLock), this.jobCancellationTokenSource, this.ManagementAgentName);
        }

        private void YieldToOlderQueuedOperations(ExecutionParameters action)
        {
            if (ExecutionController.waitingExclusiveOpID != 0 && action.QueueID > ExecutionController.waitingExclusiveOpID)
            {
                this.state.Message = "Yielding to x-lock holder";
                LockController.Wait(ExecutionController.globalExclusiveOperationLock, nameof(ExecutionController.globalExclusiveOperationLock), this.jobCancellationTokenSource, this.ManagementAgentName);
            }
        }

        private void WaitForExclusiveRunLockHolder()
        {
            this.state.Message = "Waiting for xr-lock holder to finish";
            LockController.Wait(ExecutionController.globalExclusiveOperationRunningLock.AvailableWaitHandle, nameof(ExecutionController.globalExclusiveOperationRunningLock), this.jobCancellationTokenSource, this.ManagementAgentName);
        }

        private void TakeExclusiveLock()
        {
            this.state.Message = "Waiting to take x-lock";
            LockController.WaitAndTakeLockWithSemaphore(ExecutionController.globalExclusiveOperationLock, ExecutionController.globalExclusiveOperationLockSemaphore, nameof(ExecutionController.globalExclusiveOperationLock), this.jobCancellationTokenSource, this.ManagementAgentName);
            this.state.HasExclusiveLock = true;
        }

        private bool YieldAndTakeExclusiveRunLock()
        {
            ExecutionController.waitingExclusiveOpID = Interlocked.Read(ref ExecutionQueue.CurrentQueueID);

            this.state.Message = "Yielding to non-exclusive jobs";
            if (RegistrySettings.LockMode != 2)
            {
                this.logger.LogInfo($"Yielding to non-exclusive jobs. Will allow executions up to queue ID {ExecutionController.waitingExclusiveOpID}");
            }
            else
            {
                this.logger.LogInfo("Yielding to non-exclusive jobs");
            }

            LockController.Wait(this.GetLocalOperationLockArray(), nameof(ExecutionController.allMaLocalOperationLocks), this.jobCancellationTokenSource, this.ManagementAgentName);

            return this.TakeExclusiveRunLock();
        }

        private bool TakeExclusiveRunLock()
        {
            this.state.Message = "Waiting to take xr-lock";
            LockController.WaitAndTakeLockWithSemaphore(ExecutionController.globalExclusiveOperationRunningLock, ExecutionController.globalExclusiveOperationRunningLockSemaphore, nameof(ExecutionController.globalExclusiveOperationRunningLock), this.jobCancellationTokenSource, this.ManagementAgentName);
            return true;
        }

        private bool TakeLocalLock()
        {
            this.state.Message = "Waiting to take l-lock";
            LockController.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), this.jobCancellationTokenSource, this.ManagementAgentName);
            return true;
        }

        private bool TakeLocalLockWithSemaphore()
        {
            this.state.Message = "Waiting to take l-lock";
            LockController.WaitAndTakeLockWithSemaphore(this.localOperationLock, ExecutionController.globalExclusiveOperationRunningLock, nameof(this.localOperationLock), this.jobCancellationTokenSource, this.ManagementAgentName);
            return true;
        }

        private void StaggerStart()
        {
            this.state.Message = "Preparing to start management agent";

            bool tookStaggerLock = false;
            try
            {
                LockController.WaitAndTakeLock(ExecutionController.globalStaggeredExecutionLock, nameof(ExecutionController.globalStaggeredExecutionLock), this.jobCancellationTokenSource, this.ManagementAgentName);
                tookStaggerLock = true;
                LockController.Wait(RegistrySettings.ExecutionStaggerInterval, nameof(RegistrySettings.ExecutionStaggerInterval), this.jobCancellationTokenSource, this.ManagementAgentName);
            }
            finally
            {
                if (tookStaggerLock)
                {
                    LockController.ReleaseLock(ExecutionController.globalStaggeredExecutionLock, nameof(ExecutionController.globalStaggeredExecutionLock), this.ManagementAgentName);
                }
            }
        }

        private void WaitOnOtherMAs()
        {
            this.state.Message = "Waiting for other MAs to finish";
            this.logger.LogInfo("Waiting for all MAs to complete");
            // Wait for all  MAs to finish their current job
            LockController.Wait(this.GetLocalOperationLockArray(), nameof(ExecutionController.allMaLocalOperationLocks), this.jobCancellationTokenSource, this.ManagementAgentName);
        }

        private void TakeSyncLock()
        {
            this.state.Message = "Waiting to take s-lock";
            LockController.WaitAndTakeLock(ExecutionController.globalSynchronizationStepLock, nameof(ExecutionController.globalSynchronizationStepLock), this.jobCancellationTokenSource, this.ManagementAgentName);
            this.state.HasSyncLock = true;
        }

        private WaitHandle[] GetLocalOperationLockArray(bool includeLocal = false)
        {
            return ExecutionController.allMaLocalOperationLocks.Values.Where(t => includeLocal || t != this.localOperationLock).Select(t => t.AvailableWaitHandle).ToArray();
        }

        private ConcurrentBag<SemaphoreSlim> GetForeignLocks()
        {
            ConcurrentBag<SemaphoreSlim> otherLocks = new ConcurrentBag<SemaphoreSlim>();

            if (this.config.LockManagementAgents != null)
            {
                List<Task> tasks = new List<Task>();

                foreach (string managementAgent in this.config.LockManagementAgents)
                {
                    Guid? id = Global.FindManagementAgent(managementAgent, Guid.Empty);

                    if (id == null)
                    {
                        this.logger.LogInfo($"Cannot take lock for management agent {managementAgent} as the management agent cannot be found");
                        continue;
                    }

                    if (id == this.ManagementAgentID)
                    {
                        this.logger.Trace("Not going to wait on own lock!");
                        continue;
                    }

                    tasks.Add(Task.Run(() =>
                    {
                        Thread.CurrentThread.SetThreadName($"Get localOperationLock on {managementAgent} for {this.ManagementAgentName}");
                        SemaphoreSlim h = allMaLocalOperationLocks[id.Value];
                        LockController.WaitAndTakeLock(h, $"localOperationLock for {managementAgent}", this.jobCancellationTokenSource, this.ManagementAgentName);
                        otherLocks.Add(h);
                        this.state.HasForeignLock = true;
                    }, this.jobCancellationTokenSource.Token));
                }

                if (tasks.Any())
                {
                    this.state.Message = "Waiting to take locks";
                    Task.WaitAll(tasks.ToArray(), this.jobCancellationTokenSource.Token);
                }
            }
            
            return otherLocks;
        }

        private void QueueFollowupActions(RunDetails d)
        {
            this.logger.Trace($"Analyzing results from {d.RunProfileName} run #{d.RunNumber}");

            for (int index = d.StepDetails.Count - 1; index >= 0; index--)
            {
                StepDetails s = d.StepDetails[index];

                if (s.StepDefinition == null)
                {
                    this.logger.LogWarn($"Step detail for step {s.StepNumber} was missing the step definition and cannot be processed");
                    continue;
                }

                if (s.StepDefinition.IsSyncStep)
                {
                    this.logger.Trace($"Processing outbound changes from step {s.StepNumber} of {d.RunProfileName}");
                    this.QueueFollowUpActionsSync(s);
                    continue;
                }

                Tuple<PartitionConfiguration, MARunProfileType> requiredAction = this.runDetailParser.GetImportExportFollowUpActions(s);

                if (requiredAction == null)
                {
                    // nothing to do
                    this.logger.Trace($"Step {s.StepNumber} of {d.RunProfileName} had no follow up actions to perform");
                    continue;
                }

                if (this.runDetailParser.WasFollowupAlreadyPerformed(d, index, requiredAction))
                {
                    this.logger.Trace($"The expected follow up action '{requiredAction.Item2}' in partition '{requiredAction.Item1.Name}' for step {s.StepNumber} of run profile '{d.RunProfileName}' has already been performed");
                }
                else
                {
                    this.logger.Trace($"The expected follow up action '{requiredAction.Item2}' in partition '{requiredAction.Item1.Name}' for step {s.StepNumber} of run profile '{d.RunProfileName}' has not yet been performed");

                    if (requiredAction.Item2 == MARunProfileType.DeltaImport)
                    {
                        if (requiredAction.Item1.ConfirmingImportRunProfileName == null)
                        {
                            this.logger.LogWarn($"A confirming import was required, but they have not been configured for partition {requiredAction.Item1.Name}");
                            continue;
                        }

                        this.queue.Add(new ExecutionParameters(requiredAction.Item1.ConfirmingImportRunProfileName, false, true), d.RunProfileName);
                        continue;
                    }

                    if (requiredAction.Item2 == MARunProfileType.DeltaSync)
                    {
                        if (requiredAction.Item1.DeltaSyncRunProfileName == null)
                        {
                            this.logger.LogWarn($"A delta sync was required, but they have not been configured for partition {requiredAction.Item1.Name}");
                            continue;
                        }

                        this.queue.Add(new ExecutionParameters(requiredAction.Item1.DeltaSyncRunProfileName, false, true), d.RunProfileName);
                    }
                }
            }
        }

        private void QueueFollowUpActionsSync(StepDetails s)
        {
            if (!s.StepDefinition.IsSyncStep)
            {
                return;
            }

            EventHandler<SyncCompleteEventArgs> registeredHandlers = SyncComplete;

            if (registeredHandlers == null)
            {
                this.logger.Trace("No sync event handlers were registered");
                return;
            }

            foreach (OutboundFlowCounters item in s.OutboundFlowCounters)
            {
                if (!item.HasChanges)
                {
                    this.logger.Trace($"No outbound changes detected for {item.ManagementAgent}");
                    continue;
                }

                SyncCompleteEventArgs args = new SyncCompleteEventArgs
                {
                    SendingMAName = this.ma.Name,
                    TargetMA = item.MAID
                };

                this.logger.Trace($"Sending outbound change notification for MA {item.ManagementAgent}");
                registeredHandlers(this, args);
            }
        }

        private void RaiseRunProfileComplete(string runProfileName, string lastStepStatus, int runNumber, DateTime? startTime, DateTime? endTime)
        {
            Task.Run(() =>
            {
                try
                {
                    this.RunProfileExecutionComplete?.Invoke(this, new RunProfileExecutionCompleteEventArgs(this.ma.Name, this.ma.ID, runProfileName, lastStepStatus, runNumber, startTime, endTime));
                }
                catch (Exception ex)
                {
                    this.logger.LogWarn(ex, "Unable to relay run profile complete notification");
                }
            }, this.controllerCancellationTokenSource.Token);
        }

        private void Execute(ExecutionParameters e, CancellationTokenSource ts)
        {
            try
            {
                ts.Token.ThrowIfCancellationRequested();

                this.state.ExecutionState = ControllerState.Waiting;
                this.state.ExecutingRunProfile = e.RunProfileName;
                ts.Token.ThrowIfCancellationRequested();

                foreach (RunStep s in this.ma.RunProfiles[e.RunProfileName].RunSteps)
                {
                    if (s.IsImportStep)
                    {
                        this.BeforeExecutionStartImport?.Invoke(this, new BeforeExecutionStartEventArgs(e.RunProfileName, s.Partition));
                    }
                }

                int count = 0;
                RunDetails r = null;

                while (count <= RegistrySettings.RetryCount || RegistrySettings.RetryCount < 0)
                {
                    ts.Token.ThrowIfCancellationRequested();
                    string result = null;
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    try
                    {
                        count++;
                        this.queue.StagedRun = null;

                        this.state.UpdateExecutionStatus(ControllerState.Running, "Executing");
                        this.logger.LogInfo($"Executing {e.RunProfileName}");

                        try
                        {
                            this.counters.RunCount.Increment();
                            result = this.ma.ExecuteRunProfile(e.RunProfileName, ts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            result = result ?? "canceled";
                        }
                    }
                    catch (MAExecutionException ex)
                    {
                        result = ex.Result;
                    }
                    finally
                    {
                        this.counters.AddExecutionTime(stopwatch.Elapsed);
                        this.logger.LogInfo($"{e.RunProfileName} returned {result} (duration {stopwatch.Elapsed:hh\\:mm\\:ss})");
                        this.state.UpdateExecutionStatus(ControllerState.Processing, "Evaluating run results");
                    }

                    if (ts.IsCancellationRequested)
                    {
                        this.logger.LogInfo($"The run profile {e.RunProfileName} was canceled");
                        return;
                    }

                    LockController.Wait(RegistrySettings.PostRunInterval, nameof(RegistrySettings.PostRunInterval), ts, this.ma.Name);

                    this.logger.Trace("Getting run results");
                    r = this.ma.GetLastRun();
                    this.lastRunNumber = r.RunNumber;

                    this.logger.Trace("Got run results");

                    this.RaiseRunProfileComplete(r.RunProfileName, r.LastStepStatus, r.RunNumber, r.StartTime, r.EndTime);

                    if (RegistrySettings.RetryCodes.Contains(result))
                    {
                        this.logger.Trace($"Operation is retryable. {count} attempt{count.Pluralize()} made");

                        if (count > RegistrySettings.RetryCount && RegistrySettings.RetryCount >= 0)
                        {
                            this.logger.LogInfo($"Aborting run profile after {count} attempt{count.Pluralize()}");
                            break;
                        }

                        this.state.UpdateExecutionStatus(ControllerState.Waiting, "Waiting to retry operation");

                        int interval = Global.RandomizeOffset(RegistrySettings.RetrySleepInterval.TotalMilliseconds * count);
                        this.logger.Trace($"Sleeping thread for {interval}ms before retry");
                        LockController.Wait(TimeSpan.FromMilliseconds(interval), nameof(RegistrySettings.RetrySleepInterval), ts, this.ma.Name);
                        this.logger.LogInfo("Retrying operation");
                    }
                    else
                    {
                        this.logger.Trace($"Result code '{result}' was not listed as retryable");
                        break;
                    }
                }

                if (r != null)
                {
                    this.PerformPostRunActions(r);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (System.Management.Automation.RuntimeException ex)
            {
                if (ex.InnerException is UnexpectedChangeException changeException)
                {
                    throw changeException;
                }
                else
                {
                    this.logger.LogError(ex, $"Controller encountered an error executing run profile {this.state.ExecutingRunProfile}");
                }
            }
            catch (ThresholdExceededException)
            {
                throw;
            }
            catch (UnexpectedChangeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Controller encountered an error executing run profile {this.state.ExecutingRunProfile}");
            }
            finally
            {
                this.queue.StagedRun = null;
                this.state.UpdateExecutionStatus(ControllerState.Idle, null, null);
            }
        }

        private CancellationTokenSource CreateCancellationTokenForJob()
        {
            return CancellationTokenSource.CreateLinkedTokenSource(this.controllerCancellationTokenSource.Token);
        }

        public void WaitOnUnmanagedRun()
        {
            this.ThrowOnDisposed();

            if (this.ma.IsIdle())
            {
                return;
            }

            bool hasLocalLock = false;
            bool hasRunLock = false;
            try
            {
                string erp = this.ma.ExecutingRunProfileName;

                if (erp == null)
                {
                    return;
                }

                this.state.UpdateExecutionStatus(ControllerState.Running, "Unmanaged run in progress", erp);
                CancellationTokenSource linkedToken = this.CreateCancellationTokenForJob();

                this.logger.Trace("Unmanaged run in progress");
                LockController.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), linkedToken, this.ma.Name);
                hasLocalLock = true;

                this.logger.LogInfo($"Waiting on unmanaged run {erp} to finish");

                if (this.ma.RunProfiles[erp].RunSteps.Any(t => t.IsSyncStep))
                {
                    this.logger.Trace("Getting sync lock for unmanaged run");

                    try
                    {
                        if (Program.ActiveConfig.Settings.RunMode == RunMode.Exclusive || Program.ActiveConfig.Settings.RunMode == RunMode.Supported)
                        {
                            LockController.WaitAndTakeLockWithSemaphore(globalExclusiveOperationLock, globalExclusiveOperationLockSemaphore, nameof(globalExclusiveOperationLock), linkedToken, this.ManagementAgentName);
                            this.state.HasExclusiveLock = true;

                            LockController.WaitAndTakeLockWithSemaphore(globalExclusiveOperationRunningLock, globalExclusiveOperationRunningLockSemaphore, nameof(globalExclusiveOperationLock), linkedToken, this.ManagementAgentName);
                            hasRunLock = true;
                        }

                        LockController.WaitAndTakeLock(globalSynchronizationStepLock, nameof(globalSynchronizationStepLock), linkedToken, this.ma.Name);
                        this.state.HasSyncLock = true;
                        this.ma.Wait(linkedToken.Token);
                    }
                    finally
                    {
                        if (this.state.HasSyncLock)
                        {
                            LockController.ReleaseLock(globalSynchronizationStepLock, nameof(globalSynchronizationStepLock), this.ma.Name);
                            this.state.HasSyncLock = false;
                        }

                        if (this.state.HasExclusiveLock)
                        {
                            LockController.ReleaseLock(globalExclusiveOperationLock, nameof(globalExclusiveOperationLock), this.ma.Name);
                            this.state.HasExclusiveLock = false;
                        }

                        if (hasRunLock)
                        {
                            LockController.ReleaseLock(globalExclusiveOperationRunningLock, nameof(globalExclusiveOperationRunningLock), this.ma.Name);
                        }
                    }
                }
                else
                {
                    this.ma.Wait(linkedToken.Token);
                }

                this.state.UpdateExecutionStatus(ControllerState.Processing, "Evaluating run results");
                linkedToken.Token.ThrowIfCancellationRequested();

                using (RunDetails ur = this.ma.GetLastRun())
                {
                    this.RaiseRunProfileComplete(ur.RunProfileName, ur.LastStepStatus, ur.RunNumber, ur.StartTime, ur.EndTime);
                    this.PerformPostRunActions(ur);
                }
            }
            finally
            {
                if (hasLocalLock)
                {
                    LockController.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock), this.ma.Name);
                }

                this.logger.Trace("Unmanaged run complete");
                this.state.UpdateExecutionStatus(ControllerState.Idle, null, null);
            }
        }

        private void PerformPostRunActions(RunDetails r)
        {
            this.lastRunNumber = r.RunNumber;

            this.controllerScript.ExecutionComplete(r);

            this.runDetailParser.ThrowOnThresholdsExceeded(r);

            this.runDetailParser.TrySendMail(r);

            this.QueueFollowupActions(r);
        }

        private IEnumerable<PartitionConfiguration> GetPartitionsRequiringSync()
        {
            return this.GetPartitionsRequiringImportOrExport(this.ma.GetPendingImportCSObjectIDAndPartitions, "sync");
        }

        private IEnumerable<PartitionConfiguration> GetPartitionsRequiringExport()
        {
            return this.GetPartitionsRequiringImportOrExport(this.ma.GetPendingExportCSObjectIDAndPartitions, "export");
        }

        private IEnumerable<PartitionConfiguration> GetPartitionsRequiringImportOrExport(Func<CSObjectEnumerator> enumerator, string mode)
        {
            List<PartitionConfiguration> activePartitions = this.config.Partitions.ActiveConfigurations.ToList();
            Stopwatch watch = Stopwatch.StartNew();

            int activePartitionCount = activePartitions.Count;

            HashSet<Guid> partitionsDetected = new HashSet<Guid>();
            int objectCount = 0;

            foreach (CSObject cs in enumerator.Invoke())
            {
                objectCount++;

                if (this.config.DetectionMode == PartitionDetectionMode.AssumeAll)
                {
                    return activePartitions;
                }

                if (cs.PartitionGuid == null)
                {
                    this.logger.LogWarn($"CSObject did not have a partition ID {cs.DN}");
                    continue;
                }

                if (partitionsDetected.Add(cs.PartitionGuid.Value))
                {
                    this.logger.Trace($"Found partition requiring {mode} on {cs.PartitionGuid}");

                    if (partitionsDetected.Count >= activePartitionCount)
                    {
                        this.logger.Trace($"All active partitions require {mode}");
                        break;
                    }
                }
            }

            watch.Stop();

            this.logger.Trace($"Iterated through {objectCount} objects to find {partitionsDetected.Count} partitions needing {mode} in {watch.Elapsed}");

            return activePartitions.Where(t => partitionsDetected.Contains(t.ID));
        }

        private void ExecutionController_SyncComplete(object sender, SyncCompleteEventArgs e)
        {
            if (e.TargetMA != this.ma.ID)
            {
                return;
            }

            if (this.state.ControlState != ControlState.Running)
            {
                return;
            }

            this.logger.Trace($"Got sync complete message from {e.SendingMAName}");

            foreach (PartitionConfiguration c in this.GetPartitionsRequiringExport())
            {
                if (c.ExportRunProfileName != null)
                {
                    ExecutionParameters p = new ExecutionParameters(c.ExportRunProfileName);
                    this.queue.Add(p, "Synchronization on " + e.SendingMAName);
                }
            }
        }

        public void CheckAndQueueUnmanagedChanges()
        {
            this.ThrowOnDisposed();

            bool hasLocalLock = false;

            try
            {
                // If another operation in this controller is already running, then wait for it to finish
                LockController.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), this.controllerCancellationTokenSource, this.ManagementAgentName);
                hasLocalLock = true;

                bool hasRun = this.lastRunNumber > 0;
                int lastKnownRun = this.lastRunNumber;

                RunDetails run = this.ma.GetLastRun();
                this.lastRunNumber = run?.RunNumber ?? 0;

                this.logger.Trace("Checking for unmanaged changes");

                if (hasRun && run != null)
                {
                    if (lastKnownRun == this.lastRunNumber)
                    {
                        return;
                    }

                    this.logger.Trace($"Unprocessed changes detected. Last recorded run: {lastKnownRun}. Last run in sync engine: {run.RunNumber}");

                    this.PerformPostRunActions(run);
                }
                else
                {
                    this.CheckAndQueueUnmanagedChanges(run);
                }
            }
            finally
            {
                if (hasLocalLock)
                {
                    // Reset the local lock so the next operation can run
                    LockController.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock), this.ManagementAgentName);
                }
            }
        }

        private void CheckAndQueueUnmanagedChanges(RunDetails run)
        {
            foreach (PartitionConfiguration c in this.GetPartitionsRequiringExport())
            {
                if (c.ExportRunProfileName != null)
                {
                    ExecutionParameters p = new ExecutionParameters(c.ExportRunProfileName);
                    this.queue.Add(p, "Pending export check");
                }
            }

            if (run?.StepDetails != null)
            {
                foreach (StepDetails step in run.StepDetails)
                {
                    if (step.HasUnconfirmedExports())
                    {
                        PartitionConfiguration c = this.config.Partitions.GetActiveItemOrNull(step.StepDefinition.Partition);

                        if (c != null)
                        {
                            this.queue.Add(new ExecutionParameters(c.ConfirmingImportRunProfileName), "Unconfirmed export check");
                        }
                    }
                }
            }

            foreach (PartitionConfiguration c in this.GetPartitionsRequiringSync())
            {
                if (c.ExportRunProfileName != null)
                {
                    ExecutionParameters p = new ExecutionParameters(c.DeltaSyncRunProfileName);
                    this.queue.Add(p, "Staged import check");
                }
            }
        }

        private static bool StepRequiresSyncLock(ManagementAgent ma, string runProfileName)
        {
            if (ma.IsSyncStep(runProfileName))
            {
                return true;
            }

            if (ma.Category == "FIM")
            {
                if (ma.RunProfiles[runProfileName].RunSteps.Any(t => t.Type == RunStepType.DeltaImport))
                {
                    return true;
                }

                if (ma.RunProfiles[runProfileName].RunSteps.Any(t => t.Type == RunStepType.Export))
                {
                    if (RegistrySettings.GetSyncLockForFimMAExport)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            SyncComplete -= this.ExecutionController_SyncComplete;

            // Causes a waiting controller to fault
            //allMaLocalOperationLocks.TryRemove(this.ma.ID, out SemaphoreSlim value);

            //this.localOperationLock?.Dispose();
        }

        private void ThrowOnDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.ManagementAgentName);
            }
        }
    }
}
