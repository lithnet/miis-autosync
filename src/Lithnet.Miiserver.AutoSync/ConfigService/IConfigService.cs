using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace Lithnet.Miiserver.AutoSync
{
    [ServiceKnownType(typeof(ScheduledExecutionTrigger))]
    [ServiceKnownType(typeof(ActiveDirectoryChangeTrigger))]
    [ServiceKnownType(typeof(FimServicePendingImportTrigger))]
    [ServiceKnownType(typeof(IntervalExecutionTrigger))]
    [ServiceKnownType(typeof(PowerShellExecutionTrigger))]
    [ServiceContract(Namespace = "http://lithnet.local/autosync/config")]
    public interface IConfigService
    {
        [OperationContract]
        ConfigFile GetConfig();

        [OperationContract]
        void PutConfig(ConfigFile config);

        [OperationContract]
        void PutConfigAndReloadChanged(ConfigFile config);

        [OperationContract]
        bool IsPendingRestart();

        [OperationContract(IsOneWay = true)]
        void Stop(Guid managementAgentID, bool cancelRun);

        [OperationContract(IsOneWay = true)]
        void Start(Guid managementAgentID);

        [OperationContract(IsOneWay = true)]
        void StopAll(bool cancelRuns);

        [OperationContract(IsOneWay = true)]
        void StartAll();
        
        [OperationContract(IsOneWay = true)]
        void CancelRun(Guid managementAgentID);

        [OperationContract]
        ControlState GetEngineState();

        [OperationContract]
        IList<string> GetManagementAgentNames();
        
        [OperationContract]
        IList<Guid> GetManagementAgentIDs();

        [OperationContract]
        IDictionary<Guid, string> GetManagementAgentNameIDs();

        [OperationContract]
        IList<string> GetManagementAgentRunProfileNames(Guid managementAgentID, bool includeMultiStep);

        [OperationContract]
        IList<string> GetManagementAgentRunProfileNamesForPartition(Guid managementAgentID, Guid partitionID, bool includeMultiStep);

        [OperationContract]
        IList<string> GetAllowedTriggerTypesForMA(Guid managementAgentID);

        [OperationContract]
        IMAExecutionTrigger CreateTriggerForManagementAgent(string type, Guid managementAgentID);

        [OperationContract]
        void AddToExecutionQueue(Guid managementAgentID, string runProfileName);

        [OperationContract]
        IList<Guid> GetManagementAgentsPendingRestart();

        [OperationContract]
        void RestartChangedControllers();
        
        [OperationContract]
        void SetAutoStartState(bool autoStart);

        [OperationContract]
        bool GetAutoStartState();

        [OperationContract]
        ConfigFile ValidateConfig(ConfigFile config);

        [OperationContract]
        string GetAutoSyncServiceAccountName();
    }
}