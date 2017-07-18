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
            ConfigFile x = this.Channel.GetConfig();
            x.ValidateManagementAgents();
            return x;
        }

        public void PutConfig(ConfigFile config)
        {
            this.Channel.PutConfig(config);
        }

        public void Reload()
        {
            this.Channel.Reload();
        }
    }
}