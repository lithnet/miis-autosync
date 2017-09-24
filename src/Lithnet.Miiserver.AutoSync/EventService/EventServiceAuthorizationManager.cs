using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Security.Principal;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    public class EventServiceAuthorizationManager : ServiceAuthorizationManager
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            try
            {
                // Allow MEX requests through. 
                if (operationContext.EndpointDispatcher.ContractName == ServiceMetadataBehavior.MexContractName &&
                    operationContext.EndpointDispatcher.ContractNamespace == "http://schemas.microsoft.com/2006/04/mex" &&
                    operationContext.IncomingMessageHeaders.Action == "http://schemas.xmlsoap.org/ws/2004/09/transfer/Get")
                {
                    return true;
                }

                IPrincipal wp = new WindowsPrincipal(operationContext.ServiceSecurityContext.WindowsIdentity);

                return wp.IsInRole(RegistrySettings.ServiceAdminsGroup);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking authorization");
                throw;
            }
        }
    }
}
