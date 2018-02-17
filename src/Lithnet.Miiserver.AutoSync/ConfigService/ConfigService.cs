using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using Lithnet.Miiserver.Client;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigService : IConfigService
    {
        public const int ServiceContractVersion = 1;

        private static Logger logger = LogManager.GetCurrentClassLogger();

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
                    logger.Trace("Added service debug behavior");
                }
                else
                {
                    s.Description.Behaviors.Remove(d);
                    s.Description.Behaviors.Add(ConfigServiceConfiguration.ServiceDebugBehavior);
                    logger.Trace("Replaced service debug behavior");
                }

                s.Authorization.ServiceAuthorizationManager = new ConfigServiceAuthorizationManager();
                s.Open();

                return s;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Cannot create service instance");
                throw;
            }
        }

        public ConfigFile GetConfig()
        {
            try
            {
                logger.Trace($"Calling {nameof(this.GetConfig)} as {Environment.UserName}");
                Global.ThrowOnSyncEngineNotRunning();
                ProtectedString.EncryptOnWrite = false;
                return Program.ActiveConfig;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        public ConfigFile ValidateConfig(ConfigFile config)
        {
            try
            {
                logger.Trace($"Calling {nameof(this.ValidateConfig)} as {Environment.UserName}");
                Global.ThrowOnSyncEngineNotRunning();
                ProtectedString.EncryptOnWrite = false;
                config.ValidateManagementAgents();
                return config;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        public string GetAutoSyncServiceAccountName()
        {
            return RegistrySettings.ServiceAccount;
        }

        public byte[] GetFileContent(string path)
        {
            throw new NotImplementedException();
        }

        public void PutFileContent(string path, byte[] content)
        {
            throw new NotImplementedException();
        }

        public void PutConfig(ConfigFile config)
        {
            try
            {
                logger.Trace($"Calling {nameof(this.PutConfig)} as {Environment.UserName}");
                ProtectedString.EncryptOnWrite = true;
                ConfigFile.Save(config, RegistrySettings.ConfigurationFile);
                Program.ActiveConfig = config;

                IList<Guid> items = this.GetManagementAgentsPendingRestart();

                if (items != null && items.Count > 0)
                {
                    string list = string.Join("\r\n", items);
                    logger.Info($"The configuration has been updated. The following management agents must be restarted for the configuration to take effect\r\n{list}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        public void PutConfigAndReloadChanged(ConfigFile config)
        {
            try
            {
                logger.Trace($"Calling {nameof(this.PutConfig)} as {Environment.UserName}");
                Global.ThrowOnSyncEngineNotRunning();
                ProtectedString.EncryptOnWrite = true;
                ConfigFile.Save(config, RegistrySettings.ConfigurationFile);
                Program.ActiveConfig = config;
                Program.Engine?.RestartChangedControllers();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        public bool IsPendingRestart()
        {
            try
            {
                logger.Trace($"Calling {nameof(this.IsPendingRestart)} as {Environment.UserName}");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        public int GetServiceContractVersion()
        {
            return ConfigService.ServiceContractVersion;
        }

        public void CancelRun(Guid managementAgentID)
        {
            Program.Engine?.CancelRun(managementAgentID);
        }

        public void Stop(Guid managementAgentID, bool cancelRun)
        {
            try
            {
                Program.Engine?.Stop(managementAgentID, cancelRun);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        public void Start(Guid managementAgentID)
        {
            try
            {
                Global.ThrowOnSyncEngineNotRunning();
                Program.Engine?.Start(managementAgentID);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
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
                logger.Error(ex);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
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
                logger.Error(ex);
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
            return Global.MANameIDMapping.Values.ToList();
        }

        public IList<Guid> GetManagementAgentIDs()
        {
            Global.ThrowOnSyncEngineNotRunning();
            return Global.MANameIDMapping.Keys.ToList();
        }

        public IDictionary<Guid, string> GetManagementAgentNameIDs()
        {
            Global.ThrowOnSyncEngineNotRunning();
            return Global.MANameIDMapping;
        }

        public IList<string> GetManagementAgentRunProfileNames(Guid managementAgentID, bool includeMultiStep)
        {
            return this.GetManagementAgentRunProfileNamesForPartition(managementAgentID, Guid.Empty, includeMultiStep);
        }

        public IList<string> GetManagementAgentRunProfileNamesForPartition(Guid managementAgentID, Guid partitionID, bool includeMultiStep)
        {
            List<string> items = new List<string>();

            try
            {
                ManagementAgent ma = ManagementAgent.GetManagementAgent(managementAgentID);

                foreach (KeyValuePair<string, RunConfiguration> i in ma.RunProfiles.Where(t => includeMultiStep || t.Value.RunSteps.Count == 1))
                {
                    if (partitionID != Guid.Empty)
                    {
                        if (i.Value.RunSteps.Any(t => t.Partition == partitionID))
                        {
                            items.Add(i.Key);
                        }
                    }
                    else
                    {
                        items.Add(i.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "A request to get the run profile names failed");
            }

            return items;
        }

        public IList<string> GetAllowedTriggerTypesForMA(Guid managementAgentID)
        {
            List<string> allowedTypes = new List<string>();

            ManagementAgent ma = ManagementAgent.GetManagementAgent(managementAgentID);

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

        public IMAExecutionTrigger CreateTriggerForManagementAgent(string type, Guid managementAgentID)
        {
            ManagementAgent ma = ManagementAgent.GetManagementAgent(managementAgentID);
            Type t = Type.GetType(type);

            if (t == null)
            {
                throw new InvalidOperationException($"Could not create trigger for management agent {ma.Name} because the type {type} was unknown");
            }

            IMAExecutionTrigger instance = (IMAExecutionTrigger)Activator.CreateInstance(t, ma);

            return instance;
        }

        public void AddToExecutionQueue(Guid managementAgentID, string runProfileName)
        {
            Program.Engine?.AddToExecutionQueue(managementAgentID, runProfileName);
        }

        public IList<Guid> GetManagementAgentsPendingRestart()
        {
            return Program.Engine?.GetManagementAgentsPendingRestart();
        }

        public void RestartChangedControllers()
        {
            Global.ThrowOnSyncEngineNotRunning();
            Program.Engine?.RestartChangedControllers();
        }

        public void SetAutoStartState(bool autoStart)
        {
            logger.Info($"Setting auto start to {autoStart}");
            RegistrySettings.AutoStartEnabled = autoStart;
        }

        public bool GetAutoStartState()
        {
            return RegistrySettings.AutoStartEnabled;
        }

        public string GetMAData(Guid managementAgentID)
        {
            ManagementAgent ma = ManagementAgent.GetManagementAgent(managementAgentID);
            return ma.GetOuterXml();
        }
    }
}
