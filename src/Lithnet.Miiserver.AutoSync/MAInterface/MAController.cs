using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using Lithnet.Miiserver.Client;
using NLog;
using Timer = System.Timers.Timer;
using System.Runtime.CompilerServices;

namespace Lithnet.Miiserver.AutoSync
{
    internal class MAController
    {
        private const int MonitorLockWaitInterval = 100;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        protected static SemaphoreSlim GlobalStaggeredExecutionLock;
        protected static ManualResetEvent GlobalExclusiveOperationLock;
        protected static SemaphoreSlim GlobalExclusiveOperationLockSemaphore;
        protected static SemaphoreSlim GlobalSynchronizationStepLock;
        protected static ConcurrentDictionary<Guid, SemaphoreSlim> AllMaLocalOperationLocks;

        public delegate void SyncCompleteEventHandler(object sender, SyncCompleteEventArgs e);
        public static event SyncCompleteEventHandler SyncComplete;

        public delegate void RunProfileExecutionCompleteEventHandler(object sender, RunProfileExecutionCompleteEventArgs e);
        public event RunProfileExecutionCompleteEventHandler RunProfileExecutionComplete;

        public delegate void StateChangedEventHandler(object sender, MAStatusChangedEventArgs e);
        public event StateChangedEventHandler StateChanged;

        public delegate void MessageLoggedEventHandler(object sender, MessageLoggedEventArgs e);
        public event MessageLoggedEventHandler MessageLogged;

        private SemaphoreSlim localOperationLock;
        private SemaphoreSlim serviceControlLock;
        private Timer unmanagedChangesCheckTimer;
        private CancellationTokenSource controllerCancellationTokenSource;
        private CancellationTokenSource jobCancellationTokenSource;
        private Dictionary<string, string> perProfileLastRunStatus;
        private ManagementAgent ma;
        private BlockingCollection<ExecutionParameters> pendingActions;
        private ExecutionParameterCollection pendingActionList;
        private MAControllerScript controllerScript;
        private Task internalTask;
        private PartitionDetectionMode detectionMode;
        private int lastRunNumber = 0;

        private Dictionary<Guid, Timer> importCheckTimers = new Dictionary<Guid, Timer>();

        internal MAStatus InternalStatus;

        private List<IMAExecutionTrigger> ExecutionTriggers { get; }

        public MAControllerConfiguration Configuration { get; private set; }

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

        public string ManagementAgentName => this.ma?.Name;

        public Guid ManagementAgentID => this.ma?.ID ?? Guid.Empty;

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

