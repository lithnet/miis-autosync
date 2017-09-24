using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ExecutionMonitorViewModel : ViewModelBase<object>, IEventCallBack
    {
        private EventClient client;

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

        public BitmapImage StatusIcon
        {
            get
            {
                switch (this.DisplayState)
                {
                    case "Disabled":
                        return App.GetImageResource("Stop.png");

                    case "Idle":
                       return App.GetImageResource("dot-medium.png");

                    case "Paused":
                        return App.GetImageResource("Pause.png");

                    case "Processing":
                    case "Running":
                        return App.GetImageResource("Run.png");

                    case "Pausing":
                    case "Resuming":
                    case "Starting":
                    case "Waiting":
                    case "Stopping":
                        return App.GetImageResource("Hourglass.png");

                    case "Stopped":
                        return App.GetImageResource("Stop.png");

                    default:
                        return null;
                }
            }
        }

        public bool Disabled { get; private set; }

        public string DisplayName => this.ManagementAgentName ?? "Unknown MA";

        public string ManagementAgentName { get; set; }

        public string ExecutionQueue { get; private set; }

        public string Message { get; private set; }

        public string ExecutingRunProfile { get; private set; }

        [AlsoNotifyFor(nameof(LastRun), nameof(DisplayIcon))]
        public string LastRunProfileResult { get; private set; }

        [AlsoNotifyFor(nameof(LastRun), nameof(DisplayIcon))]
        public string LastRunProfileName { get; private set; }

        public string LastRun => this.LastRunProfileName == null ? null : $"{this.LastRunProfileName}: {this.LastRunProfileResult}";

        public string DisplayState { get; private set; }

        public ControlState ControlState { get; private set; }

        public ControllerState ExecutionState { get; private set; }

        public ObservableCollection<LoggedMessageViewModel> DetailMessages { get; private set; }

        public ObservableCollection<RunProfileResultViewModel> RunHistory { get; private set; }

        [AlsoNotifyFor(nameof(LockIcon))]
        public bool HasExclusiveLock { get; private set; }


        [AlsoNotifyFor(nameof(LockIcon))]
        public bool HasSyncLock { get; private set; }

        [AlsoNotifyFor(nameof(LockIcon))]
        public bool HasForeignLock { get; private set; }

        public BitmapImage LockIcon
        {
            get
            {
                if (this.HasExclusiveLock)
                {
                    return App.GetImageResource("Lock.ico");
                }
                else if (this.HasSyncLock)
                {
                    return App.GetImageResource("sLock.ico");
                }
                else if (this.HasForeignLock)
                {
                    return App.GetImageResource("fLock.ico");
                }
                else
                {
                    return null;
                }
            }
        }


        public void MAStatusChanged(MAStatus status)
        {
            this.Message = status.Message;
            this.ExecutingRunProfile = status.ExecutingRunProfile;
            this.ExecutionQueue = status.ExecutionQueue;
            this.DisplayState = status.DisplayState;
            this.ControlState = status.ControlState;
            this.ExecutionState = status.ExecutionState;
            this.HasExclusiveLock = status.HasExclusiveLock;
            this.HasSyncLock = status.HasSyncLock;
            this.HasForeignLock = status.HasForeignLock;

            this.Disabled = this.ControlState == ControlState.Disabled;
        }

        public new BitmapImage DisplayIcon => this.lastRunResult?.DisplayIcon;

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

            this.MenuItems.Add(new MenuItemViewModel()
            {
                Header = "Start",
                Icon = App.GetImageResource("Run.ico"),
                Command = new DelegateCommand(t => this.Start(), t => this.CanStart()),
            });

            this.MenuItems.Add(new MenuItemViewModel()
            {
                Header = "Stop",
                Icon = App.GetImageResource("Stop.ico"),
                Command = new DelegateCommand(t => this.Stop(false), t => this.CanStop()),
            });

            this.MenuItems.Add(new MenuItemViewModel()
            {
                Header = "Stop and cancel run",
                Icon = App.GetImageResource("Stop.ico"),
                Command = new DelegateCommand(t => this.Stop(true), t => this.CanStop() && this.CanCancelRun()),
            });

            this.MenuItems.Add(new MenuItemViewModel()
            {
                Header = "Cancel run",
                Icon = App.GetImageResource("Cancel.ico"),
                Command = new DelegateCommand(t => this.CancelRun(), t => this.CanCancelRun()),
            });

            try
            {
                MenuItemViewModel addrp = new MenuItemViewModel()
                {
                    Header = "Add run profile to execution queue",
                    Command = new DelegateCommand(t => { }, t => this.CanAddToExecutionQueue())
                };

                ConfigClient c = App.GetDefaultConfigClient();
                c.InvokeThenClose(u =>
                {
                    foreach (string rp in c.GetManagementAgentRunProfileNames(this.ManagementAgentID, true).OrderBy(t => t))
                    {
                        addrp.MenuItems.Add(new MenuItemViewModel()
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

            if (status != null)
            {
                this.MAStatusChanged(status);
            }
        }

        private void InnerChannel_Faulted(object sender, EventArgs e)
        {
            Trace.WriteLine($"Closing faulted event channel on client side for {this.ManagementAgentName}/{this.ManagementAgentID}");
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
            this.IsConnected = false;
            this.DisplayState = "Disconnected";
            this.client.InnerChannel.Closed -= this.InnerChannel_Closed;
            this.client.InnerChannel.Faulted -= this.InnerChannel_Faulted;

            if (this.faultedCount > 0)
            {
                this.CleanupAndRestartClient();
            }
        }

        private int faultedCount;

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
