using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class ExecutionMonitorsViewModel : ListViewModel<ExecutionMonitorViewModel, string>
    {
        public string DisplayName => "Execution Monitor";

        public ExecutionMonitorsViewModel(IList<string> items)
            : base(items, ExecutionMonitorsViewModel.ViewModelResolver)
        {
            this.Commands.AddItem("StartEngine", x => this.StartEngine(), x => this.CanStartEngine());
            this.Commands.AddItem("StopEngine", x => this.StopEngine(), x => this.CanStopEngine());
        }

        private static ExecutionMonitorViewModel ViewModelResolver(string model)
        {
            return new ExecutionMonitorViewModel(model);
        }
        
        private void StartEngine()
        {
            ConfigClient c = new ConfigClient();
            c.StartAll();
        }

        private bool CanStartEngine()
        {
            ConfigClient c = new ConfigClient();
            ExecutorState state = c.GetEngineState();
            return state == ExecutorState.Stopped;
        }

        private void StopEngine()
        {
            ConfigClient c = new ConfigClient();
            c.StopAll();
        }

        private bool CanStopEngine()
        {
            ConfigClient c = new ConfigClient();
            ExecutorState state = c.GetEngineState();
            return state == ExecutorState.Running;
        }
    }
}
