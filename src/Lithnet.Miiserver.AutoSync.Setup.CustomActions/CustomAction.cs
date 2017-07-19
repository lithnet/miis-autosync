using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;

namespace Lithnet.Miiserver.AutoSync.Setup.CustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult GetAdminGroupName(Session session)
        {
            SecurityIdentifier sid = null;

            try
            {
                session.Log("Attempting to get administrators group SID");
                sid = Lithnet.Miiserver.Client.SyncServer.GetAdministratorsGroupSid();
            }
            catch (Exception ex)
            {
                session.Log(ex.ToString());
            }

            if (sid == null)
            {
                session.Log("Get administrator group SID failed");
                return ActionResult.Failure;
            }

            session.Log("Got administrators group SID");
            session["SERVICE_PERMISSION_GROUP"] = sid.Translate(typeof(NTAccount)).Value;

            return ActionResult.Success;
        }
    }
}
