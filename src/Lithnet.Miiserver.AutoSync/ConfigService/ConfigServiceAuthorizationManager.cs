using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Security.Principal;
using System.ServiceProcess;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigServiceAuthorizationManager : ServiceAuthorizationManager
    {
        //private static SecurityIdentifier authZGroup;
        
        //private static SecurityIdentifier AuthZGroup
        //{
        //    get
        //    {
        //        if (authZGroup == null)
        //        {
        //            try
        //            {
        //                authZGroup = Lithnet.Miiserver.Client.SyncServer.GetAdministratorsGroupSid();
        //            }
        //            catch (Exception ex)
        //            {
        //                Logger.WriteException(ex);
        //                throw;
        //            }
        //        }

        //        return authZGroup;
        //    }
        //}

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

                //Trace.WriteLine($"User {wp.Identity.Name} is in service admin group {RegistrySettings.AdminGroup}: {result}");

                //if (!result)
                //{
                //    Trace.WriteLine("User is a member of these groups");
                //    WindowsIdentity identity = wp.Identity as WindowsIdentity;
                //    foreach (IdentityReference group in identity.Groups)
                //    {
                //        Trace.WriteLine(group.Value);
                //    }
                //}

                return result;

                //using (WindowsImpersonationContext i = operationContext.ServiceSecurityContext.WindowsIdentity.Impersonate())
                //{
                //    bool result = Lithnet.Miiserver.Client.SyncServer.IsAdmin();
                //    Trace.WriteLine($"User {Environment.UserName} is fim admin: {result}");
                //    return result;
                //}
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }
    }
}
