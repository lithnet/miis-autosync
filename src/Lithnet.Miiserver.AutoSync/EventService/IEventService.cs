using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [ServiceContract(Namespace = "http://lithnet.local/autosync/config", CallbackContract = typeof(IEventCallBack))]
    public interface IEventService
    {
        [OperationContract]
        void Register(string managementAgentName);

        [OperationContract()]
        MAStatus GetFullUpdate(string managementAgentName);
    }
}
