﻿using System;
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
        void PutConfigAndReloadChanged(ConfigFile config);

        [OperationContract]
        bool IsPendingRestart();

        [OperationContract(IsOneWay = true)]
        void Stop(string managementAgentName);

        [OperationContract(IsOneWay = true)]
        void Start(string managementAgentName);

        [OperationContract(IsOneWay = true)]
        void StopAll();

        [OperationContract(IsOneWay = true)]
        void StartAll();

        [OperationContract]
        ExecutorState GetEngineState();

        [OperationContract]
        IList<string> GetManagementAgentNames();

        [OperationContract]
        IList<string> GetManagementAgentsPendingRestart();

        [OperationContract]
        void RestartChangedExecutors();
        
        [OperationContract]
        void SetAutoStartState(bool autoStart);

        [OperationContract]
        bool GetAutoStartState();
    }
}