        public bool HasForeignLock
        {
            get => this.InternalStatus.HasForeignLock;
            private set
            {
                if (this.InternalStatus.HasForeignLock != value)
                {
                    this.InternalStatus.HasForeignLock = value;
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

        public ControllerState ExecutionState
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

        static MAController()
        {
            MAController.GlobalSynchronizationStepLock = new SemaphoreSlim(1, 1);
            MAController.GlobalStaggeredExecutionLock = new SemaphoreSlim(1, 1);
            MAController.GlobalExclusiveOperationLockSemaphore = new SemaphoreSlim(1, 1);
            MAController.GlobalExclusiveOperationLock = new ManualResetEvent(true);
            MAController.AllMaLocalOperationLocks = new ConcurrentDictionary<Guid, SemaphoreSlim>();
        }

        public MAController(ManagementAgent ma)
        {
            this.controllerCancellationTokenSource = new CancellationTokenSource();
            this.ma = ma;
            this.InternalStatus = new MAStatus() { ManagementAgentName = this.ma.Name, ManagementAgentID = this.ma.ID };
            this.ControlState = ControlState.Stopped;
            this.ExecutionTriggers = new List<IMAExecutionTrigger>();
            this.localOperationLock = new SemaphoreSlim(1, 1);
            this.serviceControlLock = new SemaphoreSlim(1, 1);
            MAController.AllMaLocalOperationLocks.TryAdd(this.ma.ID, this.localOperationLock);
            MAController.SyncComplete += this.MAController_SyncComplete;
        }

        private void RaiseMessageLogged(string message)
        {
            Task.Run(() =>
            {
                try
                {
                    this.MessageLogged?.Invoke(this, new MessageLoggedEventArgs(DateTime.Now, message));
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Unable to relay state change");
                }
            }, this.controllerCancellationTokenSource.Token);
        }

        private void RaiseStateChange()
        {
            Task.Run(() =>
            {
                try
                {
                    this.StateChanged?.Invoke(this, new MAStatusChangedEventArgs(this.InternalStatus, this.ma.Name));
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Unable to relay state change");
                }
            }); // Using the global cancellation token here prevents the final state messages being received (see issue #80)
        }

        private void Setup(MAControllerConfiguration config)
        {
            if (!this.ma.Name.Equals(config.ManagementAgentName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Configuration was provided for the management agent {config.ManagementAgentName} for a controller configured for {this.ma.Name}");
            }

            this.Configuration = config;
            this.InternalStatus.ActiveVersion = config.Version;
            this.ControlState = config.Disabled ? ControlState.Disabled : ControlState.Stopped;
            this.controllerScript = new MAControllerScript(config);
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

        private int inUnmangedChangesTimer = 0;

        private void UnmanagedChangesCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.ControlState != ControlState.Running)
            {
                return;
            }

            if (Interlocked.Exchange(ref this.inUnmangedChangesTimer, 1) == 1)
            {
                return;
            }

            Thread.CurrentThread.SetThreadName($"{this.ManagementAgentName} unmanaged changes check");

            try
            {
                this.CheckAndQueueUnmanagedChanges();
            }
            finally
            {
                Interlocked.Exchange(ref this.inUnmangedChangesTimer, 0);
            }
        }

        private void SetupImportTimers()
        {
            this.importCheckTimers.Clear();

            foreach (PartitionConfiguration p in this.Configuration.Partitions.Where(u => u.AutoImportEnabled))
            {
                double interval = TimeSpan.FromMinutes(Math.Max(p.AutoImportIntervalMinutes, 1)).TotalMilliseconds;
                bool timerIntervalReset = false;

                Timer t = new Timer();
                t.AutoReset = true;
                t.Interval = Global.RandomizeOffset(interval);
                t.Elapsed += (sender, e) =>
                {
                    if (this.ControlState != ControlState.Running)
                    {
                        return;
                    }

                    if (!timerIntervalReset)
                    {
                        t.Interval = interval;
                        timerIntervalReset = true;
                    }

                    this.AddPendingActionIfNotQueued(new ExecutionParameters(p.ScheduledImportRunProfileName), $"Import timer on {p.Name}");
                };

                t.Start();
                this.Trace($"Initialized import timer for partition {p.Name} at interval of {t.Interval}");
                this.importCheckTimers.Add(p.ID, t);
            }
        }

        private void StopImportTimers()
        {
            if (this.importCheckTimers == null)
            {
                return;
            }

            foreach (KeyValuePair<Guid, Timer> kvp in this.importCheckTimers)
            {
                kvp.Value.Stop();
            }
        }

        private void ResetImportTimerOnImport(Guid partition)
        {
            if (this.importCheckTimers.ContainsKey(partition))
            {
                this.importCheckTimers[partition].Stop();
                this.importCheckTimers[partition].Start();
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
                    this.LogInfo($"Registering execution trigger '{t.DisplayName}'");
                    t.Message += this.NotifierTriggerMessage;
                    t.Error += this.NotifierTriggerError;
                    t.TriggerFired += this.NotifierTriggerFired;
                    t.Start(this.ManagementAgentName);
                }
                catch (Exception ex)
                {
                    this.LogError(ex, $"Could not start execution trigger {t.DisplayName}");
                }
            }
        }

        private void NotifierTriggerMessage(object sender, TriggerMessageEventArgs e)
        {
            IMAExecutionTrigger t = (IMAExecutionTrigger)sender;
            if (e.Details == null)
            {
                this.LogInfo($"{t.DisplayName}: {e.Message}");
            }
            else
            {
                this.LogInfo($"{t.DisplayName}: {e.Message}\n{e.Details}");
            }
        }

        private void NotifierTriggerError(object sender, TriggerMessageEventArgs e)
        {
            IMAExecutionTrigger t = (IMAExecutionTrigger)sender;
            if (e.Details == null)
            {
                this.LogError($"{t.DisplayName}: {e.Message}");
            }
            else
            {
                this.LogError($"{t.DisplayName}: {e.Message}\n{e.Details}");
            }
        }

        private void StopTriggers()
        {
            foreach (IMAExecutionTrigger t in this.ExecutionTriggers)
            {
                try
                {
                    this.LogInfo($"Unregistering execution trigger '{t.DisplayName}'");
                    t.TriggerFired -= this.NotifierTriggerFired;
                    t.Message -= this.NotifierTriggerMessage;
                    t.Error -= this.NotifierTriggerError;
                    t.Stop();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    this.LogError(ex, $"Could not stop execution trigger {t.DisplayName}");
                }
            }
        }

        private void QueueFollowupActions(RunDetails d)
        {
            this.Trace($"Analyzing results from {d.RunProfileName} run #{d.RunNumber}");

            for (int index = d.StepDetails.Count - 1; index >= 0; index--)
            {
                StepDetails s = d.StepDetails[index];

                if (s.StepDefinition == null)
                {
                    this.LogWarn($"Step detail for step {s.StepNumber} was missing the step definition and cannot be processed");
                    continue;
                }

                if (s.StepDefinition.IsSyncStep)
                {
                    this.Trace($"Processing outbound changes from step {s.StepNumber} of {d.RunProfileName}");
                    this.QueueFollowUpActionsSync(s);
                    continue;
                }

                Tuple<PartitionConfiguration, MARunProfileType> requiredAction = this.GetImportExportFollowUpActions(s);

                if (requiredAction == null)
                {
                    // nothing to do
                    this.Trace($"Step {s.StepNumber} of {d.RunProfileName} had no follow up actions to perform");
                    continue;
                }

                if (this.WasFollowupAlreadyPerformed(d, index, requiredAction))
                {
                    this.Trace($"The expected follow up action '{requiredAction.Item2}' in partition '{requiredAction.Item1.Name}' for step {s.StepNumber} of run profile '{d.RunProfileName}' has already been performed");
                }
                else
                {
                    this.Trace($"The expected follow up action '{requiredAction.Item2}' in partition '{requiredAction.Item1.Name}' for step {s.StepNumber} of run profile '{d.RunProfileName}' has not yet been performed");

                    if (requiredAction.Item2 == MARunProfileType.DeltaImport)
                    {
                        if (requiredAction.Item1.ConfirmingImportRunProfileName == null)
                        {
                            this.LogWarn($"A confirming import was required, but they have not been configured for partition {requiredAction.Item1.Name}");
                            continue;
                        }

                        this.AddPendingActionIfNotQueued(new ExecutionParameters(requiredAction.Item1.ConfirmingImportRunProfileName), d.RunProfileName, true);
                        continue;
                    }

                    if (requiredAction.Item2 == MARunProfileType.DeltaSync)
                    {
                        if (requiredAction.Item1.DeltaSyncRunProfileName == null)
                        {
                            this.LogWarn($"A delta sync was required, but they have not been configured for partition {requiredAction.Item1.Name}");
                            continue;
                        }

                        this.AddPendingActionIfNotQueued(new ExecutionParameters(requiredAction.Item1.DeltaSyncRunProfileName), d.RunProfileName, true);
                        continue;
                    }
                }
            }
        }

        private bool WasFollowupAlreadyPerformed(RunDetails d, int i, Tuple<PartitionConfiguration, MARunProfileType> result)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                StepDetails f = d.StepDetails[j];

                if (f.StepDefinition == null)
                {
                    this.LogWarn($"Step detail was missing the step definition");
                    continue;
                }

                PartitionConfiguration p = this.Configuration.Partitions.GetActiveItemOrNull(f.StepDefinition.Partition);

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

        private Tuple<PartitionConfiguration, MARunProfileType> GetImportExportFollowUpActions(StepDetails s)
        {
            if (s.StepDefinition == null)
            {
                this.LogWarn($"Step detail was missing the step definition");
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

        private Tuple<PartitionConfiguration, MARunProfileType> GetImportFollowUpAction(StepDetails s)
        {
            if (!s.StepDefinition.IsImportStep)
            {
                return null;
            }

            if (!s.StagingCounters?.HasChanges ?? false)
            {
                this.Trace($"No staged imports in step {s.StepNumber}");
                return null;
            }

            // has staged imports
            this.Trace($"Staged imports in step {s.StepNumber}");

            string partitionName = s.StepDefinition.Partition;

            if (partitionName == null)
            {
                this.LogWarn($"Partition in step {s.StepNumber} was blank");
                return null;
            }

            PartitionConfiguration p = this.Configuration.Partitions.GetActiveItemOrNull(partitionName);

            if (p == null)
            {
                this.LogWarn($"Could not find the partition {partitionName}");
                return null;
            }

            return new Tuple<PartitionConfiguration, MARunProfileType>(p, MARunProfileType.DeltaSync);
        }

        private Tuple<PartitionConfiguration, MARunProfileType> GetExportFollowUpAction(StepDetails s)
        {
            if (!s.StepDefinition.IsExportStep)
            {
                return null;
            }

            if (!s.ExportCounters?.HasChanges ?? false)
            {
                this.Trace($"No unconfirmed exports in step {s.StepNumber}");
                return null;
            }

            // has unconfirmed exports
            this.Trace($"Unconfirmed exports in step {s.StepNumber}");

            string partitionName = s.StepDefinition.Partition;

            if (partitionName == null)
            {
                this.LogWarn($"Partition in step {s.StepNumber} was blank");
                return null;
            }

            PartitionConfiguration p = this.Configuration.Partitions.GetActiveItemOrNull(partitionName);

            if (p == null)
            {
                this.LogWarn($"Could not find the partition {partitionName}");
                return null;
            }

            return new Tuple<PartitionConfiguration, MARunProfileType>(p, MARunProfileType.DeltaImport);
        }

        private void QueueFollowUpActionsSync(StepDetails s)
        {
            if (!s.StepDefinition.IsSyncStep)
            {
                return;
            }

            SyncCompleteEventHandler registeredHandlers = MAController.SyncComplete;

            if (registeredHandlers == null)
            {
                this.Trace("No sync event handlers were registered");
                return;
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
                    SendingMAName = this.ManagementAgentName,
                    TargetMA = item.MAID
                };

                this.Trace($"Sending outbound change notification for MA {item.ManagementAgent}");
                registeredHandlers(this, args);
            }
        }

        private void LogInfo(string message)
        {
            logger.Info($"{this.ManagementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        private void LogWarn(string message)
        {
            logger.Warn($"{this.ManagementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        private void LogWarn(Exception ex, string message)
        {
            logger.Warn(ex, $"{this.ManagementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        private void LogError(string message)
        {
            logger.Error($"{this.ManagementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        private void LogError(Exception ex, string message)
        {
            logger.Error(ex, $"{this.ManagementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        private void Trace(string message)
        {
            logger.Trace($"{this.ManagementAgentName}: {message}");
        }

        private void Debug(string message)
        {
#if LOCKDEBUG
            logger.Trace($"{this.ManagementAgentName}: {message}");
#endif
        }

        private void Wait(TimeSpan duration, string name, CancellationTokenSource ts, [CallerMemberName]string caller = "")
        {
            ts.Token.ThrowIfCancellationRequested();
            this.Debug($"SLEEP: {name}: {duration}: {caller}");
            ts.Token.WaitHandle.WaitOne(duration);
            ts.Token.ThrowIfCancellationRequested();
        }

        private void Wait(WaitHandle wh, string name, CancellationTokenSource ts, [CallerMemberName]string caller = "")
        {
            this.Debug($"LOCK: WAIT: {name}: {caller}");
            WaitHandle.WaitAny(new[] { wh, ts.Token.WaitHandle });
            ts.Token.ThrowIfCancellationRequested();
            this.Debug($"LOCK: CLEARED: {name}: {caller}");
        }

        private void WaitAndTakeLock(SemaphoreSlim mre, string name, CancellationTokenSource ts, [CallerMemberName]string caller = "")
        {
            this.Debug($"LOCK: WAIT: {name}: {caller}");
            mre.Wait(ts.Token);
            this.Debug($"LOCK: TAKE: {name}: {caller}");
        }

        private void WaitAndTakeLockWithSemaphore(EventWaitHandle mre, SemaphoreSlim sem, string name, CancellationTokenSource ts, [CallerMemberName]string caller = "")
        {
            bool gotLock = false;

            try
            {
                this.Debug($"SYNCOBJECT: WAIT: {name}: {caller}");
                sem.Wait(ts.Token);
                gotLock = true;
                this.Debug($"SYNCOBJECT: LOCKED: {name}: {caller}");
                this.Wait(mre, name, ts);
                this.TakeLockUnsafe(mre, name, ts, caller);
            }
            finally
            {
                if (gotLock)
                {
                    sem.Release();
                    this.Debug($"SYNCOBJECT: UNLOCKED: {name}: {caller}");
                }
            }
        }

        private void Wait(WaitHandle[] waitHandles, string name, CancellationTokenSource ts, [CallerMemberName]string caller = "")
        {
            this.Debug($"LOCK: WAIT: {name}: {caller}");
            while (!WaitHandle.WaitAll(waitHandles, 1000))
            {
                ts.Token.ThrowIfCancellationRequested();
            }

            ts.Token.ThrowIfCancellationRequested();
            this.Debug($"LOCK: CLEARED: {name}: {caller}");
        }

        private void TakeLockUnsafe(EventWaitHandle mre, string name, CancellationTokenSource ts, string caller)
        {
            this.Debug($"LOCK: TAKE: {name}: {caller}");
            mre.Reset();
            ts.Token.ThrowIfCancellationRequested();
        }

        private void ReleaseLock(EventWaitHandle mre, string name, [CallerMemberName]string caller = "")
        {
            this.Debug($"LOCK: RELEASE: {name}: {caller}");
            mre.Set();
        }

        private void ReleaseLock(SemaphoreSlim mre, string name, [CallerMemberName] string caller = "")
        {
            this.Debug($"LOCK: RELEASE: {name}: {caller}");
            mre.Release();
        }

        private void RaiseRunProfileComplete(string runProfileName, string lastStepStatus, int runNumber, DateTime? startTime, DateTime? endTime)
        {
            Task.Run(() =>
            {
                try
                {
                    this.RunProfileExecutionComplete?.Invoke(this, new RunProfileExecutionCompleteEventArgs(this.ManagementAgentName, runProfileName, lastStepStatus, runNumber, startTime, endTime));
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Unable to relay run profile complete notification");
                }
            }, this.controllerCancellationTokenSource.Token);
        }

        private void Execute(ExecutionParameters e, CancellationTokenSource ts)
        {
            try
            {
                ts.Token.ThrowIfCancellationRequested();

                this.ExecutionState = ControllerState.Waiting;
                this.ExecutingRunProfile = e.RunProfileName;
                ts.Token.ThrowIfCancellationRequested();

                foreach (RunStep s in this.ma.RunProfiles[e.RunProfileName].RunSteps)
                {
                    if (s.IsImportStep)
                    {
                        this.Trace($"Import step detected on partition {s.Partition}. Resetting timer");
                        this.ResetImportTimerOnImport(s.Partition);
                    }
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
                        this.UpdateExecutionStatus(ControllerState.Running, "Executing");
                        this.LogInfo($"Executing {e.RunProfileName}");

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
                        this.LogInfo($"{e.RunProfileName} returned {result}");
                        this.UpdateExecutionStatus(ControllerState.Processing, "Evaluating run results");
                    }

                    if (ts.IsCancellationRequested)
                    {
                        this.LogInfo($"The run profile {e.RunProfileName} was canceled");
                        return;
                    }

                    this.Wait(RegistrySettings.PostRunInterval, nameof(RegistrySettings.PostRunInterval), ts);

                    this.Trace("Getting run results");
                    r = this.ma.GetLastRun();
                    this.lastRunNumber = r.RunNumber;

                    this.Trace("Got run results");

                    this.RaiseRunProfileComplete(r.RunProfileName, r.LastStepStatus, r.RunNumber, r.StartTime, r.EndTime);

                    if (RegistrySettings.RetryCodes.Contains(result))
                    {
                        this.Trace($"Operation is retryable. {count} attempt{count.Pluralize()} made");

                        if (count > RegistrySettings.RetryCount && RegistrySettings.RetryCount >= 0)
                        {
                            this.LogInfo($"Aborting run profile after {count} attempt{count.Pluralize()}");
                            break;
                        }

                        this.UpdateExecutionStatus(ControllerState.Waiting, "Waiting to retry operation");

                        int interval = Global.RandomizeOffset(RegistrySettings.RetrySleepInterval.TotalMilliseconds * count);
                        this.Trace($"Sleeping thread for {interval}ms before retry");
                        this.Wait(TimeSpan.FromMilliseconds(interval), nameof(RegistrySettings.RetrySleepInterval), ts);
                        this.LogInfo("Retrying operation");
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
            catch (ThresholdExceededException)
            {
                throw;
            }
            catch (System.Management.Automation.RuntimeException ex)
            {
                if (ex.InnerException is UnexpectedChangeException changeException)
                {
                    this.ProcessUnexpectedChangeException(changeException);
                }
                else
                {
                    this.LogError(ex, $"Controller encountered an error executing run profile {this.ExecutingRunProfile}");
                }
            }
            catch (UnexpectedChangeException ex)
            {
                this.ProcessUnexpectedChangeException(ex);
            }
            catch (Exception ex)
            {
                this.LogError(ex, $"Controller encountered an error executing run profile {this.ExecutingRunProfile}");
            }
            finally
            {
                this.UpdateExecutionStatus(ControllerState.Idle, null, null);
            }
        }

        private CancellationTokenSource CreateJobTokenSource()
        {
            this.jobCancellationTokenSource = new CancellationTokenSource();
            return CancellationTokenSource.CreateLinkedTokenSource(this.controllerCancellationTokenSource.Token, this.jobCancellationTokenSource.Token);
        }

        private void WaitOnUnmanagedRun()
        {
            if (this.ma.IsIdle())
            {
                return;
            }

            bool hasLocalLock = false;

            try
            {
                string erp = this.ma.ExecutingRunProfileName;

                if (erp == null)
                {
                    return;
                }

                this.UpdateExecutionStatus(ControllerState.Running, "Unmanaged run in progress", erp);
                CancellationTokenSource linkedToken = this.CreateJobTokenSource();

                this.Trace("Unmanaged run in progress");
                this.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), linkedToken);
                hasLocalLock = true;

                this.LogInfo($"Waiting on unmanaged run {erp} to finish");

                if (this.ma.RunProfiles[erp].RunSteps.Any(t => t.IsSyncStep))
                {
                    this.Trace("Getting sync lock for unmanaged run");

                    try
                    {
                        this.WaitAndTakeLock(MAController.GlobalSynchronizationStepLock, nameof(MAController.GlobalSynchronizationStepLock), linkedToken);
                        this.HasSyncLock = true;
                        this.ma.Wait(linkedToken.Token);
                    }
                    finally
                    {
                        if (this.HasSyncLock)
                        {
                            this.ReleaseLock(MAController.GlobalSynchronizationStepLock, nameof(MAController.GlobalSynchronizationStepLock));
                            this.HasSyncLock = false;
                        }
                    }
                }
                else
                {
                    this.ma.Wait(linkedToken.Token);
                }

                this.UpdateExecutionStatus(ControllerState.Processing, "Evaluating run results");
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
                    this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));
                }

                this.Trace("Unmanaged run complete");
                this.UpdateExecutionStatus(ControllerState.Idle, null, null);
            }
        }

        private void PerformPostRunActions(RunDetails r)
        {
            this.lastRunNumber = r.RunNumber;

            this.controllerScript.ExecutionComplete(r);

            if (this.controllerScript.HasStoppedMA)
            {
                this.Stop(false, false, false);
                return;
            }

            this.ThrowOnThresholdsExceeded(r);

            this.TrySendMail(r);

            this.QueueFollowupActions(r);
        }

        private void ThrowOnThresholdsExceeded(RunDetails r)
        {
            if (this.Configuration.StagingThresholds == null)
            {
                return;
            }

            foreach (StepDetails s in r.StepDetails)
            {
                if (this.Configuration.StagingThresholds.Adds > 0)
                {
                    if (s.StagingCounters.StageAdd >= this.Configuration.StagingThresholds.Adds)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageAdd} adds which triggered the threshold of {this.Configuration.StagingThresholds.Adds}", r);
                    }
                }

                if (this.Configuration.StagingThresholds.Deletes > 0)
                {
                    if (s.StagingCounters.StageDelete >= this.Configuration.StagingThresholds.Deletes)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageDelete} deletes which triggered the threshold of {this.Configuration.StagingThresholds.Deletes}", r);
                    }
                }

                if (this.Configuration.StagingThresholds.Renames > 0)
                {
                    if (s.StagingCounters.StageRename >= this.Configuration.StagingThresholds.Renames)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageRename} renames which triggered the threshold of {this.Configuration.StagingThresholds.Renames}", r);
                    }
                }

                if (this.Configuration.StagingThresholds.Updates > 0)
                {
                    if (s.StagingCounters.StageUpdate >= this.Configuration.StagingThresholds.Updates)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageUpdate} updates which triggered the threshold of {this.Configuration.StagingThresholds.Updates}", r);
                    }
                }

                if (this.Configuration.StagingThresholds.DeleteAdds > 0)
                {
                    if (s.StagingCounters.StageDeleteAdd >= this.Configuration.StagingThresholds.DeleteAdds)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StageDeleteAdd} delete/adds which triggered the threshold of {this.Configuration.StagingThresholds.DeleteAdds}", r);
                    }
                }

                if (this.Configuration.StagingThresholds.Changes > 0)
                {
                    if (s.StagingCounters.StagingChanges >= this.Configuration.StagingThresholds.Changes)
                    {
                        throw new ThresholdExceededException($"The management agent operation staged {s.StagingCounters.StagingChanges} total changes which triggered the threshold of {this.Configuration.StagingThresholds.Changes}", r);
                    }
                }
            }
        }

