using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Lithnet.Common.Presentation;
using NLog;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class ExecutionMonitorsViewModel : ListViewModel<ExecutionMonitorViewModel, object>
    {
        private bool starting;

        private bool stopping;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string DisplayName => "Execution Monitor";

        public bool AutoStartEnabled
        {
            get
            {
                try
                {
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    return c.InvokeThenClose(x => x.GetAutoStartState());
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Cannot get auto start state");
                    return false;
                }
            }
            set
            {
                try
                {
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.SetAutoStartState(value));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not set the auto start state");
                }
            }
        }

        public ExecutionMonitorsViewModel(IList<object> items)
            : base(items, ExecutionMonitorsViewModel.ViewModelResolver)
        {
            this.Commands.AddItem("StartEngine", x => this.StartEngine(), x => this.CanStartEngine());
            this.Commands.AddItem("StopEngine", x => this.StopEngine(false), x => this.CanStopEngine());
            this.Commands.AddItem("StopEngineAndCancelRuns", x => this.StopEngine(true), x => this.CanStopEngine());

            this.DisplayIcon = App.GetImageResource("Monitor.ico");

            ExecutionMonitorViewModel vm = this.ViewModels.FirstOrDefault();
            if (vm != null)
            {
                vm.IsSelected = true;
            }

            foreach (var vm2 in this.ViewModels)
            {
                vm2.PropertyChanged += this.Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ControlState")
            {
                Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
            }
        }

        private static ExecutionMonitorViewModel ViewModelResolver(object model)
        {
            return new ExecutionMonitorViewModel((KeyValuePair<Guid, string>)model);
        }

        private void StartEngine()
        {
            Task.Run(() =>
            {
                try
                {
                    this.starting = true;
                    Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    c.GetEngineState();
                    c.InvokeThenClose(x => x.StartAll());
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not start the management agents");
                    MessageBox.Show($"Error starting the management agents\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    this.starting = false;
                }
            }).ContinueWith(x => Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested));
        }

        private bool CanStartEngine()
        {
            return !this.starting && this.ViewModels.Any(t => t.ControlState == ControlState.Stopped);
        }

        private void StopEngine(bool cancelRuns)
        {
            Task.Run(() =>
            {
                try
                {
                    this.stopping = true;
                    Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
                    ConfigClient c = ConnectionManager.GetDefaultConfigClient();
                    c.InvokeThenClose(x => x.StopAll(cancelRuns));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Could not stop the management agents");
                    MessageBox.Show($"Error stopping the management agents\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    this.stopping = false;
                }
            }).ContinueWith(x => Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested));
        }

        private bool CanStopEngine()
        {
            return !this.stopping && this.ViewModels.Any(t => t.ControlState == ControlState.Running);
        }
    }
}
