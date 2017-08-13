using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Windows;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ExecutionMonitorViewModel : ViewModelBase<string>, IEventCallBack
    {
        private EventClient client;

        public ExecutionMonitorViewModel(string maName)
            :base (maName)
        {
            this.Commands.Add("Start", new DelegateCommand(t => this.Start(), u => this.CanStart()));
            this.Commands.Add("Stop", new DelegateCommand(t => this.Stop(), u => this.CanStop()));
            this.ManagementAgentName = maName;
            this.SubscribeToStateChanges();
        }

        private void Stop()
        {
            try
            {
                ConfigClient c = new ConfigClient();
                c.Stop(this.ManagementAgentName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not stop the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanStop()
        {
            return this.ControlState == ExecutorState.Running;
        }

        private void Start()
        {
            try
            {
                ConfigClient c = new ConfigClient();
                c.Start(this.ManagementAgentName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not start the management agent\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanStart()
        {
            return this.ControlState == ExecutorState.Stopped;
        }

        private void SubscribeToStateChanges()
        {
            InstanceContext i = new InstanceContext(this);
            this.client = new EventClient(i);
            this.client.Register(this.ManagementAgentName);
            MAStatus status = this.client.GetFullUpdate(this.ManagementAgentName);

            if (status != null)
            {
                this.MAStatusChanged(status);
            }
        }

        public BitmapImage StatusIcon
        {
            get
            {
                switch (this.DisplayState)
                {
                    case ExecutorState.Disabled:
                        return App.GetImageResource("Stop.png");

                    case ExecutorState.Idle:
                        return App.GetImageResource("Clock1.png");

                    case ExecutorState.Paused:
                        return App.GetImageResource("Pause.png");

                    case ExecutorState.Processing:
                    case ExecutorState.Running:
                        return App.GetImageResource("Run.png");

                    case ExecutorState.Pausing:
                    case ExecutorState.Resuming:
                    case ExecutorState.Starting:
                    case ExecutorState.Waiting:
                    case ExecutorState.Stopping:
                        return App.GetImageResource("Hourglass.png");

                    case ExecutorState.Stopped:
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

        [AlsoNotifyFor(nameof(LastRun))]
        public string LastRunProfileResult { get; private set; }

        [AlsoNotifyFor(nameof(LastRun))]
        public string LastRunProfileName { get; private set; }

        public string LastRun => this.LastRunProfileName == null ? null : $"{this.LastRunProfileName}: {this.LastRunProfileResult}";

        public ExecutorState DisplayState { get; private set; }

        public ExecutorState ControlState { get; private set; }

        public void MAStatusChanged(MAStatus status)
        {
            this.Message = status.Message;
            this.ExecutingRunProfile = status.ExecutingRunProfile;
            this.ExecutionQueue = status.ExecutionQueue;
            this.LastRunProfileResult = status.LastRunProfileResult;
            this.LastRunProfileName = status.LastRunProfileName;
            this.DisplayState = status.DisplayState;
            this.ControlState = status.ControlState;
            this.Disabled = this.ControlState == ExecutorState.Disabled;
        }
    }
}