        private void ProcessUnexpectedChangeException(UnexpectedChangeException ex)
        {
            if (ex.ShouldTerminateService)
            {
                this.LogWarn($"Controller script indicated that service should immediately stop. Run profile {this.ExecutingRunProfile}");
                Program.Engine.Stop(true);
            }
            else
            {
                this.LogWarn($"Controller indicated that management agent controller should stop further processing on this MA. Run Profile {this.ExecutingRunProfile}");
                this.Stop(false, false, false);
            }
        }

        public void Start(MAControllerConfiguration config)
        {
            if (this.ControlState == ControlState.Running)
            {
                this.Trace($"Ignoring request to start {config.ManagementAgentName} as it is already running");
                return;
            }

            if (this.ControlState != ControlState.Stopped && this.ControlState != ControlState.Disabled)
            {
                throw new InvalidOperationException($"Cannot start a controller that is in the {this.ControlState} state");
            }

            this.controllerCancellationTokenSource = new CancellationTokenSource();

            if (config.Version == 0 || config.IsMissing || config.Disabled)
            {
                logger.Info($"Ignoring start request as management agent {config.ManagementAgentName} is disabled or unconfigured");
                this.ControlState = ControlState.Disabled;
                return;
            }

            bool gotLock = false;

            try
            {
                logger.Info($"Preparing to start controller for {config.ManagementAgentName}");

                this.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock), this.controllerCancellationTokenSource);
                gotLock = true;
                this.ControlState = ControlState.Starting;
                this.pendingActionList = new ExecutionParameterCollection();
                this.pendingActions = new BlockingCollection<ExecutionParameters>(this.pendingActionList);
                this.perProfileLastRunStatus = new Dictionary<string, string>();

