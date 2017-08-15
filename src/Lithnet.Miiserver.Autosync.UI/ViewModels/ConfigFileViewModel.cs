using System.Collections.Generic;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class ConfigFileViewModel : ViewModelBase<ConfigFile>
    {
        public ConfigFileViewModel(ConfigFile model)
            :base (model)
        {
            if (model.ManagementAgents == null)
            {
                model.ManagementAgents = new MAConfigParametersCollection();
            }

            this.ManagementAgents = new ManagementAgentsViewModel(model.ManagementAgents);
            this.Settings = new SettingsViewModel(model.Settings);

            this.SubscribeToErrors(this.ManagementAgents);
            this.SubscribeToErrors(this.Settings);

            this.AddIsDirtyProperty("Description");
        }

        public string DisplayName => "Configuration";

        public ManagementAgentsViewModel ManagementAgents { get; private set; }

        public SettingsViewModel Settings { get; private set; }

        public void Commit()
        {
            this.IsDirty = false;
            this.Settings.IsDirty = false;
            this.ManagementAgents.IsDirty = false;

            foreach (MAConfigParametersViewModel item in this.ManagementAgents)
            {
                item.Commit();
            }
        }

        public string Description
        {
            get => this.Model.Description;
            set => this.Model.Description = value;
        }

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
