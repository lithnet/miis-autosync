using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class ManagementAgentsViewModel : ListViewModel<MAConfigParametersViewModel, MAConfigParameters>
    {
        public string DisplayName => "Management Agents";

        public ManagementAgentsViewModel(IList<MAConfigParameters> items)
            : base(items, ManagementAgentsViewModel.ViewModelResolver)
        {

            this.Commands.AddItem("StartEngine", x => this.StartEngine(), x => this.CanStartEngine());
            this.Commands.AddItem("StopEngine", x => this.StopEngine(), x => this.CanStopEngine());
        }

        private static MAConfigParametersViewModel ViewModelResolver(MAConfigParameters model)
        {
            return new MAConfigParametersViewModel(model);
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
