using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigClient : ClientBase<IConfigService>, IConfigService
    {
        protected ConfigClient(Binding binding, EndpointAddress endpoint)
            : base(binding, endpoint)
        {
        }

        public ConfigFile GetConfig()
        {
            ProtectedString.EncryptOnWrite = false;
            ConfigFile x = this.Channel.GetConfig();
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

        public bool IsPendingRestart()
        {
            return this.Channel.IsPendingRestart();
        }

        public int GetServiceContractVersion()
        {
            return this.Channel.GetServiceContractVersion();
        }

        public void Stop(Guid managementAgentID, bool cancelRun)
        {
            this.Channel.Stop(managementAgentID, cancelRun);
        }

        public void CancelRun(Guid managementAgentID)
        {
            this.Channel.CancelRun(managementAgentID);
        }

        public void Start(Guid managementAgentID)
        {
            this.Channel.Start(managementAgentID);
        }

        public void StopAll(bool cancelRuns)
        {
            this.Channel.StopAll(cancelRuns);
        }

        public void StartAll()
        {
            this.Channel.StartAll();
        }

        public ControlState GetEngineState()
        {
            return this.Channel.GetEngineState();
        }

        public IList<string> GetManagementAgentNames()
        {
            return this.Channel.GetManagementAgentNames();
        }

        public IList<Guid> GetManagementAgentIDs()
        {
            return this.Channel.GetManagementAgentIDs();
        }

        public IDictionary<Guid, string> GetManagementAgentNameIDs()
        {
            return this.Channel.GetManagementAgentNameIDs();
        }

        public IList<string> GetManagementAgentRunProfileNames(Guid managementAgentID, bool includeMultiStep)
        {
            return this.Channel.GetManagementAgentRunProfileNames(managementAgentID, includeMultiStep);
        }

        public IList<string> GetManagementAgentRunProfileNamesForPartition(Guid managementAgentID, Guid partitionID, bool includeMultiStep)
        {
            return this.Channel.GetManagementAgentRunProfileNamesForPartition(managementAgentID, partitionID, includeMultiStep);
        }

        public IList<string> GetAllowedTriggerTypesForMA(Guid managementAgentID)
        {
            return this.Channel.GetAllowedTriggerTypesForMA(managementAgentID);
        }

        public IMAExecutionTrigger CreateTriggerForManagementAgent(string type, Guid managementAgentID)
        {
            return this.Channel.CreateTriggerForManagementAgent(type, managementAgentID);
        }

        public void AddToExecutionQueue(Guid managementAgentID, string runProfileName)
        {
            this.Channel.AddToExecutionQueue(managementAgentID, runProfileName);
        }

        public IList<Guid> GetManagementAgentsPendingRestart()
        {
            return this.Channel.GetManagementAgentsPendingRestart();
        }

        public string GetMAData(Guid managementAgentID)
        {
            return this.Channel.GetMAData(managementAgentID);
        }

        public void RestartChangedControllers()
        {
            this.Channel.RestartChangedControllers();
        }

        public void SetAutoStartState(bool autoStart)
        {
            this.Channel.SetAutoStartState(autoStart);
        }

        public bool GetAutoStartState()
        {
            return this.Channel.GetAutoStartState();
        }

        public ConfigFile ValidateConfig(ConfigFile config)
        {
            return this.Channel.ValidateConfig(config);
        }

        public string GetAutoSyncServiceAccountName()
        {
            return this.Channel.GetAutoSyncServiceAccountName();
        }

        public void ValidateServiceContractVersion()
        {
            if (this.GetServiceContractVersion() > ConfigService.ServiceContractVersion)
            {
                throw new UnsupportedVersionException("This client needs to be upgraded before it can connect to the specified server");
            }

            if (this.GetServiceContractVersion() < ConfigService.ServiceContractVersion)
            {
                throw new UnsupportedVersionException("This client cannot connect to the server because the server is running an older version. Install a version of the client that matches the one installed on the server, or upgrade the server");
            }
        }

        public static ConfigClient GetNamedPipesClient()
        {
            return new ConfigClient(ConfigServiceConfiguration.NetNamedPipeBinding, ConfigServiceConfiguration.NetNamedPipeEndpointAddress);
        }

        public static ConfigClient GetNetTcpClient(string hostname, int port, string expectedServerIdentityFormat)
        {
            return new ConfigClient(ConfigServiceConfiguration.NetTcpBinding, ConfigServiceConfiguration.CreateTcpEndPointAddress(hostname, port, expectedServerIdentityFormat));
        }
    }
}