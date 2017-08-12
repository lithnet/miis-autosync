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
          //  this.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
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

        public void Pause(string managementAgentName)
        {
            this.Channel.Pause(managementAgentName);
        }

        public void Resume(string managementAgentName)
        {
            this.Channel.Resume(managementAgentName);
        }

        public void StopAll()
        {
            this.Channel.StopAll();
        }

        public void StartAll()
        {
            this.Channel.StartAll();
        }

        public void PauseAll()
        {
            this.Channel.PauseAll();
        }

        public void ResumeAll()
        {
            this.Channel.ResumeAll();
        }

        public ExecutorState GetEngineState()
        {
            return this.Channel.GetEngineState();
        }
    }
}