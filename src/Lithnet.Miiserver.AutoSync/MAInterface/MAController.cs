using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Miiserver.Client;
using NLog;
using Timer = System.Timers.Timer;

namespace Lithnet.Miiserver.AutoSync
{
    internal class MAController
    {
        private static Logger rawLogger = LogManager.GetCurrentClassLogger();

        public event EventHandler<MessageLoggedEventArgs> MessageLogged
        {
            add => this.logger.MessageLogged += value;
            remove => this.logger.MessageLogged -= value;
        }

        internal event EventHandler<RunProfileExecutionCompleteEventArgs> RunProfileExecutionComplete;

        private SemaphoreSlim serviceControlLock;
        private Timer unmanagedChangesCheckTimer;
        private CancellationTokenSource controllerCancellationTokenSource;
        private ManagementAgent ma;
        private Task internalTask;
        private MAControllerPerfCounters counters;
        private int inUnmangedChangesTimer;
        private Dictionary<Guid, Timer> importCheckTimers = new Dictionary<Guid, Timer>();
        private ControllerLogger logger;
        private ExecutionController execController;

        internal ExecutionQueue Queue { get; set; }

        internal MAStatus State { get; }

        private TriggerController Triggers { get; set; }

        public MAControllerConfiguration Configuration { get; private set; }

        public string ManagementAgentName => this.ma?.Name;

        public Guid ManagementAgentID => this.ma?.ID ?? Guid.Empty;

        public MAController(ManagementAgent ma)
        {
            this.controllerCancellationTokenSource = new CancellationTokenSource();
            this.ma = ma;
            this.State = new MAStatus(this.ma.Name, this.ma.ID);
            this.State.ControlState = ControlState.Stopped;
            this.serviceControlLock = new SemaphoreSlim(1, 1);
            this.counters = new MAControllerPerfCounters(ma.Name);
            this.logger = new ControllerLogger(this.ma.Name, this.ma.ID, this.controllerCancellationTokenSource.Token);
        }

        private void SetupUnmanagedChangesCheckTimer()
        {
            this.unmanagedChangesCheckTimer = new Timer();
            this.unmanagedChangesCheckTimer.Elapsed += this.UnmanagedChangesCheckTimer_Elapsed;
            this.unmanagedChangesCheckTimer.AutoReset = true;
            this.unmanagedChangesCheckTimer.Interval = Global.RandomizeOffset(RegistrySettings.UnmanagedChangesCheckInterval.TotalMilliseconds);
            this.unmanagedChangesCheckTimer.Start();
        }

        private void UnmanagedChangesCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.State.ControlState != ControlState.Running)
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
                this.execController.CheckAndQueueUnmanagedChanges();
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
                    if (this.State.ControlState != ControlState.Running)
                    {
                        return;
                    }

                    if (!timerIntervalReset)
                    {
                        t.Interval = interval;
                        timerIntervalReset = true;
                    }

