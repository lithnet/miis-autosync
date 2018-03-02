using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using Lithnet.Miiserver.Client;
using LiveCharts;
using LiveCharts.Wpf;
using NLog;
using PropertyChanged;
using Timer = System.Timers.Timer;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ExecutionMonitorViewModel : ViewModelBase<object>, IEventCallBack, IDisposable
    {
        private EventClient client;

        private Timer pingTimer;

        private int faultedCount;

        private bool disposed;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public ExecutionMonitorViewModel(KeyValuePair<Guid, string> ma)
            : base(ma)
        {
            this.ManagementAgentID = ma.Key;
            this.ManagementAgentName = ma.Value;
            this.DetailMessages = new ObservableCollection<LoggedMessageViewModel>();
            this.RunHistory = new ObservableCollection<RunProfileResultViewModel>();
            this.SubscribeToStateChanges();
            this.PopulateMenuItems();

            this.Commands.AddItem("Start", x => this.Start(), x => this.CanStart());
            this.Commands.AddItem("Stop", x => this.Stop(false), x => this.CanStop());
            this.Commands.AddItem("StopAndCancelRuns", x => this.Stop(true), x => this.CanStop());
        }

        private Guid ManagementAgentID { get; set; }

        public ObservableCollection<MenuItemViewModelBase> MenuItems { get; set; }

        public bool IsConnected { get; set; }

        public new BitmapImage DisplayIcon
        {
            get
            {
                if (this.ThresholdExceeded)
                {
                    return App.GetImageResource("Blocked.png");
                }

                if (this.HasError)
                {
                    return App.GetImageResource("Error_red.ico");

                }

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

        public bool ThresholdExceeded { get; private set; }

        public bool HasError { get; private set; }

        public string ExecutingRunProfile { get; private set; }

        [AlsoNotifyFor(nameof(LastRun), nameof(ExecutionMonitorViewModel.LastRunResultIcon))]
        public string LastRunProfileResult { get; private set; }

        [AlsoNotifyFor(nameof(LastRun), nameof(ExecutionMonitorViewModel.LastRunResultIcon))]
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
            this.Message = status.ThresholdExceeded ? "Threshold exceeded" : status.Message;
            this.ExecutingRunProfile = status.ExecutingRunProfile;
            this.ExecutionQueue = status.ExecutionQueue;
            this.DisplayState = status.DisplayState;
            this.ControlState = status.ControlState;
            this.ExecutionState = status.ExecutionState;
            this.HasExclusiveLock = status.HasExclusiveLock;
            this.HasSyncLock = status.HasSyncLock;
            this.HasForeignLock = status.HasForeignLock;
            this.ThresholdExceeded = status.ThresholdExceeded;
            this.HasError = status.HasError;

            this.Disabled = this.ControlState == ControlState.Disabled;
        }

        public BitmapImage LastRunResultIcon => this.lastRunResult?.ResultIcon;

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

        public SeriesCollection ResultSeries { get; } = new SeriesCollection();

        public Dictionary<string, int> ResultCounters = new Dictionary<string, int>();

        private void AddRunProfileHistory(RunProfileExecutionCompleteEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.RunProfileName))
            {
                return;
            }

            RunProfileResultViewModel t = new RunProfileResultViewModel(this.OpenRunDetails, e);

            this.lastRunResult = t;
            this.LastRunProfileName = t.RunProfileName;
            this.LastRunProfileResult = t.Result;

            if (!this.ResultCounters.ContainsKey(e.Result))
            {
                this.ResultCounters.Add(e.Result, 0);
            }

            this.ResultCounters[e.Result]++;

            PieSeries item = this.ResultSeries.OfType<PieSeries>().FirstOrDefault(u => u.Title == e.Result);

            if (item == null)
            {
                item = new PieSeries();
                item.Title = e.Result;
                var hash = Math.Abs(e.Result.GetHashCode() % 70);

                if (e.Result == "success")
                {
                    item.Fill = Brushes.Green;
                }
                else if (e.Result.StartsWith("completed"))
                {
                    item.Fill = new SolidColorBrush(Color.FromRgb(255, (byte)(hash + 165), (byte)hash));
                }
                else
                {
                    item.Fill = new SolidColorBrush(Color.FromRgb(255, (byte)hash, (byte)hash));
                }

                this.ResultSeries.Add(item);
            }

            item.Values = new ChartValues<int>() { this.ResultCounters[e.Result] };

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

                ConfigClient c = ConnectionManager.GetDefaultConfigClient();
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
                logger.Error(ex, "Could not get the run profile list");
            }
        }

        private void AddToExecutionQueue(string runProfileName)
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    c.AddToExecutionQueue(this.ManagementAgentID, runProfileName);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not add the run profile to the execution queue");
                    MessageBox.Show($"Could not add the run profile to the execution queue\r\n\r\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private void Stop(bool cancelRun)
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.Stop(this.ManagementAgentID, cancelRun));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not stop the management agent");
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

        private void OpenRunDetails(int runNumber)
        {
            Task.Run(() =>
            {
                try
                {
                    string result = this.client.GetRunDetail(this.ManagementAgentID, runNumber);
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(result);

                    RunDetails r = new RunDetails(doc.FirstChild);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RunDetailsWindow window = new RunDetailsWindow();
                        window.DataContext = new RunDetailsViewModel(r, this.GetStepDetails);

                        window.Show();
                    });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not load the run profile results");
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Could not load the run profile results\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private IEnumerable<CSObjectRef> GetStepDetails(Guid stepId, string statisticsType)
        {
            return this.client.GetStepDetail(stepId, statisticsType);
        }

        private void CancelRun()
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.CancelRun(this.ManagementAgentID));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not cancel the management agent");
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Could not cancel the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void Start()
        {
            Task.Run(() =>
            {
                try
                {
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.Start(this.ManagementAgentID));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not start the management agent");
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Could not start the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private bool CanStart()
        {
            return this.ControlState == ControlState.Stopped;
        }

        private void SubscribeToStateChanges()
        {
            if (this.disposed)
            {
                return;
            }

            logger.Trace($"Attempting to open event channel for {this.ManagementAgentName}/{this.ManagementAgentID}");
            InstanceContext i = new InstanceContext(this);
            this.IsConnected = false;

            while (!this.IsConnected)
            {
                try
                {
                    this.DisplayState = "Disconnected";
                    this.client = ConnectionManager.GetDefaultEventClient(i);
                    this.client.Open();
                    this.client.Register(this.ManagementAgentID);
                    this.IsConnected = true;
                    this.faultedCount = 0;
                }
                catch (TimeoutException)
                {
                    this.client.Abort();
                    logger.Trace("Timeout connecting to server");
                    Thread.Sleep(UserSettings.ReconnectInterval);
                }
                catch (Exception ex)
                {
                    this.client.Abort();
                    logger.Trace(ex, "Error connecting to server");
                    Thread.Sleep(UserSettings.ReconnectInterval);
                }
            }

            logger.Trace($"Registered event channel for {this.ManagementAgentName}/{this.ManagementAgentID}");

            this.client.InnerChannel.Closed += this.InnerChannel_Closed;
            this.client.InnerChannel.Faulted += this.InnerChannel_Faulted;

            logger.Trace($"Requesting full update for {this.ManagementAgentName}/{this.ManagementAgentID}");
            MAStatus status = this.client.GetFullUpdate(this.ManagementAgentID);
            logger.Trace($"Got full update from {this.ManagementAgentName}/{this.ManagementAgentID}");

            this.StartPingTimer();

            if (status != null)
            {
                this.MAStatusChanged(status);
            }
        }

        private void StartPingTimer()
        {
            if (ConnectionManager.ConnectViaNetTcp)
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
                    logger.Trace("Server ping failed. Restarting client");
                    this.CleanupAndRestartClient();
                }
            }
        }

        private void InnerChannel_Faulted(object sender, EventArgs e)
        {
            logger.Warn($"Closing faulted event channel on client side for {this.ManagementAgentName}/{this.ManagementAgentID}");
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
            logger.Warn($"Closing event channel on client side for {this.ManagementAgentName}/{this.ManagementAgentID}");
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
            if (this.disposed)
            {
                return;
            }

            if (this.faultedCount > 5)
            {
                throw new ApplicationException($"An unrecoverable error occurred trying to reestablish the monitor channel for {this.ManagementAgentName}");
            }

            this.SubscribeToStateChanges();
        }

        public void Dispose()
        {
            this.disposed = true;

            this.pingTimer?.Stop();
            this.pingTimer?.Dispose();

            try
            {
                this.client.Abort();
            }
            catch
            {
            }
        }
    }
}
