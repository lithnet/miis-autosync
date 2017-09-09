using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using Lithnet.Logging;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel.Channels;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigService : IConfigService
    {
        public static ServiceHost CreateNetNamedPipeInstance()
        {
            return ConfigService.CreateInstance(ConfigServiceConfiguration.NetNamedPipeBinding, ConfigServiceConfiguration.NamedPipeUri);
        }

        public static ServiceHost CreateNetTcpInstance()
        {
            return ConfigService.CreateInstance(ConfigServiceConfiguration.NetTcpBinding, ConfigServiceConfiguration.CreateServerBindingUri());
        }

        private static ServiceHost CreateInstance(Binding binding, string uri)
        {
            try
            {
                ServiceHost s = new ServiceHost(typeof(ConfigService));
                s.AddServiceEndpoint(typeof(IConfigService), binding, uri);
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

        public ConfigFile ValidateConfig(ConfigFile config)
        {
            try
            {
                Trace.WriteLine($"Calling {nameof(this.ValidateConfig)} as {Environment.UserName}");
                Global.ThrowOnSyncEngineNotRunning();
                ProtectedString.EncryptOnWrite = false;
                config.ValidateManagementAgents();
                return config;
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
                Program.Engine?.RestartChangedExecutors();
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

        public void CancelRun(string managementAgentName)
        {
            Program.Engine?.CancelRun(managementAgentName);
        }

        public void Stop(string managementAgentName, bool cancelRun)
        {
            try
            {
                Program.Engine?.Stop(managementAgentName, cancelRun);
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
                Program.Engine?.Start(managementAgentName);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public void StopAll(bool cancelRuns)
        {
            try
            {
                Program.Engine?.Stop(cancelRuns);
            }
            catch (TimeoutException ex)
            {
                Logger.WriteException(ex);
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
                Program.Engine?.Start();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        public ControlState GetEngineState()
        {
            return Program.Engine?.State ?? ControlState.Stopped;
        }

        public IList<string> GetManagementAgentNames()
        {
            Global.ThrowOnSyncEngineNotRunning();
            return ManagementAgent.GetManagementAgents().Select(t => t.Name).ToList();
        }

        public IList<string> GetManagementAgentRunProfileNames(string managementAgentName, bool includeMultiStep)
        {
            List<string> items = new List<string>();

            try
            {
                ManagementAgent ma = ManagementAgent.GetManagementAgent(managementAgentName);

                foreach (KeyValuePair<string, RunConfiguration> i in ma.RunProfiles.Where(t => includeMultiStep || t.Value.RunSteps.Count == 1))
                {
                    items.Add(i.Key);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("A request to get the run profile names failed");
                Logger.WriteException(ex);
            }

            return items;
        }

        public IList<string> GetAllowedTriggerTypesForMA(string managementAgentName)
        {
            List<string> allowedTypes = new List<string>();

            ManagementAgent ma = ManagementAgent.GetManagementAgent(managementAgentName);

            foreach (Type t in Assembly.GetExecutingAssembly().GetTypes()
                .Where(mytype => mytype.GetInterfaces().Contains(typeof(IMAExecutionTrigger))))
            {
                MethodInfo i = t.GetMethod("CanCreateForMA");

                if (i != null)
                {
                    if ((bool)i.Invoke(null, new object[] { ma }))
                    {
                        allowedTypes.Add(t.FullName);
                    }
                }
            }

            return allowedTypes;
        }

        public IMAExecutionTrigger CreateTriggerForManagementAgent(string type, string managementAgentName)
        {
            ManagementAgent ma = ManagementAgent.GetManagementAgent(managementAgentName);
            Type t = Type.GetType(type);

            if (t == null)
            {
                throw new InvalidOperationException($"Could not create trigger for management agent {managementAgentName} because the type {type} was unknown");
            }

            IMAExecutionTrigger instance = (IMAExecutionTrigger)Activator.CreateInstance(t, ma);

            return instance;
        }

        public void AddToExecutionQueue(string managementAgentName, string runProfileName)
        {
            Program.Engine?.AddToExecutionQueue(managementAgentName, runProfileName);
        }

        public IList<string> GetManagementAgentsPendingRestart()
        {
            return Program.Engine?.GetManagementAgentsPendingRestart();
        }

        public void RestartChangedExecutors()
        {
            Global.ThrowOnSyncEngineNotRunning();
            Program.Engine?.RestartChangedExecutors();
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
