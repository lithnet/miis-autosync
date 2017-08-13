using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;
using System.Text;

namespace Lithnet.Miiserver.AutoSync
{
    public class MAExecutor
    {
        protected static ManualResetEvent GlobalStaggeredExecutionLock;
        protected static ManualResetEvent GlobalExclusiveOperationLock;
        protected static ManualResetEvent GlobalSynchronizationStepLock;

        protected static List<WaitHandle> AllMaLocalOperationLocks;

        public static event SyncCompleteEventHandler SyncComplete;
        public delegate void SyncCompleteEventHandler(object sender, SyncCompleteEventArgs e);
        private ManagementAgent ma;
        private BlockingCollection<ExecutionParameters> pendingActions;
        private ExecutionParameterCollection pendingActionList;

        public delegate void StateChangedEventHandler(object sender, MAStatusChangedEventArgs e);
        public event StateChangedEventHandler StateChanged;

        internal MAStatus InternalStatus;

        private ManualResetEvent localOperationLock;
        private ManualResetEvent serviceControlLock;
        private System.Timers.Timer importCheckTimer;
        private System.Timers.Timer unmanagedChangesCheckTimer;
        private TimeSpan importInterval;
        private CancellationTokenSource tokenSource;

        private Dictionary<string, string> perProfileLastRunStatus;

        public MAConfigParameters Configuration { get; private set; }

        internal void RaiseStateChange()
        {
            this.StateChanged?.Invoke(this, new MAStatusChangedEventArgs(this.InternalStatus, this.ma.Name));
        }

        public string ExecutingRunProfile
        {
            get => this.InternalStatus.ExecutingRunProfile;
            private set
            {
                if (this.InternalStatus.ExecutingRunProfile != value)
                {
                    this.InternalStatus.ExecutingRunProfile = value;
                    this.RaiseStateChange();
                }
            }
        }

        public string LastRunProfileResult
        {
            get => this.InternalStatus.LastRunProfileResult;
            private set
            {
                if (this.InternalStatus.LastRunProfileResult != value)
                {
                    this.InternalStatus.LastRunProfileResult = value;
                    this.RaiseStateChange();
                }
            }
        }

        public string LastRunProfileName
        {
            get => this.InternalStatus.LastRunProfileName;
            private set
            {
                if (this.InternalStatus.LastRunProfileName != value)
                {
                    this.InternalStatus.LastRunProfileName = value;
                    this.RaiseStateChange();
                }
            }
        }


        public string Message
        {
            get => this.InternalStatus.Message;
            private set
            {
                if (this.InternalStatus.Message != value)
                {
                    this.InternalStatus.Message = value;
                    this.RaiseStateChange();
                }
            }
        }

        public ExecutorState ControlState
        {
            get => this.InternalStatus.ControlState;
            private set
            {
                if (this.InternalStatus.ControlState != value)
                {
                    this.InternalStatus.ControlState = value;
                    this.RaiseStateChange();
                }
            }
        }

        public ExecutorState ExecutionState
        {
            get => this.InternalStatus.ExecutionState;
            private set
            {
                if (this.InternalStatus.ExecutionState != value)
                {
                    this.InternalStatus.ExecutionState = value;
                    this.RaiseStateChange();
                }
            }
        }

        private List<IMAExecutionTrigger> ExecutionTriggers { get; }

        private MAController controller;

        private CancellationToken token;

        private Task internalTask;

        static MAExecutor()
        {
            MAExecutor.GlobalSynchronizationStepLock = new ManualResetEvent(true);
            MAExecutor.GlobalStaggeredExecutionLock = new ManualResetEvent(true);
            MAExecutor.GlobalExclusiveOperationLock = new ManualResetEvent(true);
            MAExecutor.AllMaLocalOperationLocks = new List<WaitHandle>();
        }

