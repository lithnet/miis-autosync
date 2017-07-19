using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Security.Principal;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigServiceAuthorizationManager : ServiceAuthorizationManager
    {
        private static SecurityIdentifier authZGroup;

        private static SecurityIdentifier AuthZGroup
        {
            get
            {
                if (authZGroup == null)
                {
                    authZGroup = Lithnet.Miiserver.Client.SyncServer.GetAdministratorsGroupSid();
                }

                return authZGroup;
            }
        }

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            // Allow MEX requests through. 
            if (operationContext.EndpointDispatcher.ContractName == ServiceMetadataBehavior.MexContractName &&
                operationContext.EndpointDispatcher.ContractNamespace == "http://schemas.microsoft.com/2006/04/mex" &&
                operationContext.IncomingMessageHeaders.Action == "http://schemas.xmlsoap.org/ws/2004/09/transfer/Get")
            {
                return true;
            }

            WindowsPrincipal wp = new WindowsPrincipal(operationContext.ServiceSecurityContext.WindowsIdentity);

            if (ConfigServiceAuthorizationManager.AuthZGroup == null)
            {
                return false;
            }

            return wp.IsInRole(ConfigServiceAuthorizationManager.AuthZGroup);
        }
    }
}
