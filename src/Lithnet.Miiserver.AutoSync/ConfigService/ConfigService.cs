﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Logging;
using System.Diagnostics;

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
                    Trace.WriteLine("Added service debug behavior");

                }
                else
                {
                    s.Description.Behaviors.Remove(d);
                    s.Description.Behaviors.Add(ConfigServiceConfiguration.ServiceDebugBehavior);
                    Trace.WriteLine("Replaced service debug behavior");
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
                Trace.WriteLine($"Calling {nameof(this.GetConfig)} as {Environment.UserName}");
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
                Trace.WriteLine($"Calling {nameof(this.PutConfig)} as {Environment.UserName}");
                ProtectedString.EncryptOnWrite = true;
                ConfigFile.Save(config, RegistrySettings.ConfigurationFile);
                Program.CurrentConfig = config;
                Logger.WriteLine("The configuration has been updated. The service must be restarted for the new configuration to take effect");
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
                Trace.WriteLine($"Calling {nameof(this.Reload)} as {Environment.UserName}");
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
                Trace.WriteLine($"Calling {nameof(this.IsPendingRestart)} as {Environment.UserName}");
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
