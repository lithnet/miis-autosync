using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    internal class ConfigFileViewModel : ViewModelBase<ConfigFile>
    {
        public ConfigFileViewModel(ConfigFile model)
            :base (model)
        {
            this.ManagementAgents = new ManagementAgentsViewModel(model.ManagementAgents);
        }

        public ManagementAgentsViewModel ManagementAgents { get; private set; }

        public ViewModelBase Settings { get; private set; }

        public override IEnumerable<ViewModelBase> ChildNodes
        {
            get
            {
                yield return this.ManagementAgents;
                yield return this.Settings;
            }
        }
    }
}
