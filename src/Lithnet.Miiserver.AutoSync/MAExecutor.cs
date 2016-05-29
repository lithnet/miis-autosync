using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using Lithnet.Miiserver.Client;
using Lithnet.ResourceManagement.Client;
using System.Xml;
using Lithnet.Logging;
using System.Configuration;
using System.Net.Mail;

namespace Lithnet.Miiserver.AutoSync
{
    public class MAExecutor
    {
        protected static object globalStaggeredExecutionLock;
        protected static ManualResetEvent globalExclusiveOperationLock;
        protected static object globalSynchronizationStepLock;
        protected static List<ManualResetEvent> allMaLocalOperationLocks;

        public static event SyncCompleteEventHandler SyncComplete;
        public delegate void SyncCompleteEventHandler(object sender, SyncCompleteEventArgs e);
        private ManagementAgent ma;
        private BlockingCollection<ExecutionParameters> pendingActions;
        private ManualResetEvent localOperationLock;
        private System.Timers.Timer ImportCheckTimer;
        private System.Timers.Timer UnmanagedChangesCheckTimer;

        private Dictionary<string, string> perProfileLastRunStatus;

        public MAConfigParameters Configuration { get; private set; }

        public string ExecutingRunProfile { get; private set; }

        private List<IMAExecutionTrigger> ExecutionTriggers { get; set; }

        private MAController controller;

        private CancellationTokenSource token;

        static MAExecutor()
        {
            MAExecutor.globalSynchronizationStepLock = new object();
            MAExecutor.globalStaggeredExecutionLock = new object();
            MAExecutor.globalExclusiveOperationLock = new ManualResetEvent(true);
            MAExecutor.allMaLocalOperationLocks = new List<ManualResetEvent>();
        }

        public MAExecutor(ManagementAgent ma, MAConfigParameters profiles)
        {
            this.ma = ma;
            this.pendingActions = new BlockingCollection<ExecutionParameters>();
            this.perProfileLastRunStatus = new Dictionary<string, string>();
            this.ExecutionTriggers = new List<IMAExecutionTrigger>();
            this.Configuration = profiles;
            this.token = new CancellationTokenSource();
            this.controller = new MAController(ma);
            this.localOperationLock = new ManualResetEvent(true);
            MAExecutor.allMaLocalOperationLocks.Add(this.localOperationLock);
            MAExecutor.SyncComplete += this.MAExecutor_SyncComplete;
            this.SetupImportSchedule();
            this.SetupUnmanagedChangesCheckTimer();
        }

