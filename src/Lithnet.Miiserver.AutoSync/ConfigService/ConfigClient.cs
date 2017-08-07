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
    }
}