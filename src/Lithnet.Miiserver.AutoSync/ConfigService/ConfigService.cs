using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigService : IConfigService
    {
        public static ServiceHost CreateInstance()
        {
            ServiceHost s = new ServiceHost(typeof(ConfigService));
            s.AddServiceEndpoint(typeof(IConfigService), ConfigServiceConfiguration.NetNamedPipeBinding, ConfigServiceConfiguration.NamedPipeUri);
            if (s.Description.Behaviors.Find<ServiceMetadataBehavior>() == null)
            {
                s.Description.Behaviors.Add(ConfigServiceConfiguration.ServiceMetadataDisabledBehavior);
            }

            if (s.Description.Behaviors.Find<ServiceDebugBehavior>() == null)
            {
                s.Description.Behaviors.Add(ConfigServiceConfiguration.ServiceDebugBehavior);
            }

            s.Authorization.ServiceAuthorizationManager = new ConfigServiceAuthorizationManager();
            s.Open();

            return s;
        }

        public ConfigFile GetConfig()
        {
            ProtectedString.EncryptOnWrite = false;
            return Program.ActiveConfig;
        }

        public void PutConfig(ConfigFile config)
        {
            ProtectedString.EncryptOnWrite = true;
            ConfigFile.Save(config, RegistrySettings.ConfigurationFile);
        }

        public void Reload()
        {
            Program.Reload();
        }
    }
}
