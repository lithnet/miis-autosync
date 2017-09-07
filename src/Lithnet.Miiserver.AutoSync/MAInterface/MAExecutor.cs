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
        private static readonly int spinInterval = 250;
        protected static ManualResetEvent GlobalStaggeredExecutionLock;
        protected static ManualResetEvent GlobalExclusiveOperationLock;
        protected static ManualResetEvent GlobalSynchronizationStepLock;
        protected static ConcurrentDictionary<Guid, WaitHandle> AllMaLocalOperationLocks;

        public delegate void SyncCompleteEventHandler(object sender, SyncCompleteEventArgs e);
        public static event SyncCompleteEventHandler SyncComplete;

        public delegate void RunProfileExecutionCompleteEventHandler(object sender, RunProfileExecutionCompleteEventArgs e);
        public event RunProfileExecutionCompleteEventHandler RunProfileExecutionComplete;

        public delegate void StateChangedEventHandler(object sender, MAStatusChangedEventArgs e);
        public event StateChangedEventHandler StateChanged;

        private ManualResetEvent localOperationLock;
        private ManualResetEvent serviceControlLock;
        private System.Timers.Timer importCheckTimer;
        private System.Timers.Timer unmanagedChangesCheckTimer;
        private TimeSpan importInterval;
        private CancellationTokenSource executorCancellationTokenSource;
        private CancellationTokenSource jobCancellationTokenSource;
        private Dictionary<string, string> perProfileLastRunStatus;
        private ManagementAgent ma;
        private BlockingCollection<ExecutionParameters> pendingActions;
        private ExecutionParameterCollection pendingActionList;
        private MAController controller;
        private Task internalTask;

        internal MAStatus InternalStatus;

        private List<IMAExecutionTrigger> ExecutionTriggers { get; }

        public MAConfigParameters Configuration { get; private set; }

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

        public string ManagementAgentName
        {
            get => this.ma?.Name;
        }

        public Guid ManagementAgentID
        {
            get => this.ma?.ID ?? Guid.Empty;
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

        public string Detail
        {
            get => this.InternalStatus.Detail;
            private set
            {
                if (this.InternalStatus.Detail != value)
                {
                    this.InternalStatus.Detail = value;
                    this.RaiseStateChange();
                }
            }
        }

        public bool HasSyncLock
        {
            get => this.InternalStatus.HasSyncLock;
            private set
            {
                if (this.InternalStatus.HasSyncLock != value)
                {
                    this.InternalStatus.HasSyncLock = value;
                    this.RaiseStateChange();
                }
            }
        }

        public bool HasExclusiveLock
        {
            get => this.InternalStatus.HasExclusiveLock;
            private set
            {
                if (this.InternalStatus.HasExclusiveLock != value)
                {
                    this.InternalStatus.HasExclusiveLock = value;
                    this.RaiseStateChange();
                }
            }
        }

        public ControlState ControlState
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

        static MAExecutor()
        {
            MAExecutor.GlobalSynchronizationStepLock = new ManualResetEvent(true);
            MAExecutor.GlobalStaggeredExecutionLock = new ManualResetEvent(true);
            MAExecutor.GlobalExclusiveOperationLock = new ManualResetEvent(true);
            MAExecutor.AllMaLocalOperationLocks = new ConcurrentDictionary<Guid, WaitHandle>();
        }

        public MAExecutor(ManagementAgent ma)
        {
            this.ma = ma;
            this.InternalStatus = new MAStatus() { MAName = this.ma.Name };
            this.ControlState = ControlState.Stopped;

            this.ExecutionTriggers = new List<IMAExecutionTrigger>();
            this.localOperationLock = new ManualResetEvent(true);
            this.serviceControlLock = new ManualResetEvent(true);
            MAExecutor.AllMaLocalOperationLocks.TryAdd(this.ma.ID, this.localOperationLock);
            MAExecutor.SyncComplete += this.MAExecutor_SyncComplete;
        }
  
        internal void RaiseStateChange()
        {
            this.StateChanged?.Invoke(this, new MAStatusChangedEventArgs(this.InternalStatus, this.ma.Name));
        }

        private void Setup(MAConfigParameters config)
        {
            if (!this.ma.Name.Equals(config.ManagementAgentName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Configuration was provided for the management agent {config.ManagementAgent.Name} for an executor configured for {this.ma.Name}");
            }

            this.Configuration = config;
            this.InternalStatus.ActiveVersion = config.Version;
            this.ControlState = config.Disabled ? ControlState.Disabled : ControlState.Stopped;
            this.controller = new MAController(config);
            this.AttachTrigger(config.Triggers?.ToArray());
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
            if (this.ControlState != ControlState.Running)
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
            if (this.ControlState != ControlState.Running)
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
                    t.Message += this.NotifierTriggerMessage;
                    t.Error += this.NotifierTriggerError;
                    t.TriggerFired += this.NotifierTriggerFired;
                    t.Start(this.ManagementAgentName);
                }
                catch (Exception ex)
                {
                    this.Log($"Could not start execution trigger {t.DisplayName}");
                    Logger.WriteException(ex);
                }
            }
        }

        private void NotifierTriggerMessage(object sender, TriggerMessageEventArgs e)
        {
            IMAExecutionTrigger t = (IMAExecutionTrigger)sender;
            this.Log($"{t.DisplayName}: {e.Message}\n{e.Details}");
        }

        private void NotifierTriggerError(object sender, TriggerMessageEventArgs e)
        {
            IMAExecutionTrigger t = (IMAExecutionTrigger)sender;
            this.Log($"{t.DisplayName}: ERROR: {e.Message}\n{e.Details}");
        }

        private void StopTriggers()
        {
            foreach (IMAExecutionTrigger t in this.ExecutionTriggers)
            {
                try
                {
                    this.Log($"Unregistering execution trigger '{t.DisplayName}'");
                    t.TriggerFired -= this.NotifierTriggerFired;
                    t.Stop();
                }
                catch (OperationCanceledException)
                {
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
            this.Detail = message;
        }

        private void Trace(string message)
        {
            System.Diagnostics.Trace.WriteLine($"{this.ma.Name}: {message}");
        }

        private void Wait(TimeSpan duration, string name, CancellationTokenSource ts)
        {
            ts.Token.ThrowIfCancellationRequested();
            this.Trace($"SLEEP: {name}: {duration}");
            ts.Token.WaitHandle.WaitOne(duration);
            ts.Token.ThrowIfCancellationRequested();
        }

        private void Wait(ManualResetEvent mre, string name, CancellationTokenSource ts)
        {
            this.Trace($"LOCK: WAIT: {name}");
            WaitHandle.WaitAny(new[] { mre, ts.Token.WaitHandle });
            ts.Token.ThrowIfCancellationRequested();
            this.Trace($"LOCK: CLEARED: {name}");
        }

        private void WaitAndTakeLock(ManualResetEvent mre, string name, CancellationTokenSource ts)
        {
            bool gotLock = false;

            try
            {
                this.Trace($"SYNCOBJECT: WAIT: {name}");
                while (!gotLock)
                {
                    gotLock = Monitor.TryEnter(mre, MAExecutor.spinInterval);
                    ts.Token.ThrowIfCancellationRequested();
                }

                this.Trace($"SYNCOBJECT: LOCKED: {name}");

                this.Wait(mre, name, ts);
                this.TakeLockUnsafe(mre, name, ts);
            }
            finally
            {
                if (gotLock)
                {
                    Monitor.Exit(mre);
                    this.Trace($"SYNCOBJECT: UNLOCKED: {name}");
                }
            }
        }

        private void Wait(WaitHandle[] waitHandles, string name, CancellationTokenSource ts)
        {
            this.Trace($"LOCK: WAIT: {name}");
            while (!WaitHandle.WaitAll(waitHandles, 1000))
            {
                ts.Token.ThrowIfCancellationRequested();
            }

            ts.Token.ThrowIfCancellationRequested();
            this.Trace($"LOCK: CLEARED: {name}");
        }

        private void TakeLockUnsafe(ManualResetEvent mre, string name, CancellationTokenSource ts)
        {
            this.Trace($"LOCK: TAKE: {name}");
            mre.Reset();
            ts.Token.ThrowIfCancellationRequested();
        }

        private void TakeLock(ManualResetEvent mre, string name, CancellationTokenSource ts)
        {
            bool gotLock = false;

            try
            {
                this.Trace($"SYNCOBJECT: WAIT: {name}");
                while (!gotLock)
                {
                    gotLock = Monitor.TryEnter(mre, MAExecutor.spinInterval);
                    ts.Token.ThrowIfCancellationRequested();
                }

                this.Trace($"SYNCOBJECT: LOCKED: {name}");

                this.TakeLockUnsafe(mre, name, ts);
            }
            finally
            {
                if (gotLock)
                {
                    Monitor.Exit(mre);
                    this.Trace($"SYNCOBJECT: UNLOCKED: {name}");
                }
            }
        }

        private void ReleaseLock(ManualResetEvent mre, string name)
        {
            this.Trace($"LOCK: RELEASE: {name}");
            mre.Set();
        }

        private void Execute(ExecutionParameters e, CancellationTokenSource ts)
        {
            try
            {
                ts.Token.ThrowIfCancellationRequested();

                this.ExecutionState = ExecutorState.Waiting;
                this.ExecutingRunProfile = e.RunProfileName;
                ts.Token.ThrowIfCancellationRequested();

                if (this.ma.RunProfiles[e.RunProfileName].RunSteps.Any(t => t.IsImportStep))
                {
                    this.Trace("Import step detected. Resetting timer");
                    this.ResetImportTimerOnImport();
                }

                int count = 0;
                RunDetails r = null;

                while (count <= RegistrySettings.RetryCount || RegistrySettings.RetryCount < 0)
                {
                    ts.Token.ThrowIfCancellationRequested();
                    string result = null;

                    try
                    {
                        count++;
                        this.UpdateExecutionStatus(ExecutorState.Running, "Executing");
                        this.Log($"Executing {e.RunProfileName}");

                        try
                        {
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
                        this.Log($"{e.RunProfileName} returned {result}");
                        this.UpdateExecutionStatus(ExecutorState.Processing, "Evaluating run results");
                    }
                    
                    if (ts.IsCancellationRequested)
                    {
                        this.Log($"The run profile {e.RunProfileName} was canceled");
                        return;
                    }

                    this.Wait(RegistrySettings.PostRunInterval, nameof(RegistrySettings.PostRunInterval), ts);

                    this.Trace("Getting run results");
                    r = this.ma.GetLastRun();
                    this.Trace("Got run results");

                    this.RunProfileExecutionComplete?.Invoke(this, new RunProfileExecutionCompleteEventArgs(this.ManagementAgentName, r.RunProfileName, r.LastStepStatus, r.RunNumber, r.StartTime, r.EndTime));

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
                        this.Wait(TimeSpan.FromMilliseconds(interval), nameof(RegistrySettings.RetrySleepInterval), ts);
                        this.Log("Retrying operation");
                    }
                    else
                    {
                        this.Trace($"Result code '{result}' was not listed as retryable");
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
            }
        }

        private CancellationTokenSource CreateJobTokenSource()
        {
            this.jobCancellationTokenSource = new CancellationTokenSource();
            return CancellationTokenSource.CreateLinkedTokenSource(this.executorCancellationTokenSource.Token, this.jobCancellationTokenSource.Token);
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
                CancellationTokenSource linkedToken = this.CreateJobTokenSource();

                this.Trace("Unmanaged run in progress");
                this.TakeLock(this.localOperationLock, nameof(this.localOperationLock), linkedToken);


                this.Log($"Waiting on unmanaged run {this.ma.ExecutingRunProfileName} to finish");

                if (this.ma.RunProfiles[this.ma.ExecutingRunProfileName].RunSteps.Any(t => t.IsSyncStep))
                {
                    this.Log("Getting sync lock for unmanaged run");

                    try
                    {
                        this.WaitAndTakeLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock), linkedToken);
                        this.HasSyncLock = true;
                        this.ma.Wait(linkedToken.Token);
                    }
                    finally
                    {
                        if (this.HasSyncLock)
                        {
                            this.ReleaseLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock));
                            this.HasSyncLock = false;
                        }
                    }
                }
                else
                {
                    this.ma.Wait(linkedToken.Token);
                }

                this.UpdateExecutionStatus(ExecutorState.Processing, "Evaluating run results");
                linkedToken.Token.ThrowIfCancellationRequested();

                using (RunDetails ur = this.ma.GetLastRun())
                {
                    this.RunProfileExecutionComplete?.Invoke(this, new RunProfileExecutionCompleteEventArgs(this.ManagementAgentName, ur.RunProfileName, ur.LastStepStatus, ur.RunNumber, ur.StartTime, ur.EndTime));
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

            if (this.controller.HasStoppedMA)
            {
                this.Stop(false);
                return;
            }

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
                this.Stop(false);
            }
        }

        public void Start(MAConfigParameters config)
        {
            if (config.IsNew || config.IsMissing || config.Disabled)
            {
                Logger.WriteLine($"Ignoring start request as management agent {config.ManagementAgentName} is disabled or unconfigured");
                this.ControlState = ControlState.Disabled;
                return;
            }

            if (this.ControlState == ControlState.Running)
            {
                this.Trace($"Ignoring request to start {config.ManagementAgentName} as it is already running");
                return;
            }

            if (this.ControlState != ControlState.Stopped && this.ControlState != ControlState.Disabled)
            {
                throw new InvalidOperationException($"Cannot start an executor that is in the {this.ControlState} state");
            }

            try
            {
                Logger.WriteLine($"Preparing to start executor for {config.ManagementAgentName}");

                this.executorCancellationTokenSource = new CancellationTokenSource();

                this.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock), this.executorCancellationTokenSource);

                this.ControlState = ControlState.Starting;

                this.pendingActionList = new ExecutionParameterCollection();
                this.pendingActions = new BlockingCollection<ExecutionParameters>(this.pendingActionList);
                this.perProfileLastRunStatus = new Dictionary<string, string>();

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
                        this.executorCancellationTokenSource.Token.ThrowIfCancellationRequested();
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
                }, this.executorCancellationTokenSource.Token);

                this.internalTask.Start();

                this.ControlState = ControlState.Running;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred starting the executor");
                Logger.WriteException(ex);
                this.Stop(false);
                this.Message = $"Startup error: {ex.Message}";
            }
            finally
            {
                this.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock));
            }
        }

        private void TryCancelRun()
        {
            try
            {
                if (this.ma != null && !this.ma.IsIdle())
                {
                    this.Log("Requesting sync engine to terminate run");
                    this.ma.StopAsync();
                }
                else
                {
                    this.Log("Canceling current job");
                    this.jobCancellationTokenSource?.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Cannot cancel run");
                Logger.WriteException(ex);
            }
        }

        public void Stop(bool cancelRun)
        {
            try
            {
                this.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock), this.executorCancellationTokenSource ?? new CancellationTokenSource());

                if (this.ControlState == ControlState.Stopped || this.ControlState == ControlState.Disabled)
                {
                    if (cancelRun)
                    {
                        this.TryCancelRun();
                    }

                    return;
                }

                if (this.ControlState == ControlState.Stopping)
                {
                    return;
                }

                this.ControlState = ControlState.Stopping;

                this.Log("Stopping MAExecutor");
                this.pendingActions?.CompleteAdding();
                this.executorCancellationTokenSource?.Cancel();

                this.StopTriggers();

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

                if (cancelRun)
                {
                    this.TryCancelRun();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred stopping the executor");
                Logger.WriteException(ex);
                this.Message = $"Stop error: {ex.Message}";
            }
            finally
            {
                this.importCheckTimer?.Stop();
                this.ExecutionTriggers.Clear();

                this.pendingActionList = null;
                this.pendingActions = null;
                this.internalTask = null;
                this.InternalStatus.Clear();
                this.ControlState = ControlState.Stopped;
                this.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock));
            }
        }

        public void CancelRun()
        {
            this.TryCancelRun();
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

        private void Init()
        {
            try
            {
                this.WaitOnUnmanagedRun();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred in an unmanaged run");
                Logger.WriteException(ex);
            }

            this.executorCancellationTokenSource.Token.ThrowIfCancellationRequested();

            this.CheckAndQueueUnmanagedChanges();

            this.executorCancellationTokenSource.Token.ThrowIfCancellationRequested();

            this.StartTriggers();

            this.SetupImportSchedule();

            this.SetupUnmanagedChangesCheckTimer();

            this.executorCancellationTokenSource.Token.ThrowIfCancellationRequested();

            try
            {
                this.Log("Starting action processing queue");
                this.UpdateExecutionStatus(ExecutorState.Idle, null, null);

                // ReSharper disable once InconsistentlySynchronizedField
                foreach (ExecutionParameters action in this.pendingActions.GetConsumingEnumerable(this.executorCancellationTokenSource.Token))
                {
                    this.executorCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    this.UpdateExecutionStatus(ExecutorState.Waiting, "Staging run", action.RunProfileName, this.GetQueueItemNames(false));

                    if (this.controller.SupportsShouldExecute)
                    {
                        this.Message = "Asking controller for execution permission";

                        if (!this.controller.ShouldExecute(action.RunProfileName))
                        {
                            this.Log($"Controller indicated that run profile {action.RunProfileName} should not be executed");
                            continue;
                        }
                    }

                    this.SetExclusiveMode(action);
                    this.TakeLocksAndExecute(action);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.Log("Stopped action processing queue");
            }
        }

        private void TakeLocksAndExecute(ExecutionParameters action)
        {
            try
            {
                this.WaitOnUnmanagedRun();

                this.jobCancellationTokenSource = this.CreateJobTokenSource();

                this.UpdateExecutionStatus(ExecutorState.Waiting, "Waiting for lock holder to finish", action.RunProfileName);
                this.Wait(MAExecutor.GlobalExclusiveOperationLock, nameof(MAExecutor.GlobalExclusiveOperationLock), this.jobCancellationTokenSource);

                if (action.Exclusive)
                {
                    this.Message = "Waiting to take lock";
                    this.Log($"Entering exclusive mode for {action.RunProfileName}");

                    // Signal all executors to wait before running their next job
                    this.WaitAndTakeLock(MAExecutor.GlobalExclusiveOperationLock, nameof(MAExecutor.GlobalExclusiveOperationLock), this.jobCancellationTokenSource);
                    this.HasExclusiveLock = true;

                    this.Message = "Waiting for other MAs to finish";
                    this.Log("Waiting for all MAs to complete");
                    // Wait for all  MAs to finish their current job
                    this.Wait(MAExecutor.AllMaLocalOperationLocks.Values.ToArray(), nameof(MAExecutor.AllMaLocalOperationLocks), this.jobCancellationTokenSource);
                }

                if (this.StepRequiresSyncLock(action.RunProfileName))
                {
                    this.Message = "Waiting to take lock";
                    this.Log("Waiting to take sync lock");
                    this.WaitAndTakeLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock), this.jobCancellationTokenSource);
                    this.HasSyncLock = true;
                }

                // If another operation in this executor is already running, then wait for it to finish before taking the lock for ourselves
                this.Message = "Waiting for lock on management agent";
                this.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), this.jobCancellationTokenSource);

                // Grab the staggered execution lock, and hold for x seconds
                // This ensures that no MA can start within x seconds of another MA
                // to avoid deadlock conditions
                this.Message = "Preparing to start management agent";
                bool tookStaggerLock = false;
                try
                {
                    this.WaitAndTakeLock(MAExecutor.GlobalStaggeredExecutionLock, nameof(MAExecutor.GlobalStaggeredExecutionLock), this.jobCancellationTokenSource);
                    tookStaggerLock = true;
                    this.Wait(RegistrySettings.ExecutionStaggerInterval, nameof(RegistrySettings.ExecutionStaggerInterval), this.jobCancellationTokenSource);
                }
                finally
                {
                    if (tookStaggerLock)
                    {
                        this.ReleaseLock(MAExecutor.GlobalStaggeredExecutionLock, nameof(MAExecutor.GlobalStaggeredExecutionLock));
                    }
                }

                this.Execute(action, this.jobCancellationTokenSource);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.UpdateExecutionStatus(ExecutorState.Idle, null, null);

                // Reset the local lock so the next operation can run
                this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));

                if (this.HasSyncLock)
                {
                    this.ReleaseLock(MAExecutor.GlobalSynchronizationStepLock, nameof(MAExecutor.GlobalSynchronizationStepLock));
                    this.HasSyncLock = false;
                }

                if (this.HasExclusiveLock)
                {
                    // Reset the global lock so pending operations can run
                    this.ReleaseLock(MAExecutor.GlobalExclusiveOperationLock, nameof(MAExecutor.GlobalExclusiveOperationLock));
                    this.HasExclusiveLock = false;
                }
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

        private bool StepRequiresSyncLock(string runProfileName)
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

                if (this.ma.RunProfiles[runProfileName].RunSteps.Any(t => t.Type == RunStepType.Export))
                {
                    if (RegistrySettings.GetSyncLockForFimMAExport)
                    {
                        return true;
                    }
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
                this.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), this.executorCancellationTokenSource);

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

        private void NotifierTriggerFired(object sender, ExecutionTriggerEventArgs e)
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

        internal void AddPendingActionIfNotQueued(string runProfileName, string source)
        {
            this.AddPendingActionIfNotQueued(new ExecutionParameters(runProfileName), source);
        }

        internal void AddPendingActionIfNotQueued(ExecutionParameters p, string source, bool runNext = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p.RunProfileName))
                {
                    if (p.RunProfileType == MARunProfileType.None)
                    {
                        this.Trace("Dropping pending action request as no run profile name or run profile type was specified");
                        this.Detail = $"{source} did not specify a run profile";
                        this.RaiseStateChange();
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
                        this.Detail = $"{p.RunProfileName} requested by {source} was ignored because the run profile was already queued";
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
                    this.pendingActions.Add(p, this.executorCancellationTokenSource.Token);
                    this.pendingActionList.MoveToFront(p);
                    this.Log($"Added {p.RunProfileName} to the front of the execution queue (triggered by: {source})");
                }
                else
                {
                    this.pendingActions.Add(p, this.executorCancellationTokenSource.Token);
                    this.Log($"Added {p.RunProfileName} to the execution queue (triggered by: {source})");
                }

                //this.Detail = $"{p.RunProfileName} added by {source}";

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

            return Program.ActiveConfig.Settings.MailIgnoreReturnCodes == null ||
                !Program.ActiveConfig.Settings.MailIgnoreReturnCodes.Contains(r.LastStepStatus, StringComparer.OrdinalIgnoreCase);
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