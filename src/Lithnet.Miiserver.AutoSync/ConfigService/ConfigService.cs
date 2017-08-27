﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Logging;
using System.Diagnostics;
using Lithnet.Miiserver.Client;

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
                Global.ThrowOnSyncEngineNotRunning();
                ProtectedString.EncryptOnWrite = false;
                return Program.ActiveConfig;
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
                Program.ActiveConfig = config;

                IList<string> items = this.GetManagementAgentsPendingRestart();

                if (items != null && items.Count > 0)
                {
                    string list = string.Join("\r\n", items);
                    Logger.WriteLine($"The configuration has been updated. The following management agents must be restarted for the configuration to take effect\r\n{list}");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void PutConfigAndReloadChanged(ConfigFile config)
        {
            try
            {
                Trace.WriteLine($"Calling {nameof(this.PutConfig)} as {Environment.UserName}");
                Global.ThrowOnSyncEngineNotRunning();
                ProtectedString.EncryptOnWrite = true;
                ConfigFile.Save(config, RegistrySettings.ConfigurationFile);
                Program.ActiveConfig = config;
                Program.RestartChangedExecutors();
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
                return false;
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void Stop(string managementAgentName)
        {
            try
            {
                Program.StopExecutor(managementAgentName);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void Start(string managementAgentName)
        {
            try
            {
                Global.ThrowOnSyncEngineNotRunning();
                Program.StartExecutor(managementAgentName);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void StopAll()
        {
            try
            {
                Program.StopExecutors();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void StartAll()
        {
            try
            {
                Global.ThrowOnSyncEngineNotRunning();
                Program.StartExecutors();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public ExecutorState GetEngineState()
        {
            return Program.GetEngineState();
        }

        public IList<string> GetManagementAgentNames()
        {
            Global.ThrowOnSyncEngineNotRunning();
            return ManagementAgent.GetManagementAgents().Select(t => t.Name).ToList();
        }

        public IList<string> GetManagementAgentsPendingRestart()
        {
            return Program.GetManagementAgentsPendingRestart();
        }

        public void RestartChangedExecutors()
        {
            Global.ThrowOnSyncEngineNotRunning();
            Program.RestartChangedExecutors();
        }

        public void SetAutoStartState(bool autoStart)
        {
            Logger.WriteLine($"Setting auto start to {autoStart}");
            RegistrySettings.AutoStartEnabled = autoStart;
        }

        public bool GetAutoStartState()
        {
            return RegistrySettings.AutoStartEnabled;
        }
    }
}