        private void SetupUnmanagedChangesCheckTimer()
        {
            this.UnmanagedChangesCheckTimer = new System.Timers.Timer();
            this.UnmanagedChangesCheckTimer.Elapsed += this.UnmanagedChangesCheckTimer_Elapsed;
            this.UnmanagedChangesCheckTimer.AutoReset = true;
            this.UnmanagedChangesCheckTimer.Interval = Global.RandomizeOffset(Settings.UnmanagedChangesCheckInterval);
            this.UnmanagedChangesCheckTimer.Start();
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
                    this.ImportCheckTimer = new System.Timers.Timer();
                    this.ImportCheckTimer.Elapsed += this.ImportCheckTimer_Elapsed;
                    int importSeconds = this.Configuration.AutoImportIntervalMinutes > 0 ? this.Configuration.AutoImportIntervalMinutes * 60 : MAExecutionTriggerDiscovery.GetTriggerInterval(this.ma);
                    this.ImportCheckTimer.Interval = Global.RandomizeOffset(importSeconds * 1000);
                    this.ImportCheckTimer.AutoReset = true;
                    Logger.WriteLine("{0}: Starting import interval timer. Imports will be queued if they have not been run for {1} seconds", this.ma.Name, importSeconds);
                    this.ImportCheckTimer.Start();
                }
                else
                {
                    Logger.WriteLine("{0}: Import schedule not enabled", this.ma.Name);
                }
            }
            else
            {
                Logger.WriteLine("{0}: Import schedule disabled", this.ma.Name);
            }
        }

        private void ImportCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.ScheduledImportRunProfileName), "Import timer");
        }

        private void ResetImportTimerOnImport()
        {
            if (this.ImportCheckTimer != null)
            {
                Logger.WriteLine("{0}: Resetting import timer for {1} seconds", this.ma.Name, (int)(this.ImportCheckTimer.Interval / 1000));
                this.ImportCheckTimer.Stop();
                this.ImportCheckTimer.Start();
            }
        }

        public void AttachTrigger(params IMAExecutionTrigger[] triggers)
        {
            if (triggers == null)
            {
                throw new ArgumentNullException("triggers");
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
                    Logger.WriteLine("{0}: Registering execution trigger '{1}'", this.ma.Name, t.Name);
                    t.TriggerExecution += this.notifier_TriggerExecution;
                    t.Start();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("{0}: Could not start execution trigger {1}", this.ma.Name, t.Name);
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
                if (this.HasUnconfirmedExports(d))
                {
                    this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.ConfirmingImportRunProfileName), d.RunProfileName);
                }
            }
        }

        private void QueueFollowUpActionsImport(RunDetails d)
        {
            if (this.HasStagedImports(d))
            {
                this.AddPendingActionIfNotQueued(new ExecutionParameters(this.Configuration.DeltaSyncRunProfileName), d.RunProfileName);
            }
        }

        private void QueueFollowUpActionsSync(RunDetails d)
        {
            SyncCompleteEventHandler registeredHandlers = MAExecutor.SyncComplete;

            if (registeredHandlers == null)
            {
                return;
            }

            foreach (StepDetails s in d.StepDetails)
            {
                if (s.StepDefinition.IsSyncStep)
                {
                    foreach (var item in s.OutboundFlowCounters)
                    {
                        if (item.HasChanges)
                        {
                            SyncCompleteEventArgs args = new SyncCompleteEventArgs();
                            args.SendingMAName = this.ma.Name;
                            args.TargetMA = item.MAID;

                            registeredHandlers(this, args);
                        }
                    }
                }
            }
        }

        private void Execute(ExecutionParameters e)
        {
            try
            {
                // Wait here if any exclusive operations are pending or in progress
                MAExecutor.globalExclusiveOperationLock.WaitOne();

                if (!this.controller.ShouldExecute(e.RunProfileName))
                {
                    Logger.WriteLine("{0}: Controller indicated that run profile {1} should not be executed", this.ma.Name, e.RunProfileName);
                    return;
                }

                this.WaitOnUnmanagedRun();

                if (e.Exclusive)
                {
                    Logger.WriteLine("{0}: Entering exclusive mode for {1}", this.ma.Name, e.RunProfileName);

                    // Signal all executors to wait before running their next job
                    MAExecutor.globalExclusiveOperationLock.Reset();

                    // Wait for all  MAs to finish their current job
                    Logger.WriteLine("{0}: Waiting for running tasks to complete", this.ma.Name);
                    WaitHandle.WaitAll(MAExecutor.allMaLocalOperationLocks.ToArray());
                }

                // If another operation in this executor is already running, then wait for it to finish
                this.localOperationLock.WaitOne();

                // Signal the local lock that an event is running
                this.localOperationLock.Reset();

                // Grab the staggered execution lock, and hold for x seconds
                // This ensures that no MA can start within x seconds of another MA
                // to avoid deadlock conditions
                lock (MAExecutor.globalStaggeredExecutionLock)
                {
                    Thread.Sleep(Settings.ExecutionStaggerInterval * 1000);
                }

                if (this.ma.RunProfiles[e.RunProfileName].RunSteps.Any(t => t.IsImportStep))
                {
                    this.ResetImportTimerOnImport();
                }

                if (this.token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    Logger.WriteLine("{0}: Executing {1}", this.ma.Name, e.RunProfileName);
                    string result = this.ma.ExecuteRunProfile(e.RunProfileName, false, this.token.Token);
                    Logger.WriteLine("{0}: {1} returned {2}", this.ma.Name, e.RunProfileName, result);
                }
                catch (MAExecutionException ex)
                {
                    Logger.WriteLine("{0}: {1} returned {2}", this.ma.Name, e.RunProfileName, ex.Result);
                }

                RunDetails r = this.ma.GetLastRun();
                this.PerformPostRunActions(r);
            }
            catch (OperationCanceledException)
            {
            }
            catch (System.Management.Automation.RuntimeException ex)
            {
                if (ex.InnerException is UnexpectedChangeException)
                {
                    this.ProcessUnexpectedChangeException((UnexpectedChangeException)ex.InnerException);
                }
                else
                {
                    Logger.WriteLine("{0}: Executor encountered an error executing run profile {1}", this.ma.Name, this.ExecutingRunProfile);
                    Logger.WriteException(ex);
                }
            }
            catch (UnexpectedChangeException ex)
            {
                this.ProcessUnexpectedChangeException(ex);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("{0}: Executor encountered an error executing run profile {1}", this.ma.Name, this.ExecutingRunProfile);
                Logger.WriteException(ex);
            }
            finally
            {
                // Reset the local lock so the next operation can run
                this.localOperationLock.Set();

                if (e.Exclusive)
                {
                    // Reset the global lock so pending operations can run
                    MAExecutor.globalExclusiveOperationLock.Set();
                }
            }
        }

        private void WaitOnUnmanagedRun()
        {
            if (!this.ma.IsIdle())
            {
                this.localOperationLock.Reset();

                try
                {
                    Logger.WriteLine("{0}: Waiting on unmanaged run {1} to finish", this.ma.Name, this.ma.ExecutingRunProfileName);

                    if (this.ma.RunProfiles[this.ma.ExecutingRunProfileName].RunSteps.Any(t => t.IsSyncStep))
                    {
                        Logger.WriteLine("{0}: Getting exclusive sync lock for unmanaged run", this.ma.Name);

                        lock (MAExecutor.globalSynchronizationStepLock)
                        {
                            this.ma.Wait(this.token.Token);
                        }
                    }
                    else
                    {
                        this.ma.Wait(this.token.Token);
                    }

                    if (!this.token.IsCancellationRequested)
                    {
                        RunDetails ur = this.ma.GetLastRun();
                        this.PerformPostRunActions(ur);
                    }
                }
                finally
                {
                    this.localOperationLock.Set();
                }
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
                Logger.WriteLine("{0}: Controller indicated that service should immediately stop. Run profile {1}", this.ma.Name, this.ExecutingRunProfile);
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
                Logger.WriteLine("{0}: Controller indicated that management agent executor should stop further processing on this MA. Run Profile {1}", this.ma.Name, this.ExecutingRunProfile);
                this.Stop();
            }
        }

        public Task Start()
        {
            if (this.Configuration.Disabled)
            {
                throw new Exception("Cannot start executor as it is disabled");
            }

            Logger.WriteLine("{0}: Starting executor", this.ma.Name);

            Task t = new Task(() =>
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
                    Logger.WriteLine("{0}: The MAExecutor encountered a unrecoverable error", this.ma.Name);
                    Logger.WriteLine(ex.Message);
                    Logger.WriteLine(ex.StackTrace);
                }
            }, this.token.Token);

            t.Start();

            return t;
        }

        public void Stop()
        {
            if (this.token != null)
            {
                this.token.Cancel();
            }

            if (this.ImportCheckTimer != null)
            {
                this.ImportCheckTimer.Stop();
            }

            foreach (var x in this.ExecutionTriggers)
            {
                x.Stop();
            }
        }

        private void Init()
        {
            if (!this.ma.IsIdle())
            {
                try
                {
                    this.localOperationLock.Reset();
                    Logger.WriteLine("{0}: Waiting for current job to finish", this.ma.Name);
                    this.ma.Wait(this.token.Token);
                }
                finally
                {
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
                       
                        if (this.ma.RunProfiles[action.RunProfileName].RunSteps.Any(t => t.IsSyncStep))
                        {
                            lock (MAExecutor.globalSynchronizationStepLock)
                            {
                                this.Execute(action);
                            }
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

        private void CheckAndQueueUnmanagedChanges()
        {
            if (this.ShouldExport())
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

        private void MAExecutor_SyncComplete(object sender, SyncCompleteEventArgs e)
        {
            if (e.TargetMA == this.ma.ID)
            {
                if (this.ShouldExport())
                {
                    ExecutionParameters p = new ExecutionParameters(this.Configuration.ExportRunProfileName);
                    this.AddPendingActionIfNotQueued(p, "Synchronization on " + e.SendingMAName);
                }
            }
        }

        private void notifier_TriggerExecution(object sender, ExecutionTriggerEventArgs e)
        {
            IMAExecutionTrigger trigger = (IMAExecutionTrigger)sender;
            string runProfile = e.Parameters.RunProfileName;

            if (string.IsNullOrWhiteSpace(e.Parameters.RunProfileName))
            {
                if (e.Parameters.RunProfileType != MARunProfileType.None)
                {
                    runProfile = this.Configuration.GetRunProfileName(e.Parameters.RunProfileType);
                }
                else
                {
                    Logger.WriteLine("{0}: Received empty run profile from trigger {1}", this.ma.Name, trigger.Name);
                    return;
                }
            }

            this.AddPendingActionIfNotQueued(e.Parameters, trigger.Name);
        }

        private void AddPendingActionIfNotQueued(ExecutionParameters p, string source)
        {
            if (string.IsNullOrWhiteSpace(p.RunProfileName))
            {
                if (p.RunProfileType == MARunProfileType.None)
                {
                    return;
                }

                p.RunProfileName = this.Configuration.GetRunProfileName(p.RunProfileType);
            }

            if (!this.pendingActions.Contains(p))
            {
                if (!p.RunProfileName.Equals(this.ExecutingRunProfile, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.WriteLine("{0}: Queuing {1} (triggered by: {2})", this.ma.Name, p.RunProfileName, source);
                    this.pendingActions.Add(p);
                }
            }
        }

        private bool HasUnconfirmedExports(RunDetails d)
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

        private bool HasStagedImports(RunDetails d)
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

        private bool HasUnconfirmedExports(StepDetails s)
        {
            return s?.ExportCounters?.HasChanges ?? false;
        }

        private bool HasUnconfirmedExportsInLastRun()
        {
            return this.HasUnconfirmedExports(this.ma.GetLastRun()?.StepDetails?.FirstOrDefault());
        }

        private bool ShouldExport()
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
                Logger.WriteLine("{0}: Send mail failed", this.ma.Name);
                Logger.WriteException(ex);
            }
        }

        private void SendMail(RunDetails r)
        {
            if (!MAExecutor.ShouldSendMail(r))
            {
                return;
            }

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

            BuildAndSendMessage(r);
        }

        private static bool ShouldSendMail(RunDetails r)
        {
            if (!MAExecutor.CanSendMail())
            {
                return false;
            }

            if (Settings.MailIgnoreReturnCodes != null && Settings.MailIgnoreReturnCodes.Contains(r.LastStepStatus, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool CanSendMail()
        {
            if (!Settings.MailEnabled)
            {
                return false;
            }

            if (!Settings.UseAppConfigMailSettings)
            {
                if (Settings.MailFrom == null || Settings.MailTo == null || Settings.MailServer == null)
                {
                    return false;
                }
            }
            else
            {
                if (Settings.MailTo == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void BuildAndSendMessage(RunDetails r)
        {
            using (MailMessage m = new MailMessage())
            {
                foreach (string address in Settings.MailTo)
                {
                    m.To.Add(address);
                }

                if (!Settings.UseAppConfigMailSettings)
                {
                    m.From = new MailAddress(Settings.MailFrom);
                }

                m.Subject = $"{r.MAName} {r.RunProfileName}: {r.LastStepStatus}";
                m.IsBodyHtml = true;
                m.Body = MessageBuilder.GetMessageBody(r);

                using (SmtpClient client = new SmtpClient())
                {

                    if (!Settings.UseAppConfigMailSettings)
                    {
                        client.Host = Settings.MailServer;
                        client.Port = Settings.MailServerPort;
                    }

                    client.Send(m);
                }
            }
        }
    }
}