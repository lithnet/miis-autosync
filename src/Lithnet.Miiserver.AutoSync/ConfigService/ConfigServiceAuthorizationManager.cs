using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Security.Principal;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigServiceAuthorizationManager : ServiceAuthorizationManager
    {
        private const string AuthZGroupName = "FimSyncAdmins";

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            // Allow MEX requests through. 
            if (operationContext.EndpointDispatcher.ContractName == ServiceMetadataBehavior.MexContractName &&
                operationContext.EndpointDispatcher.ContractNamespace == "http://schemas.microsoft.com/2006/04/mex" &&
                operationContext.IncomingMessageHeaders.Action == "http://schemas.xmlsoap.org/ws/2004/09/transfer/Get")
            {
                return true;
            }
            
            IPrincipal wp = new WindowsPrincipal(operationContext.ServiceSecurityContext.WindowsIdentity);

            return wp.IsInRole(ConfigServiceAuthorizationManager.AuthZGroupName);
        }
    }
}