                this.Setup(config);

                if (this.Configuration.Partitions.ActiveConfigurations.Count() > 1)
                {
                    this.Trace("Controller will walk connector space to detect export partitions");
                    this.detectionMode = PartitionDetectionMode.WalkConnectorSpace;
                }
                else
                {
                    this.Trace("Controller will assume all partitions require export when pending exports are detected");
                    this.detectionMode = PartitionDetectionMode.AssumeAll;
                }

                this.LogInfo($"Starting controller");

                this.internalTask = new Task(() =>
                {
                    try
                    {
                        Thread.CurrentThread.SetThreadName($"Execution thread for {this.ManagementAgentName}");
                        this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        this.Init();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (ThresholdExceededException)
                    {
                    }
                    catch (Exception ex)
                    {
                        this.LogError(ex, "The controller encountered a unrecoverable error");
                    }
                }, this.controllerCancellationTokenSource.Token);

                this.internalTask.Start();

                this.ControlState = ControlState.Running;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred starting the controller");
                this.Stop(false, false, false);
                this.Message = $"Startup error: {ex.Message}";
            }
            finally
            {
                if (gotLock)
                {
                    this.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock));
                }
            }
        }

        private void TryCancelRun()
        {
            try
            {
                if (this.ma != null && !this.ma.IsIdle())
                {
                    this.LogInfo("Requesting sync engine to terminate run");
                    this.ma.StopAsync();
                }
                else
                {
                    this.LogInfo("Canceling current job");
                    this.jobCancellationTokenSource?.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Cannot cancel run");
            }
        }

        public void Stop(bool cancelRun, bool waitForInternalTask, bool thresholdExceeded)
        {
            bool gotLock = false;

            try
            {
                this.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock), this.controllerCancellationTokenSource ?? new CancellationTokenSource());
                gotLock = true;

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

                this.LogInfo("Stopping controller");
                this.pendingActions?.CompleteAdding();
                this.controllerCancellationTokenSource?.Cancel();

                this.StopTriggers();

                this.LogInfo("Stopped execution triggers");

                if (this.internalTask != null && !this.internalTask.IsCompleted)
                {
                    if (waitForInternalTask)
                    {
                        this.LogInfo("Waiting for cancellation to complete");
                        if (this.internalTask.Wait(TimeSpan.FromSeconds(30)))
                        {
                            this.LogInfo("Cancellation completed");
                        }
                        else
                        {
                            this.LogWarn("Controller internal task did not stop in the allowed time");
                        }
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
                logger.Error(ex, "An error occurred stopping the controller");
                this.Message = $"Stop error: {ex.Message}";
            }
            finally
            {
                this.StopImportTimers();
                this.ExecutionTriggers.Clear();

                this.pendingActionList = null;
                this.pendingActions = null;
                this.internalTask = null;
                this.InternalStatus.Clear();
                this.ControlState = ControlState.Stopped;

                if (thresholdExceeded)
                {
                    this.Message = "Threshold exceeded";
                }

                if (gotLock)
                {
                    this.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock));
                }
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

        private void UpdateExecutionStatus(ControllerState state, string message)
        {
            this.InternalStatus.ExecutionState = state;
            this.InternalStatus.Message = message;
            this.RaiseStateChange();
        }

        private void UpdateExecutionStatus(ControllerState state, string message, string executingRunProfile)
        {
            this.InternalStatus.ExecutionState = state;
            this.InternalStatus.Message = message;
            this.InternalStatus.ExecutingRunProfile = executingRunProfile;
            this.RaiseStateChange();
        }

        private void UpdateExecutionStatus(ControllerState state, string message, string executingRunProfile, string executionQueue)
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
            catch (ThresholdExceededException ex)
            {
                this.LogWarn($"Threshold was exceeded on management agent run profile {this.ExecutingRunProfile}. The controller will be stopped\n{ex.Message}");
                this.SendThresholdExceededMail(ex.RunDetails, ex.Message);
                this.Stop(false, false, true);
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred in an unmanaged run");
            }

            this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();

            this.CheckAndQueueUnmanagedChanges();

            this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();

            this.StartTriggers();

            this.SetupImportTimers();

            this.SetupUnmanagedChangesCheckTimer();

            this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();

            try
            {
                this.LogInfo("Starting action processing queue");
                this.UpdateExecutionStatus(ControllerState.Idle, null, null);

                // ReSharper disable once InconsistentlySynchronizedField
                foreach (ExecutionParameters action in this.pendingActions.GetConsumingEnumerable(this.controllerCancellationTokenSource.Token))
                {
                    this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    this.UpdateExecutionStatus(ControllerState.Waiting, "Staging run", action.RunProfileName, this.GetQueueItemNames(false));

                    if (this.controllerScript.SupportsShouldExecute)
                    {
                        this.Message = "Asking controller script for execution permission";

                        if (!this.controllerScript.ShouldExecute(action.RunProfileName))
                        {
                            this.LogWarn($"Controller script indicated that run profile {action.RunProfileName} should not be executed");
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
            catch (ThresholdExceededException)
            {
            }
            finally
            {
                this.LogInfo("Stopped action processing queue");
            }
        }

        private void TakeLocksAndExecute(ExecutionParameters action)
        {
            ConcurrentBag<SemaphoreSlim> otherLocks = new ConcurrentBag<SemaphoreSlim>();
            bool hasLocalLock = false;

            try
            {
                this.WaitOnUnmanagedRun();

                this.jobCancellationTokenSource = this.CreateJobTokenSource();

                this.UpdateExecutionStatus(ControllerState.Waiting, "Waiting for lock holder to finish", action.RunProfileName);
                this.Wait(MAController.GlobalExclusiveOperationLock, nameof(MAController.GlobalExclusiveOperationLock), this.jobCancellationTokenSource);

                if (action.Exclusive)
                {
                    this.Message = "Waiting to take lock";
                    this.LogInfo($"Entering exclusive mode for {action.RunProfileName}");

                    // Signal all controllers to wait before running their next job
                    this.WaitAndTakeLockWithSemaphore(MAController.GlobalExclusiveOperationLock, MAController.GlobalExclusiveOperationLockSemaphore, nameof(MAController.GlobalExclusiveOperationLock), this.jobCancellationTokenSource);
                    this.HasExclusiveLock = true;

                    this.Message = "Waiting for other MAs to finish";
                    this.LogInfo("Waiting for all MAs to complete");
                    // Wait for all  MAs to finish their current job
                    this.Wait(MAController.AllMaLocalOperationLocks.Values.Select(t => t.AvailableWaitHandle).ToArray<WaitHandle>(), nameof(MAController.AllMaLocalOperationLocks), this.jobCancellationTokenSource);
                }

                if (this.StepRequiresSyncLock(action.RunProfileName))
                {
                    this.Message = "Waiting to take lock";
                    this.LogInfo("Waiting to take sync lock");
                    this.WaitAndTakeLock(MAController.GlobalSynchronizationStepLock, nameof(MAController.GlobalSynchronizationStepLock), this.jobCancellationTokenSource);
                    this.HasSyncLock = true;
                }

                // If another operation in this controller is already running, then wait for it to finish before taking the lock for ourselves
                this.Message = "Waiting to take lock";
                this.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), this.jobCancellationTokenSource);
                hasLocalLock = true;

                if (this.Configuration.LockManagementAgents != null)
                {
                    List<Task> tasks = new List<Task>();

                    foreach (string managementAgent in this.Configuration.LockManagementAgents)
                    {
                        Guid? id = Global.FindManagementAgent(managementAgent, Guid.Empty);

                        if (id == null)
                        {
                            this.LogInfo($"Cannot take lock for management agent {managementAgent} as the management agent cannot be found");
                            continue;
                        }

                        if (id == this.ManagementAgentID)
                        {
                            this.Trace("Not going to wait on own lock!");
                            continue;
                        }

                        tasks.Add(Task.Run(() =>
                        {
                            Thread.CurrentThread.SetThreadName($"Get localOperationLock on {managementAgent} for {this.ManagementAgentName}");
                            SemaphoreSlim h = MAController.AllMaLocalOperationLocks[id.Value];
                            this.WaitAndTakeLock(h, $"localOperationLock for {managementAgent}", this.jobCancellationTokenSource);
                            otherLocks.Add(h);
                            this.HasForeignLock = true;
                        }, this.jobCancellationTokenSource.Token));
                    }

                    if (tasks.Any())
                    {
                        this.Message = $"Waiting to take locks";
                        Task.WaitAll(tasks.ToArray(), this.jobCancellationTokenSource.Token);
                    }
                }

                // Grab the staggered execution lock, and hold for x seconds
                // This ensures that no MA can start within x seconds of another MA
                // to avoid deadlock conditions
                this.Message = "Preparing to start management agent";
                bool tookStaggerLock = false;
                try
                {
                    this.WaitAndTakeLock(MAController.GlobalStaggeredExecutionLock, nameof(MAController.GlobalStaggeredExecutionLock), this.jobCancellationTokenSource);
                    tookStaggerLock = true;
                    this.Wait(RegistrySettings.ExecutionStaggerInterval, nameof(RegistrySettings.ExecutionStaggerInterval), this.jobCancellationTokenSource);
                }
                finally
                {
                    if (tookStaggerLock)
                    {
                        this.ReleaseLock(MAController.GlobalStaggeredExecutionLock, nameof(MAController.GlobalStaggeredExecutionLock));
                    }
                }

                this.Execute(action, this.jobCancellationTokenSource);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ThresholdExceededException ex)
            {
                this.LogWarn($"Threshold was exceeded on management agent run profile {this.ExecutingRunProfile}. The controller will be stopped\n{ex.Message}");
                this.SendThresholdExceededMail(ex.RunDetails, ex.Message);
                this.Stop(false, false, true);
            }
            finally
            {
                this.UpdateExecutionStatus(ControllerState.Idle, null, null);

                if (hasLocalLock)
                {
                    // Reset the local lock so the next operation can run
                    this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));
                }

                if (this.HasSyncLock)
                {
                    this.ReleaseLock(MAController.GlobalSynchronizationStepLock, nameof(MAController.GlobalSynchronizationStepLock));
                    this.HasSyncLock = false;
                }

                if (this.HasExclusiveLock)
                {
                    // Reset the global lock so pending operations can run
                    this.ReleaseLock(MAController.GlobalExclusiveOperationLock, nameof(MAController.GlobalExclusiveOperationLock));
                    this.HasExclusiveLock = false;
                }

                if (otherLocks.Any())
                {
                    foreach (SemaphoreSlim e in otherLocks)
                    {
                        this.ReleaseLock(e, "foreign localOperationLock");
                    }

                    this.HasForeignLock = false;
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
            bool hasLocalLock = false;

            try
            {
                // If another operation in this controller is already running, then wait for it to finish
                this.WaitAndTakeLock(this.localOperationLock, nameof(this.localOperationLock), this.controllerCancellationTokenSource);
                hasLocalLock = true;

                this.Trace("Checking for unmanaged changes");
                RunDetails run = this.ma.GetLastRun();

                if (run == null || this.lastRunNumber == run.RunNumber)
                {
                    return;
                }

                this.Trace($"Unprocessed changes detected. Last recorded run: {this.lastRunNumber}. Last run in sync engine: {run.RunNumber}");

                this.lastRunNumber = run.RunNumber;

                foreach (PartitionConfiguration c in this.GetPartitionsRequiringExport())
                {
                    if (c.ExportRunProfileName != null)
                    {
                        ExecutionParameters p = new ExecutionParameters(c.ExportRunProfileName);
                        this.AddPendingActionIfNotQueued(p, "Pending export check");
                    }
                }

                if (run?.StepDetails != null)
                {
                    foreach (StepDetails step in run.StepDetails)
                    {
                        if (step.HasUnconfirmedExports())
                        {
                            PartitionConfiguration c = this.Configuration.Partitions.GetActiveItemOrNull(step.StepDefinition.Partition);

                            if (c != null)
                            {
                                this.AddPendingActionIfNotQueued(new ExecutionParameters(c.ConfirmingImportRunProfileName), "Unconfirmed export check");
                            }
                        }
                    }
                }

                foreach (PartitionConfiguration c in this.GetPartitionsRequiringSync())
                {
                    if (c.ExportRunProfileName != null)
                    {
                        ExecutionParameters p = new ExecutionParameters(c.DeltaSyncRunProfileName);
                        this.AddPendingActionIfNotQueued(p, "Staged import check");
                    }
                }
            }
            finally
            {
                if (hasLocalLock)
                {
                    // Reset the local lock so the next operation can run
                    this.ReleaseLock(this.localOperationLock, nameof(this.localOperationLock));
                }
            }
        }

        private void MAController_SyncComplete(object sender, SyncCompleteEventArgs e)
        {
            if (e.TargetMA != this.ma.ID)
            {
                return;
            }

            this.Trace($"Got sync complete message from {e.SendingMAName}");

            foreach (PartitionConfiguration c in this.GetPartitionsRequiringExport())
            {
                if (c.ExportRunProfileName != null)
                {
                    ExecutionParameters p = new ExecutionParameters(c.ExportRunProfileName);
                    this.AddPendingActionIfNotQueued(p, "Synchronization on " + e.SendingMAName);
                }
            }
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
            List<PartitionConfiguration> activePartitions = this.Configuration.Partitions.ActiveConfigurations.ToList();
            Stopwatch watch = Stopwatch.StartNew();

            int activePartitionCount = activePartitions.Count;

            HashSet<Guid> partitionsDetected = new HashSet<Guid>();
            int objectCount = 0;

            foreach (CSObject cs in enumerator.Invoke())
            {
                objectCount++;

                if (this.detectionMode == PartitionDetectionMode.AssumeAll)
                {
                    return activePartitions;
                }

                if (cs.PartitionGuid == null)
                {
                    this.LogWarn($"CSObject did not have a partition ID {cs.DN}");
                    continue;
                }

                if (partitionsDetected.Add(cs.PartitionGuid.Value))
                {
                    this.Trace($"Found partition requiring {mode} on {cs.PartitionGuid}");

                    if (partitionsDetected.Count >= activePartitionCount)
                    {
                        this.Trace($"All active partitions require {mode}");
                        break;
                    }
                }
            }

            watch.Stop();

            this.Trace($"Iterated through {objectCount} objects to find {partitionsDetected.Count} partitions needing {mode} in {watch.Elapsed}");

            return activePartitions.Where(t => partitionsDetected.Contains(t.ID));
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
                        this.LogWarn($"Received empty run profile from trigger {trigger.DisplayName}");
                        return;
                    }
                }

                this.AddPendingActionIfNotQueued(e.Parameters, trigger.DisplayName);
            }
            catch (Exception ex)
            {
                this.LogError(ex, $"The was an unexpected error processing an incoming trigger from {trigger?.DisplayName}");
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
                        this.LogInfo($"Dropping pending action request from '{source}' as no run profile name or run profile type was specified");
                        return;
                    }

                    if (p.PartitionID != Guid.Empty)
                    {
                        p.RunProfileName = this.Configuration.GetRunProfileName(p.RunProfileType, p.PartitionID);

                        if (p.RunProfileName == null)
                        {
                            this.LogInfo($"Dropping {p.RunProfileType} request from '{source}' as no matching run profile could be found in the specified partition {p.PartitionID}");
                            return;
                        }
                    }
                    else
                    {
                        p.RunProfileName = this.Configuration.GetRunProfileName(p.RunProfileType, p.PartitionName);
                    }

                    if (string.IsNullOrWhiteSpace(p.RunProfileName))
                    {
                        this.LogInfo($"Dropping {p.RunProfileType} request from '{source}' as no matching run profile could be found in the management agent partition {p.PartitionName}");
                        return;
                    }
                }

                if (this.pendingActions.ToArray().Contains(p))
                {
                    if (runNext && this.pendingActions.Count > 1)
                    {
                        this.LogInfo($"Moving {p.RunProfileName} to the front of the execution queue");
                        this.pendingActionList.MoveToFront(p);
                    }
                    else
                    {
                        this.LogInfo($"{p.RunProfileName} requested by {source} was ignored because the run profile was already queued");
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
                    this.pendingActions.Add(p, this.controllerCancellationTokenSource.Token);
                    this.pendingActionList.MoveToFront(p);
                    this.LogInfo($"Added {p.RunProfileName} to the front of the execution queue (triggered by: {source})");
                }
                else
                {
                    this.pendingActions.Add(p, this.controllerCancellationTokenSource.Token);
                    this.LogInfo($"Added {p.RunProfileName} to the execution queue (triggered by: {source})");
                }

                this.UpdateExecutionQueueState();

                this.LogInfo($"Current queue: {this.GetQueueItemNames()}");
            }
            catch (OperationCanceledException)
            { }
            catch (Exception ex)
            {
                this.LogError(ex, $"An unexpected error occurred while adding the pending action {p?.RunProfileName}. The event has been discarded");
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
                string current = this.ExecutingRunProfile + "*";

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

        private void TrySendMail(RunDetails r)
        {
            try
            {
                this.SendMail(r);
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Send mail failed");
            }
        }

        private void SendMail(RunDetails r)
        {
            if (!this.ShouldSendMail(r))
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

        private void SendThresholdExceededMail(RunDetails r, string message)
        {
            if (!MessageSender.CanSendMail())
            {
                return;
            }

            MessageSender.SendMessage($"{r.MAName} {r.RunProfileName}: Controller stopped: Threshold exceeded", MessageBuilder.GetMessageBody(r, message));
        }

        private bool ShouldSendMail(RunDetails r)
        {
            if (!MessageSender.CanSendMail())
            {
                return false;
            }

            return Program.ActiveConfig.Settings.MailIgnoreReturnCodes == null ||
                !Program.ActiveConfig.Settings.MailIgnoreReturnCodes.Contains(r.LastStepStatus, StringComparer.OrdinalIgnoreCase);
        }
    }
}