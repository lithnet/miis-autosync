using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [ServiceContract(Namespace = "http://lithnet.local/autosync/config")]
    public interface IConfigService
    {
        [OperationContract]
        ConfigFile GetConfig();

        [OperationContract]
        void PutConfig(ConfigFile config);

        [OperationContract]
        void Reload();

        [OperationContract]
        bool IsPendingRestart();

        [OperationContract(IsOneWay = true)]
        void Stop(string managementAgentName);

        [OperationContract(IsOneWay = true)]
        void Start(string managementAgentName);

        [OperationContract(IsOneWay = true)]
        void Pause(string managementAgentName);

        [OperationContract(IsOneWay = true)]
        void Resume(string managementAgentName);

        [OperationContract(IsOneWay = true)]
        void StopAll();

        [OperationContract(IsOneWay = true)]
        void StartAll();

        [OperationContract(IsOneWay = true)]
        void PauseAll();

        [OperationContract(IsOneWay = true)]
        void ResumeAll();

        [OperationContract]
        ExecutorState GetEngineState();
    }
}
