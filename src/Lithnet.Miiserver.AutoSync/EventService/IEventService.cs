using System;
using System.ServiceModel;

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
    }
}
