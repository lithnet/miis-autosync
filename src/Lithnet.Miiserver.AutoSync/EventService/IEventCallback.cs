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
        [OperationContract(IsOneWay = true)]
        void MAStatusChanged(MAStatus status);

        [OperationContract(IsOneWay = true)]
        void RunProfileExecutionComplete(RunProfileExecutionCompleteEventArgs e);
    }
}
