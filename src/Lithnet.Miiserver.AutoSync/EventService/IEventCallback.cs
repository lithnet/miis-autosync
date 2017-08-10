using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    public interface IEventCallBack
    {
        [OperationContract (IsOneWay = true)]
        void ExecutionQueueChanged(string executionQueue, string managementAgent);

        [OperationContract(IsOneWay = true)]
        void ExecutingRunProfileChanged(string executingRunProfile, string managementAgent);

        [OperationContract(IsOneWay = true)]
        void StatusChanged(string status, string managementAgent);
    }
}
