using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

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
        void Stop(string managementAgentName, bool cancelRun);

        [OperationContract(IsOneWay = true)]
        void Start(string managementAgentName);

        [OperationContract(IsOneWay = true)]
        void StopAll(bool cancelRuns);

        [OperationContract(IsOneWay = true)]
        void StartAll();
        
        [OperationContract(IsOneWay = true)]
        void CancelRun(string managementAgentName);

        [OperationContract]
        ControlState GetEngineState();

        [OperationContract]
        IList<string> GetManagementAgentNames();

        [OperationContract]
        IList<string> GetManagementAgentRunProfileNames(string managementAgentName, bool includeMultiStep);

        [OperationContract]
        IList<string> GetAllowedTriggerTypesForMA(string managementAgentName);

        [OperationContract]
        IMAExecutionTrigger CreateTriggerForManagementAgent(string type, string managementAgentName);

        [OperationContract]
        void AddToExecutionQueue(string managementAgentName, string runProfileName);

        [OperationContract]
        IList<string> GetManagementAgentsPendingRestart();

        [OperationContract]
        void RestartChangedExecutors();
        
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