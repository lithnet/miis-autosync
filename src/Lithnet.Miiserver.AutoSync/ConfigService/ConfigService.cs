using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigService : IConfigService
    {
        public static ServiceHost CreateInstance()
        {
            try
            {
                ServiceHost s = new ServiceHost(typeof(ConfigService));
                s.AddServiceEndpoint(typeof(IConfigService), ConfigServiceConfiguration.NetNamedPipeBinding, ConfigServiceConfiguration.NamedPipeUri);
                if (s.Description.Behaviors.Find<ServiceMetadataBehavior>() == null)
                {
                    s.Description.Behaviors.Add(ConfigServiceConfiguration.ServiceMetadataDisabledBehavior);
                }

                var d = s.Description.Behaviors.Find<ServiceDebugBehavior>();

                if (d == null)
                {
                    s.Description.Behaviors.Add(ConfigServiceConfiguration.ServiceDebugBehavior);
                    Logger.WriteLine("Added fault detail");

                }
                else
                {
                    d.IncludeExceptionDetailInFaults = true;
                    Logger.WriteLine("Turned on fault detail");
                }

                s.Authorization.ServiceAuthorizationManager = new ConfigServiceAuthorizationManager();
                s.Open();

                return s;
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public ConfigFile GetConfig()
        {
            try
            {
                ProtectedString.EncryptOnWrite = false;
                return Program.CurrentConfig;
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void PutConfig(ConfigFile config)
        {
            try
            {
                ProtectedString.EncryptOnWrite = true;
                ConfigFile.Save(config, RegistrySettings.ConfigurationFile);
                Program.CurrentConfig = config;
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void Reload()
        {
            try
            {
                Program.Reload();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public bool IsPendingRestart()
        {
            try
            {
                return Program.PendingRestart;
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }
    }
}
