using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ExecutionMonitorViewModel : ViewModelBase<string>, IEventCallBack
    {
        private EventClient client;

        public ExecutionMonitorViewModel(string maName)
            : base(maName)
        {
            this.ManagementAgentName = maName;
            this.DetailMessages = new ObservableCollection<string>();
            this.RunHistory = new ObservableCollection<RunProfileResultViewModel>();
            this.SubscribeToStateChanges();
            this.PopulateMenuItems();
        }

        public ObservableCollection<MenuItemViewModelBase> MenuItems { get; set; }

        public BitmapImage StatusIcon
        {
            get
            {
                switch (this.DisplayState)
                {
                    case "Disabled":
                        return App.GetImageResource("Stop.png");

                    case "Idle":
                        return App.GetImageResource("Clock1.png");

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

        public ExecutorState ExecutionState { get; private set; }

        public ObservableCollection<string> DetailMessages { get; private set; }

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
            this.AddDetailMessage(status.Detail);
        }

        public new BitmapImage DisplayIcon => this.lastRunResult?.DisplayIcon;

        public void RunProfileExecutionComplete(RunProfileExecutionCompleteEventArgs e)
        {
            this.AddRunProfileHistory(e);
        }

        private string lastDetail;

        private void AddDetailMessage(string message)
        {
            if (this.lastDetail == message)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            this.lastDetail = message;

            Application.Current.Dispatcher.Invoke(() =>
            {

                lock (this.DetailMessages)
                {
                    while (this.DetailMessages.Count >= 100)
                    {
                        this.DetailMessages.RemoveAt(this.DetailMessages.Count - 1);
                    }

                    this.DetailMessages.Insert(0, $"{DateTime.Now}: {message}");
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

                ConfigClient c = ConfigClient.GetDefaultClient();
                c.InvokeThenClose(u =>
                {
                    foreach (string rp in c.GetManagementAgentRunProfileNames(this.ManagementAgentName, true))
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
            try
            {
                ConfigClient c = ConfigClient.GetDefaultClient();
                c.AddToExecutionQueue(this.ManagementAgentName, runProfileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not add the run profile to the execution queue\r\n\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Trace.WriteLine("Could not add the run profile to the execution queue");
                Trace.WriteLine(ex.ToString());
            }
        }

        private void Stop(bool cancelRun)
        {
            try
            {
                ConfigClient c = ConfigClient.GetDefaultClient();
                c.InvokeThenClose(x => x.Stop(this.ManagementAgentName, cancelRun));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not stop the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            return this.ExecutionState != ExecutorState.Idle;
        }

        private void CancelRun()
        {
            try
            {
                ConfigClient c = ConfigClient.GetDefaultClient();
                c.InvokeThenClose(x => x.CancelRun(this.ManagementAgentName));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not cancel the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Start()
        {
            try
            {
                ConfigClient c = ConfigClient.GetDefaultClient();
                c.InvokeThenClose(x => x.Start(this.ManagementAgentName));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not start the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanStart()
        {
            return this.ControlState == ControlState.Stopped;
        }

        private void SubscribeToStateChanges()
        {
            InstanceContext i = new InstanceContext(this);
            this.client = EventClient.GetDefaultClient(i);
            this.client.Register(this.ManagementAgentName);
            this.client.InnerChannel.Closed += this.InnerChannel_Closed;
            this.client.InnerChannel.Faulted += this.InnerChannel_Faulted;

            MAStatus status = this.client.GetFullUpdate(this.ManagementAgentName);
            this.faultedCount = 0;
            if (status != null)
            {
                this.MAStatusChanged(status);
            }
        }
        private void InnerChannel_Faulted(object sender, EventArgs e)
        {
            Trace.WriteLine($"Closing faulted event channel for {this.ManagementAgentName}");
            this.client.Abort();
            this.faultedCount++;
        }

        private void InnerChannel_Closed(object sender, EventArgs e)
        {
            Trace.WriteLine($"Closing event channel for {this.ManagementAgentName}");
            this.CleanupAndRestartClient();
        }

        private int faultedCount;

        private void CleanupAndRestartClient()
        {
            if (this.faultedCount > 5)
            {
                throw new ApplicationException($"An unrecoverable error occurred trying to reestablish the monitor channel for {this.ManagementAgentName}");
            }

            this.client.InnerChannel.Closed -= this.InnerChannel_Closed;
            this.client.InnerChannel.Faulted -= this.InnerChannel_Faulted;
            this.SubscribeToStateChanges();
        }
    }
}
