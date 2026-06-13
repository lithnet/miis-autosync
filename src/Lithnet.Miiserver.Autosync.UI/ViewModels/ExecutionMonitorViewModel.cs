using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Lithnet.Common.Presentation;
using PropertyChanged;
using Timer = System.Timers.Timer;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ExecutionMonitorViewModel : ViewModelBase<object>, IEventCallBack
    {
        private EventClient client;

        private Timer pingTimer;

        private int faultedCount;

        public ExecutionMonitorViewModel(KeyValuePair<Guid, string> ma)
            : base(ma)
        {
            this.ManagementAgentID = ma.Key;
            this.ManagementAgentName = ma.Value;
            this.DetailMessages = new ObservableCollection<LoggedMessageViewModel>();
            this.RunHistory = new ObservableCollection<RunProfileResultViewModel>();
            this.SubscribeToStateChanges();
            this.PopulateMenuItems();
        }

        private Guid ManagementAgentID { get; set; }

        public ObservableCollection<MenuItemViewModelBase> MenuItems { get; set; }

        public bool IsConnected { get; set; }

        public Wpf.Ui.Controls.SymbolRegular StatusSymbol
        {
            get
            {
                if (this.ErrorState != ErrorState.None)
                {
                    return Wpf.Ui.Controls.SymbolRegular.ErrorCircle24;
                }

                switch (this.DisplayState)
                {
                    case "Disabled":
                        return Wpf.Ui.Controls.SymbolRegular.Prohibited24;

                    case "Idle":
                        return Wpf.Ui.Controls.SymbolRegular.Circle24;

                    case "Paused":
                        return Wpf.Ui.Controls.SymbolRegular.Pause24;

                    case "Processing":
                    case "Running":
                        return Wpf.Ui.Controls.SymbolRegular.Play24;

                    case "Pausing":
                    case "Resuming":
                    case "Starting":
                    case "Waiting":
                    case "Stopping":
                        return Wpf.Ui.Controls.SymbolRegular.ArrowClockwise24;

                    case "Stopped":
                        return Wpf.Ui.Controls.SymbolRegular.Stop24;

                    default:
                        return Wpf.Ui.Controls.SymbolRegular.Empty;
                }
            }
        }

        public System.Windows.Media.Brush StatusBrush
        {
            get
            {
                if (this.ErrorState != ErrorState.None)
                {
                    return StatusBrushes.Error;
                }

                switch (this.DisplayState)
                {
                    case "Processing":
                    case "Running":
                        return StatusBrushes.Running;

                    case "Paused":
                        return StatusBrushes.Paused;

                    case "Pausing":
                    case "Resuming":
                    case "Starting":
                    case "Waiting":
                    case "Stopping":
                        return StatusBrushes.Transitional;

                    default:
                        return StatusBrushes.Inactive;
                }
            }
        }

        public bool Disabled { get; private set; }

        public string DisplayName => this.ManagementAgentName ?? "Unknown MA";

        public string ManagementAgentName { get; set; }

        public string ExecutionQueue { get; private set; }

        public string Message { get; private set; }

        [AlsoNotifyFor(nameof(StatusSymbol), nameof(StatusBrush))]
        public ErrorState ErrorState { get; private set; }

        public string ExecutingRunProfile { get; private set; }

        [AlsoNotifyFor(nameof(LastRun), nameof(DisplaySymbol), nameof(DisplayBrush))]
        public string LastRunProfileResult { get; private set; }

        [AlsoNotifyFor(nameof(LastRun), nameof(DisplaySymbol), nameof(DisplayBrush))]
        public string LastRunProfileName { get; private set; }

        public string LastRun => this.LastRunProfileName == null ? null : $"{this.LastRunProfileName}: {this.LastRunProfileResult}";

        [AlsoNotifyFor(nameof(StatusSymbol), nameof(StatusBrush))]
        public string DisplayState { get; private set; }

        public ControlState ControlState { get; private set; }

        public ControllerState ExecutionState { get; private set; }

        public ObservableCollection<LoggedMessageViewModel> DetailMessages { get; private set; }

        public ObservableCollection<RunProfileResultViewModel> RunHistory { get; private set; }

        [AlsoNotifyFor(nameof(LockSymbol), nameof(HasLock))]
        public bool HasExclusiveLock { get; private set; }


        [AlsoNotifyFor(nameof(LockSymbol), nameof(HasLock))]
        public bool HasSyncLock { get; private set; }

        [AlsoNotifyFor(nameof(LockSymbol), nameof(HasLock))]
        public bool HasForeignLock { get; private set; }

        public bool HasLock => this.HasExclusiveLock || this.HasSyncLock || this.HasForeignLock;

        public Wpf.Ui.Controls.SymbolRegular LockSymbol
        {
            get
            {
                if (this.HasForeignLock)
                {
                    return Wpf.Ui.Controls.SymbolRegular.LockMultiple24;
                }

                if (this.HasExclusiveLock || this.HasSyncLock)
                {
                    return Wpf.Ui.Controls.SymbolRegular.LockClosed24;
                }

                return Wpf.Ui.Controls.SymbolRegular.Empty;
            }
        }


        public void MAStatusChanged(MAStatus status)
        {
            switch (status.ErrorState)
            {
                case ErrorState.ControllerFaulted:
                    this.Message = "An unexpected error occurred in the controller";
                    break;

                case ErrorState.None:
                    this.Message = status.Message;
                    break;

                case ErrorState.ThresholdExceeded:
                    this.Message = "Threshold exceeded";
                    break;

                case ErrorState.UnexpectedChange:
                    this.Message = "The controller was terminated due to unexpected changes";
                    break;

            }

            this.ExecutingRunProfile = status.ExecutingRunProfile;
            this.ExecutionQueue = status.ExecutionQueue;
            this.DisplayState = status.DisplayState;
            this.ControlState = status.ControlState;
            this.ExecutionState = status.ExecutionState;
            this.HasExclusiveLock = status.HasExclusiveLock;
            this.HasSyncLock = status.HasSyncLock;
            this.HasForeignLock = status.HasForeignLock;
            this.ErrorState = status.ErrorState;

            this.Disabled = this.ControlState == ControlState.Disabled;
        }

        public Wpf.Ui.Controls.SymbolRegular DisplaySymbol => this.lastRunResult?.DisplaySymbol ?? Wpf.Ui.Controls.SymbolRegular.Empty;

        public System.Windows.Media.Brush DisplayBrush => this.lastRunResult?.DisplayBrush;

        public void RunProfileExecutionComplete(RunProfileExecutionCompleteEventArgs e)
        {
            this.AddRunProfileHistory(e);
        }

        public void MessageLogged(MessageLoggedEventArgs e)
        {
            this.AddDetailMessage(e);
        }

        private void AddDetailMessage(MessageLoggedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Message))
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {

                lock (this.DetailMessages)
                {
                    while (this.DetailMessages.Count >= 100)
                    {
                        this.DetailMessages.RemoveAt(this.DetailMessages.Count - 1);
                    }

                    this.DetailMessages.Insert(0, new LoggedMessageViewModel(e));
                }
            });
        }

        private RunProfileResultViewModel lastRunResult;

        private void AddRunProfileHistory(RunProfileExecutionCompleteEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.RunProfileName))
            {
                return;
            }

            RunProfileResultViewModel t = new RunProfileResultViewModel(e);

            this.lastRunResult = t;
            this.LastRunProfileName = t.RunProfileName;
            this.LastRunProfileResult = t.Result;

            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (this.RunHistory)
                {
                    while (this.RunHistory.Count >= 100)
                    {
                        this.RunHistory.RemoveAt(this.RunHistory.Count - 1);
                    }

                    this.RunHistory.Insert(0, t);
                }
            });
        }

        private void PopulateMenuItems()
        {
            this.MenuItems = new ObservableCollection<MenuItemViewModelBase>();

            this.MenuItems.Add(new SymbolMenuItemViewModel()
            {
                Header = "Start",
                Symbol = Wpf.Ui.Controls.SymbolRegular.Play24,
                Command = new DelegateCommand(t => this.Start(), t => this.CanStart()),
            });

            this.MenuItems.Add(new SymbolMenuItemViewModel()
            {
                Header = "Stop",
                Symbol = Wpf.Ui.Controls.SymbolRegular.Stop24,
                Command = new DelegateCommand(t => this.Stop(false), t => this.CanStop()),
            });

            this.MenuItems.Add(new SymbolMenuItemViewModel()
            {
                Header = "Stop and cancel run",
                Symbol = Wpf.Ui.Controls.SymbolRegular.Prohibited24,
                Command = new DelegateCommand(t => this.Stop(true), t => this.CanStop() && this.CanCancelRun()),
            });

            this.MenuItems.Add(new SymbolMenuItemViewModel()
            {
                Header = "Cancel run",
                Symbol = Wpf.Ui.Controls.SymbolRegular.DismissCircle24,
                Command = new DelegateCommand(t => this.CancelRun(), t => this.CanCancelRun()),
            });

            try
            {
                SymbolMenuItemViewModel addrp = new SymbolMenuItemViewModel()
                {
                    Header = "Add run profile to execution queue",
                    Symbol = Wpf.Ui.Controls.SymbolRegular.AddCircle24,
                    Command = new DelegateCommand(t => { }, t => this.CanAddToExecutionQueue())
                };

                ConfigClient c = App.GetDefaultConfigClient();
                c.InvokeThenClose(u =>
                {
                    foreach (string rp in c.GetManagementAgentRunProfileNames(this.ManagementAgentID, true).OrderBy(t => t))
                    {
                        addrp.MenuItems.Add(new SymbolMenuItemViewModel()
                        {
                            Header = rp,
                            Command = new DelegateCommand(t => this.AddToExecutionQueue(rp)),
                        });
                    }
                });

                if (addrp.MenuItems.Count > 0)
                {
                    this.MenuItems.Add(new SeparatorViewModel());
                    this.MenuItems.Add(addrp);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error getting the run profile list");
                Trace.WriteLine(ex.ToString());
            }
        }

        private void AddToExecutionQueue(string runProfileName)
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = App.GetDefaultConfigClient();
                    c.AddToExecutionQueue(this.ManagementAgentID, runProfileName);
                }
                catch (EndpointNotFoundException ex)
                {
                    Trace.WriteLine(ex.ToString());
                    MessageBox.Show($"Could not contact the AutoSync service", "AutoSync service unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not add the run profile to the execution queue\r\n\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Trace.WriteLine("Could not add the run profile to the execution queue");
                    Trace.WriteLine(ex.ToString());
                }
            });
        }

        private void Stop(bool cancelRun)
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = App.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.Stop(this.ManagementAgentID, cancelRun));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    MessageBox.Show($"Could not stop the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private bool CanAddToExecutionQueue()
        {
            return this.ControlState == ControlState.Running;
        }

        private bool CanStop()
        {
            return this.ControlState == ControlState.Running;
        }

        private bool CanCancelRun()
        {
            return this.ExecutionState != ControllerState.Idle;
        }

        private void CancelRun()
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = App.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.CancelRun(this.ManagementAgentID));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    MessageBox.Show($"Could not cancel the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private void Start()
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = App.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.Start(this.ManagementAgentID));
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    MessageBox.Show($"Could not start the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private bool CanStart()
        {
            return this.ControlState == ControlState.Stopped;
        }

        private void SubscribeToStateChanges()
        {
            Trace.WriteLine($"Attempting to open event channel for {this.ManagementAgentName}/{this.ManagementAgentID}");
            InstanceContext i = new InstanceContext(this);
            this.IsConnected = false;

            while (!this.IsConnected)
            {
                try
                {
                    this.DisplayState = "Disconnected";
                    this.client = App.GetDefaultEventClient(i);
                    this.client.Open();
                    this.client.Register(this.ManagementAgentID);
                    this.IsConnected = true;
                    this.faultedCount = 0;
                }
                catch (TimeoutException)
                {
                    this.client.Abort();
                    Trace.WriteLine("Timeout connecting to server");
                    Thread.Sleep(UserSettings.ReconnectInterval);
                }
                catch (Exception ex)
                {
                    this.client.Abort();
                    Trace.WriteLine("Error connecting to server");
                    Trace.WriteLine(ex);
                    Thread.Sleep(UserSettings.ReconnectInterval);
                }
            }

            Trace.WriteLine($"Registered event channel for {this.ManagementAgentName}/{this.ManagementAgentID}");

            this.client.InnerChannel.Closed += this.InnerChannel_Closed;
            this.client.InnerChannel.Faulted += this.InnerChannel_Faulted;

            Debug.WriteLine($"Requesting full update for {this.ManagementAgentName}/{this.ManagementAgentID}");
            MAStatus status = this.client.GetFullUpdate(this.ManagementAgentID);
            Debug.WriteLine($"Got full update from {this.ManagementAgentName}/{this.ManagementAgentID}");

            this.StartPingTimer();

            if (status != null)
            {
                this.MAStatusChanged(status);
            }
        }

        private void StartPingTimer()
        {
            if (!App.IsConnectedToLocalhost())
            {
                this.pingTimer = new Timer();
                this.pingTimer.Interval = TimeSpan.FromSeconds(60).TotalMilliseconds;
                this.pingTimer.Elapsed += this.PingTimerElapsed;
                this.pingTimer.Start();
            }
        }

        private void StopPingTimer()
        {
            this.pingTimer?.Stop();
        }

        private void PingTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.IsConnected)
            {
                if (!this.client.Ping(this.ManagementAgentID))
                {
                    Trace.WriteLine("Server ping failed. Restarting client");
                    this.CleanupAndRestartClient();
                }
            }
        }
        
        private void InnerChannel_Faulted(object sender, EventArgs e)
        {
            Trace.WriteLine($"Closing faulted event channel on client side for {this.ManagementAgentName}/{this.ManagementAgentID}");
            this.StopPingTimer();
            this.faultedCount++;
            try
            {
                this.client.Abort();
            }
            catch
            {
            }
        }

        private void InnerChannel_Closed(object sender, EventArgs e)
        {
            Trace.WriteLine($"Closing event channel on client side for {this.ManagementAgentName}/{this.ManagementAgentID}");
            this.StopPingTimer();
            this.IsConnected = false;
            this.DisplayState = "Disconnected";
            this.client.InnerChannel.Closed -= this.InnerChannel_Closed;
            this.client.InnerChannel.Faulted -= this.InnerChannel_Faulted;

            if (this.faultedCount > 0)
            {
                this.CleanupAndRestartClient();
            }
        }

        private void CleanupAndRestartClient()
        {
            if (this.faultedCount > 5)
            {
                throw new ApplicationException($"An unrecoverable error occurred trying to reestablish the monitor channel for {this.ManagementAgentName}");
            }

            this.SubscribeToStateChanges();
        }
    }
}
