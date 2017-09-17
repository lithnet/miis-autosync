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
            //x.ValidateManagementAgents();
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

        public static ConfigClient GetNamedPipesClient()
        {
            return new ConfigClient(ConfigServiceConfiguration.NetNamedPipeBinding, ConfigServiceConfiguration.NetNamedPipeEndpointAddress);
        }

        public static ConfigClient GetNetTcpClient(string hostname, string port, string expectedServerIdentityFormat)
        {
            return new ConfigClient(ConfigServiceConfiguration.NetTcpBinding, ConfigServiceConfiguration.CreateTcpEndPointAddress(hostname, port, expectedServerIdentityFormat));
        }
    }
}