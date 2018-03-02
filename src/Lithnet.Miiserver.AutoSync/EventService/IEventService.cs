using System;
using System.Collections.Generic;
using System.ServiceModel;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [ServiceContract(Namespace = "http://lithnet.local/autosync/config", CallbackContract = typeof(IEventCallBack))]
    public interface IEventService
    {
        [OperationContract]
        void Register(Guid managementAgentID);

        [OperationContract()]
        MAStatus GetFullUpdate(Guid managementAgentID);
        
        [OperationContract]
        bool Ping(Guid managementAgentID);

        [OperationContract]
        string GetRunDetail(Guid managementAgentID, int runNumber);

        [OperationContract]
        IEnumerable<CSObjectRef> GetStepDetail(Guid stepID, string statisticsType);
    }
}
