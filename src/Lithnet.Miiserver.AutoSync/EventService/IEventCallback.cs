using System.ServiceModel;

namespace Lithnet.Miiserver.AutoSync
{
    public interface IEventCallBack
    {
        [OperationContract(IsOneWay = true)]
        void MAStatusChanged(MAStatus status);

        [OperationContract(IsOneWay = true)]
        void RunProfileExecutionComplete(RunProfileExecutionCompleteEventArgs e);

        [OperationContract(IsOneWay = true)]
        void MessageLogged(MessageLoggedEventArgs e);
    }
}
