using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.ServiceModel.Description;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigClient : ClientBase<IConfigService>, IConfigService
    {
        public ConfigClient()
            : base(ConfigServiceConfiguration.NetNamedPipeBinding, ConfigServiceConfiguration.NetNamedPipeEndpointAddress)
        {
        }

        public ConfigFile GetConfig()
        {
            ProtectedString.EncryptOnWrite = false;
            ConfigFile x = this.Channel.GetConfig();
            x.ValidateManagementAgents();
            return x;
        }

        public void PutConfig(ConfigFile config)
        {
            ProtectedString.EncryptOnWrite = false;
            this.Channel.PutConfig(config);
        }

        public void PutConfigAndReloadChanged(ConfigFile config)
        {
            this.Channel.PutConfigAndReloadChanged(config);
        }

        public void Reload()
        {
            this.Channel.Reload();
        }

        public bool IsPendingRestart()
        {
            return this.Channel.IsPendingRestart();
        }

        public void Stop(string managementAgentName)
        {
            this.Channel.Stop(managementAgentName);
        }

        public void Start(string managementAgentName)
        {
            this.Channel.Start(managementAgentName);
        }

        public void StopAll()
        {
            this.Channel.StopAll();
        }

        public void StartAll()
        {
            this.Channel.StartAll();
        }

        public ExecutorState GetEngineState()
        {
            return this.Channel.GetEngineState();
        }
        public IList<string> GetManagementAgentNames()
        {
            return this.Channel.GetManagementAgentNames();
        }

        public IList<string> GetManagementAgentsPendingRestart()
        {
            return this.Channel.GetManagementAgentsPendingRestart();
        }

        public void RestartChangedExecutors()
        {
            this.Channel.RestartChangedExecutors();
        }
    }
}