        public MAExecutor(ManagementAgent ma)
        {
            this.ma = ma;
            this.InternalStatus = new MAStatus() { MAName = this.ma.Name };
            this.ControlState = ExecutorState.Stopped;
            this.pendingActionList = new ExecutionParameterCollection();
            this.pendingActions = new BlockingCollection<ExecutionParameters>(this.pendingActionList);
            this.perProfileLastRunStatus = new Dictionary<string, string>();
            this.ExecutionTriggers = new List<IMAExecutionTrigger>();
            this.localOperationLock = new ManualResetEvent(true);
            this.serviceControlLock = new ManualResetEvent(true);
            MAExecutor.AllMaLocalOperationLocks.Add(this.localOperationLock);
            MAExecutor.SyncComplete += this.MAExecutor_SyncComplete;
        }

        private void Setup(MAConfigParameters config)
        {
            if (!this.ma.Name.Equals(config.ManagementAgentName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Configuration was provided for the management agent {config.ManagementAgent.Name} for an executor configured for {this.ma.Name}");
            }

            this.Configuration = config;
            this.InternalStatus.ActiveVersion = config.Version;
            this.ControlState = config.Disabled ? ExecutorState.Disabled : ExecutorState.Stopped;
            this.controller = new MAController(config);
            this.AttachTrigger(config.Triggers?.ToArray());

            RunDetails lastrun = null;

            try
            {
                lastrun = this.ma?.GetLastRun();
            }
            catch
            {
            }

            this.InternalStatus.LastRunProfileResult = lastrun?.LastStepStatus;
            this.InternalStatus.LastRunProfileName = lastrun?.RunProfileName;
        }

        private void SetupUnmanagedChangesCheckTimer()
        {
            this.unmanagedChangesCheckTimer = new System.Timers.Timer();
            this.unmanagedChangesCheckTimer.Elapsed += this.UnmanagedChangesCheckTimer_Elapsed;
            this.unmanagedChangesCheckTimer.AutoReset = true;
            this.unmanagedChangesCheckTimer.Interval = Global.RandomizeOffset(RegistrySettings.UnmanagedChangesCheckInterval.TotalMilliseconds);
            this.unmanagedChangesCheckTimer.Start();
        }

        private void UnmanagedChangesCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.ControlState != ExecutorState.Running)
            {
                return;
            }

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
                    int importSeconds = this.Configuration.AutoImportIntervalMinutes > 0 ? this.Configuration.AutoImportIntervalMinutes * 60 : MAExecutionTriggerDiscovery.GetAverageImportIntervalMinutes(this.ma) * 60;
                    this.importInterval = new TimeSpan(0, 0, Global.RandomizeOffset(importSeconds));
                    this.importCheckTimer.Interval = this.importInterval.TotalMilliseconds;
                    this.importCheckTimer.AutoReset = true;
                    this.Log($"Starting import interval timer. Imports will be queued if they have not been run for {this.importInterval}");
                    this.importCheckTimer.Start();
                }
                else
                {
                    this.Log("Import schedule not enabled");
                }
            }
            else
            {
                this.Log("Import schedule disabled");
            }
        }

        private void ImportCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.ControlState != ExecutorState.Running)
            {
                return;
            }

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
                    this.Log($"Registering execution trigger '{t.DisplayName}'");
                    t.TriggerExecution += this.Notifier_TriggerExecution;
                    t.Start();
                }
                catch (Exception ex)
                {
                    this.Log($"Could not start execution trigger {t.DisplayName}");
                    Logger.WriteException(ex);
                }
            }
        }

        private void StopTriggers()
        {
            foreach (IMAExecutionTrigger t in this.ExecutionTriggers)
            {
                try
                {
                    this.Log($"Unregistering execution trigger '{t.DisplayName}'");
                    t.TriggerExecution -= this.Notifier_TriggerExecution;
                    t.Stop();
                }
                catch (Exception ex)
                {
                    this.Log($"Could not stop execution trigger {t.DisplayName}");
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
                if (d.HasUnconfirmedExports())
                {
                    this.Trace("Unconfirmed exports in last run");
                    this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.ConfirmingImportRunProfileName), d.RunProfileName, true);
                }
            }
        }

        private void QueueFollowUpActionsImport(RunDetails d)
        {
            if (d.HasStagedImports())
            {
                this.Trace("Staged imports in last run");
                this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.DeltaSyncRunProfileName), d.RunProfileName, true);
            }
        }

        private void QueueFollowUpActionsSync(RunDetails d)
        {
            SyncCompleteEventHandler registeredHandlers = MAExecutor.SyncComplete;

            if (registeredHandlers == null)
            {
                this.Trace("No sync event handlers were registered");
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

        private void Wait(TimeSpan duration, string name)
        {
            this.token.ThrowIfCancellationRequested();
            this.Trace($"SLEEP: {name}: {duration}");
            this.token.WaitHandle.WaitOne(duration);
            this.token.ThrowIfCancellationRequested();
        }

        private void Wait(ManualResetEvent mre, string name)
        {
            this.Trace($"LOCK: WAIT: {name}");
            WaitHandle.WaitAny(new[] { mre, this.token.WaitHandle });
            this.token.ThrowIfCancellationRequested();
            this.Trace($"LOCK: CLEARED: {name}");
        }

        private void WaitAndTakeLock(ManualResetEvent mre, string name)
        {
            this.Trace($"SYNCOBJECT: WAIT: {name}");

            lock (mre)
            {
                this.Trace($"SYNCOBJECT: LOCKED: {name}");

                this.Wait(mre, name);
                this.TakeLockUnsafe(mre, name);
            }

            this.Trace($"SYNCOBJECT: UNLOCKED: {name}");
        }

        private void Wait(WaitHandle[] waitHandles, string name)
        {
            this.Trace($"LOCK: WAIT: {name}");
            while (!WaitHandle.WaitAll(waitHandles, 1000))
            {
                this.token.ThrowIfCancellationRequested();
            }

            this.token.ThrowIfCancellationRequested();
            this.Trace($"LOCK: CLEARED: {name}");
        }

        private void TakeLockUnsafe(ManualResetEvent mre, string name)
        {
            this.Trace($"LOCK: TAKE: {name}");
            mre.Reset();
            this.token.ThrowIfCancellationRequested();
        }

        private void TakeLock(ManualResetEvent mre, string name)
        {
            this.Trace($"SYNCOBJECT: WAIT: {name}");

            lock (mre)
            {
                this.Trace($"SYNCOBJECT: LOCKED: {name}");

                this.TakeLockUnsafe(mre, name);
            }

            this.Trace($"SYNCOBJECT: UNLOCKED {name}");
        }

        private void ReleaseLock(ManualResetEvent mre, string name)
        {
            this.Trace($"LOCK: RELEASE: {name}");
            mre.Set();
        }

        private void Execute(ExecutionParameters e)
        {
            bool tookGlobalLock = false;

            try
            {
                this.token.ThrowIfCancellationRequested();
                this.UpdateExecutionStatus(ExecutorState.Waiting, "Waiting for exclusive operations to complete", e.RunProfileName);

                this.Wait(MAExecutor.GlobalExclusiveOperationLock, nameof(MAExecutor.GlobalExclusiveOperationLock));

                this.Message = "Asking controller for execution permission";

                if (!this.controller.ShouldExecute(e.RunProfileName))
                {
                    this.Log($"Controller indicated that run profile {e.RunProfileName} should not be executed");
                    return;
                }

                this.WaitOnUnmanagedRun();

                this.ExecutionState = ExecutorState.Waiting;
                this.ExecutingRunProfile = e.RunProfileName;
                this.token.ThrowIfCancellationRequested();

                if (e.Exclusive)
                {
                    this.Message = "Waiting for exclusive operation lock";
                    this.Log($"Entering exclusive mode for {e.RunProfileName}");

                    // Signal all executors to wait before running their next job
                    this.TakeLock(MAExecutor.GlobalExclusiveOperationLock, nameof(MAExecutor.GlobalExclusiveOperationLock));
                    tookGlobalLock = true;

                    this.Log("Waiting for all MAs to complete");
                    // Wait for all  MAs to finish their current job
                    this.Wait(MAExecutor.AllMaLocalOperationLocks.ToArray(), nameof(MAExecutor.AllMaLocalOperationLocks));
                }

                // If another operation in this executor is already running, then wait for it to finish before taking the lock for ourselves
                this.Message = "Waiting for lock on management agent";
                this.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock));

                // Grab the staggered execution lock, and hold for x seconds
                // This ensures that no MA can start within x seconds of another MA
                // to avoid deadlock conditions
                this.Message = "Waiting for MA start";
                bool tookStaggerLock = false;
                try
                {
                    this.WaitAndTakeLock(MAExecutor.GlobalStaggeredExecutionLock, nameof(MAExecutor.GlobalStaggeredExecutionLock));
                    tookStaggerLock = true;
                    this.Wait(RegistrySettings.ExecutionStaggerInterval, nameof(RegistrySettings.ExecutionStaggerInterval));
                }
                finally
                {
                    if (tookStaggerLock)
                    {
                        this.ReleaseLock(MAExecutor.GlobalStaggeredExecutionLock, nameof(MAExecutor.GlobalStaggeredExecutionLock));
                    }
                }

                if (this.ma.RunProfiles[e.RunProfileName].RunSteps.Any(t => t.IsImportStep))
                {
                    this.Trace("Import step detected. Resetting timer");
                    this.ResetImportTimerOnImport();
                }

                int count = 0;

                while (count <= RegistrySettings.RetryCount || RegistrySettings.RetryCount < 0)
                {
                    this.token.ThrowIfCancellationRequested();
                    string result;

                    try
                    {
                        count++;
                        this.UpdateExecutionStatus(ExecutorState.Running, "Executing");
                        this.Log($"Executing {e.RunProfileName}");
                        result = this.ma.ExecuteRunProfile(e.RunProfileName, this.token);
                        this.UpdateExecutionStatus(ExecutorState.Processing, "Evaluating run results");
                        this.Log($"{e.RunProfileName} returned {result}");
                    }
                    catch (MAExecutionException ex)
                    {
                        this.Log($"{e.RunProfileName} returned {ex.Result}");
                        result = ex.Result;
                    }

                    this.UpdateLastRunStatus(e.RunProfileName, result);

                    if (RegistrySettings.RetryCodes.Contains(result))
                    {
                        this.Trace($"Operation is retryable. {count} attempt{count.Pluralize()} made");

                        if (count > RegistrySettings.RetryCount && RegistrySettings.RetryCount >= 0)
                        {
                            this.Log($"Aborting run profile after {count} attempt{count.Pluralize()}");
                            break;
                        }

                        this.UpdateExecutionStatus(ExecutorState.Waiting, "Waiting to retry operation");

                        int interval = Global.RandomizeOffset(RegistrySettings.RetrySleepInterval.TotalMilliseconds * count);
                        this.Trace($"Sleeping thread for {interval}ms before retry");
                        this.Wait(TimeSpan.FromMilliseconds(interval), nameof(RegistrySettings.RetrySleepInterval));
                        this.Log("Retrying operation");
                    }
                    else
                    {
                        this.Trace($"Result code '{result}' was not listed as retryable");
                        break;
                    }
                }

                this.Wait(RegistrySettings.PostRunInterval, nameof(RegistrySettings.PostRunInterval));

                this.Trace("Getting run results");
                using (RunDetails r = this.ma.GetLastRun())
                {
                    this.Trace("Got run results");
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
                this.ProcessUnexpectedChangeException(ex);
            }
            catch (Exception ex)
            {
                this.Log($"Executor encountered an error executing run profile {this.ExecutingRunProfile}");
                Logger.WriteException(ex);
            }
            finally
            {
                this.UpdateExecutionStatus(ExecutorState.Idle, null, null);

                // Reset the local lock so the next operation can run
                this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));

                if (tookGlobalLock)
                {
                    // Reset the global lock so pending operations can run
                    this.ReleaseLock(MAExecutor.GlobalExclusiveOperationLock, nameof(MAExecutor.GlobalExclusiveOperationLock));
                }
            }
        }

        private void WaitOnUnmanagedRun()
        {
            if (this.ma.IsIdle())
            {
                return;
            }

            try
            {
                this.UpdateExecutionStatus(ExecutorState.Running, "Unmanaged run in progress", this.ma.ExecutingRunProfileName);

                this.Trace("Unmanaged run in progress");
                this.TakeLock(this.localOperationLock, nameof(this.localOperationLock));

                this.Log($"Waiting on unmanaged run {this.ma.ExecutingRunProfileName} to finish");

                if (this.ma.RunProfiles[this.ma.ExecutingRunProfileName].RunSteps.Any(t => t.IsSyncStep))
                {
                    this.Log("Getting exclusive sync lock for unmanaged run");
                    bool takenLock = false;

                    try
                    {
                        this.WaitAndTakeLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock));
                        takenLock = true;
                        this.ma.Wait(this.token);
                    }
                    finally
                    {
                        if (takenLock)
                        {
                            this.ReleaseLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock));
                        }
                    }
                }
                else
                {
                    this.ma.Wait(this.token);
                }

                this.UpdateExecutionStatus(ExecutorState.Processing, "Evaluating run results");
                this.token.ThrowIfCancellationRequested();

                using (RunDetails ur = this.ma.GetLastRun())
                {
                    this.UpdateLastRunStatus(ur.RunProfileName, ur.LastStepStatus);
                    this.PerformPostRunActions(ur);
                }
            }
            finally
            {
                this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));
                this.Trace("Unmanaged run complete");
                this.UpdateExecutionStatus(ExecutorState.Idle, null, null);
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

        public void Start(MAConfigParameters config)
        {
            if (config.IsNew || config.IsMissing || config.Disabled)
            {
                this.Log("Ignoring start request as management agent is disabled or unconfigured");
                this.ControlState = ExecutorState.Disabled;
                return;
            }

            if (this.ControlState != ExecutorState.Stopped)
            {
                throw new InvalidOperationException($"Cannot start an executor that is in the {this.ControlState} state");
            }
          
            try
            {
                this.tokenSource = new CancellationTokenSource();
                this.token = this.tokenSource.Token;

                this.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock));

                this.ControlState = ExecutorState.Starting;

                this.Setup(config);

                try
                {
                    Logger.StartThreadLog();
                    Logger.WriteSeparatorLine('-');

                    this.Log("Starting executor");

                    Logger.WriteRaw($"{this}\n");
                    Logger.WriteSeparatorLine('-');
                }
                finally
                {
                    Logger.EndThreadLog();
                }

                this.internalTask = new Task(() =>
                {
                    try
                    {
                        this.token.ThrowIfCancellationRequested();
                        this.Init();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        this.Log("The MAExecutor encountered a unrecoverable error");
                        Logger.WriteLine(ex.Message);
                        Logger.WriteLine(ex.StackTrace);
                    }
                }, this.token);

                this.internalTask.Start();

                this.ControlState = ExecutorState.Running;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred starting the executor");
                Logger.WriteException(ex);
                this.Stop();
                this.Message = $"Startup error: {ex.Message}";
            }
            finally
            {
                this.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock));
            }
        }

        public void Stop()
        {
            if (this.ControlState == ExecutorState.Stopped || this.ControlState == ExecutorState.Disabled || this.ControlState == ExecutorState.Stopping)
            {
                return;
            }

            try
            {
                this.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock));

                this.ControlState = ExecutorState.Stopping;

                this.Log("Stopping MAExecutor");

                this.tokenSource?.Cancel();
                this.importCheckTimer?.Stop();

                this.StopTriggers();
                this.ExecutionTriggers.Clear();

                this.Log("Stopped execution triggers");

                if (this.internalTask != null && !this.internalTask.IsCompleted)
                {
                    this.Log("Waiting for cancellation to complete");
                    if (this.internalTask.Wait(TimeSpan.FromSeconds(30)))
                    {
                        this.Log("Cancellation completed");
                    }
                    else
                    {
                        this.Log("MAExecutor internal task did not stop in the allowed time");
                    }
                }

                this.internalTask = null;

                this.ControlState = ExecutorState.Stopped;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred starting the executor");
                Logger.WriteException(ex);
                this.Message = $"Stop error: {ex.Message}";
            }
            finally
            {
                this.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock));
            }
        }
        
        private void UpdateExecutionQueueState()
        {
            string items = this.GetQueueItemNames(false);

            if (items != this.InternalStatus.ExecutionQueue)
            {
                this.InternalStatus.ExecutionQueue = items;
                this.RaiseStateChange();
            }
        }

        private void UpdateExecutionStatus(ExecutorState state, string message)
        {
            this.InternalStatus.ExecutionState = state;
            this.InternalStatus.Message = message;
            this.RaiseStateChange();
        }

        private void UpdateExecutionStatus(ExecutorState state, string message, string executingRunProfile)
        {
            this.InternalStatus.ExecutionState = state;
            this.InternalStatus.Message = message;
            this.InternalStatus.ExecutingRunProfile = executingRunProfile;
            this.RaiseStateChange();
        }

        private void UpdateExecutionStatus(ExecutorState state, string message, string executingRunProfile, string executionQueue)
        {
            this.InternalStatus.ExecutionState = state;
            this.InternalStatus.Message = message;
            this.InternalStatus.ExecutingRunProfile = executingRunProfile;
            this.InternalStatus.ExecutionQueue = executionQueue;
            this.RaiseStateChange();
        }

        private void UpdateLastRunStatus(string runProfileName, string result)
        {
            this.InternalStatus.LastRunProfileName = runProfileName;
            this.InternalStatus.LastRunProfileResult = result;
            this.RaiseStateChange();
        }

        private void Init()
        {
            if (!this.ma.IsIdle())
            {
                try
                {
                    this.UpdateExecutionStatus(ExecutorState.Running, "Unmanaged run in progress", this.ma.ExecutingRunProfileName);
                    this.Log("Waiting for sync engine to finish current run profile before initializing executor");
                    this.TakeLock(this.localOperationLock, nameof(this.localOperationLock));
                    this.ma.Wait(this.token);
                    this.ExecutionState = ExecutorState.Processing;
                    RunDetails r = this.ma.GetLastRun();
                    this.UpdateLastRunStatus(r.RunProfileName, r.LastStepStatus);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("An error occurred in an unmanaged run");
                    Logger.WriteException(ex);
                }
                finally
                {
                    this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));
                    this.UpdateExecutionStatus(ExecutorState.Idle, null, null);
                }
            }

            this.token.ThrowIfCancellationRequested();

            this.CheckAndQueueUnmanagedChanges();

            this.token.ThrowIfCancellationRequested();

            this.StartTriggers();

            this.SetupImportSchedule();

            this.SetupUnmanagedChangesCheckTimer();

            this.token.ThrowIfCancellationRequested();

            try
            {
                this.Log("Starting action processing queue");
                this.UpdateExecutionStatus(ExecutorState.Idle, null, null);

                // ReSharper disable once InconsistentlySynchronizedField
                foreach (ExecutionParameters action in this.pendingActions.GetConsumingEnumerable(this.token))
                {
                    this.token.ThrowIfCancellationRequested();

                    try
                    {
                        this.UpdateExecutionStatus(ExecutorState.Waiting, "Staging run", action.RunProfileName, this.GetQueueItemNames(false));

                        this.SetExclusiveMode(action);

                        if (this.IsSyncStepOrFimMADeltaImport(action.RunProfileName))
                        {
                            this.Message = "Waiting for synchronization lock";
                            bool tookLock = false;

                            try
                            {
                                this.WaitAndTakeLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock));
                                tookLock = true;
                                this.Execute(action);
                            }
                            finally
                            {
                                if (tookLock)
                                {
                                    this.ReleaseLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock));
                                }
                            }
                        }
                        else
                        {
                            this.Execute(action);
                        }
                    }
                    finally
                    {
                        this.UpdateExecutionStatus(ExecutorState.Idle, null, null);
                    }
                }
            }
            catch (OperationCanceledException)
            {
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
                this.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock));

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
                // Reset the local lock so the next operation can run
                this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));
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

        private void Notifier_TriggerExecution(object sender, ExecutionTriggerEventArgs e)
        {
            IMAExecutionTrigger trigger = null;

            try
            {
                trigger = (IMAExecutionTrigger)sender;

                if (string.IsNullOrWhiteSpace(e.Parameters.RunProfileName))
                {
                    if (e.Parameters.RunProfileType == MARunProfileType.None)
                    {
                        this.Log($"Received empty run profile from trigger {trigger.DisplayName}");
                        return;
                    }
                }

                this.AddPendingActionIfNotQueued(e.Parameters, trigger.DisplayName);
            }
            catch (Exception ex)
            {
                this.Log($"The was an unexpected error processing an incoming trigger from {trigger?.DisplayName}");
                Logger.WriteException(ex);
            }
        }

        private void AddPendingActionIfNotQueued(ExecutionParameters p, string source, bool runNext = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p.RunProfileName))
                {
                    if (p.RunProfileType == MARunProfileType.None)
                    {
                        this.Trace("Dropping pending action request as no run profile name or run profile type was specified");
                        return;
                    }

                    p.RunProfileName = this.Configuration.GetRunProfileName(p.RunProfileType);
                }

                if (this.pendingActions.ToArray().Contains(p))
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

                // Removing this as it may caused changes to go unseen. e.g an import is in progress, 
                // a snapshot is taken, but new items become available during the import of the snapshot

                //if (p.RunProfileName.Equals(this.ExecutingRunProfile, StringComparison.OrdinalIgnoreCase))
                //{
                //    this.Trace($"Ignoring queue request for {p.RunProfileName} as it is currently executing");
                //    return;
                //}

                this.Trace($"Got queue request for {p.RunProfileName}");

                if (runNext)
                {
                    this.pendingActions.Add(p, this.token);
                    this.pendingActionList.MoveToFront(p);
                    this.Log($"Added {p.RunProfileName} to the front of the execution queue (triggered by: {source})");
                }
                else
                {
                    this.pendingActions.Add(p, this.token);
                    this.Log($"Added {p.RunProfileName} to the execution queue (triggered by: {source})");
                }

                this.UpdateExecutionQueueState();

                this.Log($"Current queue: {this.GetQueueItemNames()}");
            }
            catch (Exception ex)
            {
                this.Log($"An unexpected error occurred while adding the pending action {p?.RunProfileName}. The event has been discarded");
                Logger.WriteException(ex);
            }
        }

        private string GetQueueItemNames(bool includeExecuting = true)
        {
            // ToArray is implemented by BlockingCollection and allows an approximate copy of the data to be made in 
            // the event an add or remove is in progress. Other functions such as ToList are generic and can cause
            // collection modified exceptions when enumerating the values

            string queuedNames = string.Join(",", this.pendingActions.ToArray().Select(t => t.RunProfileName));

            if (includeExecuting && this.ExecutingRunProfile != null)
            {
                return string.Join(",", this.ExecutingRunProfile + "*", queuedNames);
            }
            else
            {
                return queuedNames;
            }
        }

        private bool HasUnconfirmedExportsInLastRun()
        {
            return this.ma.GetLastRun()?.StepDetails?.FirstOrDefault()?.HasUnconfirmedExports() ?? false;
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
                this.Log("Send mail failed");
                Logger.WriteException(ex);
            }
        }

        private void SendMail(RunDetails r)
        {
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

            return Program.ActiveConfig.Settings.MailIgnoreReturnCodes == null || !Program.ActiveConfig.Settings.MailIgnoreReturnCodes.Contains(r.LastStepStatus, StringComparer.OrdinalIgnoreCase);
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
                builder.AppendLine(trigger.ToString());
            }

            return builder.ToString();
        }
    }
}