using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Security.Principal;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigServiceAuthorizationManager : ServiceAuthorizationManager
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

                WindowsPrincipal wp = new WindowsPrincipal(operationContext.ServiceSecurityContext.WindowsIdentity);

                bool result = wp.IsInRole(RegistrySettings.ServiceAdminsGroup);

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error checking authorization");
                throw;
            }
        }
    }
}