                    this.Queue.Add(new ExecutionParameters(p.ScheduledImportRunProfileName), $"Import timer on {p.Name}");
                };

                t.Start();
                this.logger.Trace($"Initialized import timer for partition {p.Name} at interval of {t.Interval}");
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

        public void Start(MAControllerConfiguration config)
        {
            if (!this.ma.Name.Equals(config.ManagementAgentName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Configuration was provided for the management agent {config.ManagementAgentName} for a controller configured for {this.ma.Name}");
            }

            if (this.State.ControlState == ControlState.Running)
            {
                rawLogger.Trace($"Ignoring request to start {config.ManagementAgentName} as it is already running");
                return;
            }

            if (this.State.ControlState != ControlState.Stopped && this.State.ControlState != ControlState.Disabled)
            {
                throw new InvalidOperationException($"Cannot start a controller that is in the {this.State.ControlState} state");
            }

            this.controllerCancellationTokenSource = new CancellationTokenSource();

            if (config.Version == 0 || config.IsMissing || config.Disabled)
            {
                rawLogger.Info($"Ignoring start request as management agent {config.ManagementAgentName} is disabled or unconfigured");
                this.State.ControlState = ControlState.Disabled;
                return;
            }

            bool gotLock = false;

            try
            {
                rawLogger.Info($"Preparing to start controller for {config.ManagementAgentName}");
                LockController.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock), this.controllerCancellationTokenSource, this.ManagementAgentName);
                gotLock = true;

                this.Configuration = config;
                this.State.Reset();
                this.State.ActiveVersion = config.Version;
                this.State.ControlState = config.Disabled ? ControlState.Disabled : ControlState.Starting;

                this.Queue = new ExecutionQueue(config, this.logger, this.State, this.ma.RunProfiles);

                this.execController = new ExecutionController(this.ma, this.State, config, this.logger, this.Queue, this.counters, this.controllerCancellationTokenSource);
                this.execController.BeforeExecutionStartImport += this.ExecController_BeforeExecutionStartImport;
                this.execController.RunProfileExecutionComplete += this.ExecController_RunProfileExecutionComplete;

                this.Triggers = new TriggerController(config, this.logger, this.Queue, this.controllerCancellationTokenSource.Token);
                this.Triggers.Attach(config.Triggers);

                this.counters.Start();

                this.logger.LogInfo($"Starting controller");

                this.internalTask = new Task(() =>
                {
                    try
                    {
                        Thread.CurrentThread.SetThreadName($"Execution thread for {this.ManagementAgentName}");
                        this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        this.InitializeController();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (ThresholdExceededException ex)
                    {
                        this.HandleThresholdExceededException(ex);
                    }
                    catch (UnexpectedChangeException ex)
                    {
                        this.HandleUnexpectedChangeException(ex);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "The controller encountered a unrecoverable error");
                        this.StopOnError("Unrecoverable error");
                    }
                }, this.controllerCancellationTokenSource.Token);

                this.internalTask.Start();

                this.State.ControlState = ControlState.Running;
            }
            catch (Exception ex)
            {
                rawLogger.Error(ex, "An error occurred starting the controller");
                this.StopOnError($"Startup error: {ex.Message}");
            }
            finally
            {
                if (gotLock)
                {
                    LockController.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock), this.ManagementAgentName);
                }
            }
        }

        private void ExecController_RunProfileExecutionComplete(object sender, RunProfileExecutionCompleteEventArgs e)
        {
            this.RunProfileExecutionComplete?.Invoke(this, e);
        }

        private void ExecController_BeforeExecutionStartImport(object sender, BeforeExecutionStartEventArgs e)
        {
            this.logger.Trace($"Import step detected on partition {e.PartitionID}. Resetting timer");
            this.ResetImportTimerOnImport(e.PartitionID);
        }

        public void Stop(bool cancelRun)
        {
            this.StopInternal(true);

            if (cancelRun)
            {
                this.execController?.TryCancelRun(true);
            }
        }

        private void StopOnUnexpectedChangeException(string stopMessage)
        {
            this.StopInternal(false);
            this.State.HasError = true;
            this.State.Message = stopMessage;
        }

        private void StopOnThresholdExceededException(string stopMessage)
        {
            this.StopInternal(false);
            this.State.ThresholdExceeded = true;
            this.State.Message = stopMessage;
        }

        private void StopOnError(string stopMessage)
        {
            this.StopInternal(false);
            this.State.HasError = true;
            this.State.Message = stopMessage;
        }

        private void StopInternal(bool waitForInternalTask)
        {
            bool gotLock = false;

            try
            {
                LockController.WaitAndTakeLock(this.serviceControlLock, nameof(this.serviceControlLock), this.controllerCancellationTokenSource ?? new CancellationTokenSource(), this.ManagementAgentName);
                gotLock = true;

                if (this.State.ControlState == ControlState.Stopped || this.State.ControlState == ControlState.Disabled)
                {
                    return;
                }

                if (this.State.ControlState == ControlState.Stopping)
                {
                    return;
                }

                this.State.ControlState = ControlState.Stopping;

                this.logger.LogInfo("Stopping controller");
                this.Queue?.Reset();
                this.controllerCancellationTokenSource?.Cancel();

                this.Triggers.Stop();

                this.logger.LogInfo("Stopped execution triggers");

                if (this.internalTask != null && !this.internalTask.IsCompleted)
                {
                    if (waitForInternalTask)
                    {
                        this.logger.LogInfo("Waiting for cancellation to complete");
                        if (this.internalTask.Wait(TimeSpan.FromSeconds(30)))
                        {
                            this.logger.LogInfo("Cancellation completed");
                        }
                        else
                        {
                            this.logger.LogWarn("Controller internal task did not stop in the allowed time");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                rawLogger.Error(ex, "An error occurred stopping the controller");
                this.State.Message = $"Stop error: {ex.Message}";
            }
            finally
            {
                if (this.execController != null)
                {
                    this.execController.Unregister();
                    this.execController.BeforeExecutionStartImport -= this.ExecController_BeforeExecutionStartImport;
                    this.execController.RunProfileExecutionComplete -= this.ExecController_RunProfileExecutionComplete;
                    this.execController = null;
                }

                this.StopImportTimers();
                this.counters.Stop();
                this.Queue = null;
                this.internalTask = null;
                this.State.Reset();
                this.State.ControlState = ControlState.Stopped;
                
                if (gotLock)
                {
                    LockController.ReleaseLock(this.serviceControlLock, nameof(this.serviceControlLock), this.ManagementAgentName);
                }
            }
        }

        public void CancelRun()
        {
            this.execController?.TryCancelRun(true);
        }

        private void InitializeController()
        {
            this.execController.WaitOnUnmanagedRun();

            this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();

            this.execController.CheckAndQueueUnmanagedChanges();

            this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();

            this.Triggers.Start();

            this.SetupImportTimers();

            this.SetupUnmanagedChangesCheckTimer();

            this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();

            this.RunActionProcessingQueue();
        }

        private void RunActionProcessingQueue()
        {
            try
            {
                this.logger.LogInfo("Starting action processing queue");
                this.State.ExecutionState = ControllerState.Idle;

                foreach (ExecutionParameters action in this.Queue.GetConsumingEnumerable(this.controllerCancellationTokenSource.Token))
                {
                    this.controllerCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    this.counters.CurrentQueueLength.Decrement();
                    this.Queue.StagedRun = action;

                    this.State.UpdateExecutionStatus(ControllerState.Waiting, "Staging run", action.RunProfileName, this.Queue.GetQueueItemNames());

                    this.execController.TakeLocksAndExecute(action);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.logger.LogInfo("Stopped action processing queue");
            }
        }

        private void HandleUnexpectedChangeException(UnexpectedChangeException ex)
        {
            if (ex.ShouldTerminateService)
            {
                this.logger.LogWarn($"Controller script indicated that service should immediately stop. Run profile {this.State.ExecutingRunProfile}");
                Program.Engine.Stop(true);
            }
            else
            {
                this.logger.LogWarn($"Controller indicated that management agent controller should stop further processing on this MA. Run Profile {this.State.ExecutingRunProfile}");
                this.StopOnUnexpectedChangeException("Controller script detected an unexpected change");
            }
        }

        private void HandleThresholdExceededException(ThresholdExceededException ex)
        {
            this.logger.LogWarn($"Threshold was exceeded on management agent run profile {this.State.ExecutingRunProfile}. The controller will be stopped\n{ex.Message}");
            RunDetailParser.SendThresholdExceededMail(ex.RunDetails, ex.Message);
            this.StopOnThresholdExceededException("Threshold exceeded");
        }
    }
}