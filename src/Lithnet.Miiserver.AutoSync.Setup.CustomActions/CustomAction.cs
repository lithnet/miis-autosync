using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;
using System.DirectoryServices.AccountManagement;

namespace Lithnet.Miiserver.AutoSync.Setup.CustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult GetFimGroups(Session session)
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
            }
            else
            {
                session.Log("Got administrators group SID");
                session["GROUP_FIM_SYNC_ADMINS"] = sid.ToString();
                session["GROUP_FIM_SYNC_ADMINS_NAME"] = sid.Translate(typeof(NTAccount)).Value;
            }

            try
            {
                session.Log("Attempting to get operators group SID");
                sid = Lithnet.Miiserver.Client.SyncServer.GetOperatorsGroupSid();
            }
            catch (Exception ex)
            {
                session.Log(ex.ToString());
            }

            if (sid == null)
            {
                session.Log("Get operators group SID failed");
            }
            else
            {
                session.Log("Got operators group SID");
                session["GROUP_FIM_SYNC_OPERATORS"] = sid.ToString();
                session["GROUP_FIM_SYNC_OPERATORS_NAME"] = sid.Translate(typeof(NTAccount)).Value;
            }

            return ActionResult.Success;
        }

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

        [CustomAction]
        public static ActionResult GetOperatorsGroupName(Session session)
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

        [CustomAction]
        public static ActionResult AddServiceAccountToFimSyncOperators(Session session)
        {
            string account = session["SERVICE_USERNAME"];
            string group = session["GROUP_FIM_SYNC_OPERATORS"];

            try
            {
                session.Log($"Attempting to add user {account} to {group}");
                AddUserToGroup(account, group);
                session.Log($"Added user {account} to {group}");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {

                session["GROUP_ADD_ACTION_FAILED"] = "1";
                session["WIXUI_EXITDIALOGOPTIONALTEXT"] = $"The installer was unable to add the user {account} to the FIM Sync Operators group. Please add this user manually to the group before starting the AutoSync service.";
                session.Log("!Could not add user {0} to group {1}", account, group);
                session.Log(ex.ToString());
                                
                session.Message(InstallMessage.User, new Record($"The installer was unable to add the user {0} to the FIM Sync Operators group. Please add this user manually to the group before starting the AutoSync service.", account));
                return ActionResult.Success;
            }
        }

        private static void AddUserToGroup(string account, string groupSid)
        { 
            PrincipalContext context = new PrincipalContext(ContextType.Machine);
            GroupPrincipal group = CustomActions.FindInDomainOrMachineBySid(groupSid) as GroupPrincipal;

            bool mustSave = false;

            if (group == null)
            {
                throw new NoMatchingPrincipalException(string.Format("The group {0} could not be found", groupSid));
            }

            Principal user = CustomActions.FindInDomainOrMachine(account);

            if (user == null)
            {
                throw new NoMatchingPrincipalException(string.Format("The user {0} could not be found", account));
            }
            
            if (!group.Members.Contains(user))
            {
                group.Members.Add(user);
                mustSave = true;
            }

            if (mustSave)
            {
                group.Save();
            }
        }
        
        private static Principal FindInDomainOrMachine(string accountName)
        {
            PrincipalContext context = new PrincipalContext(ContextType.Domain);
            Principal p = Principal.FindByIdentity(context, accountName);

            if (p == null)
            {
                context = new PrincipalContext(ContextType.Machine);
                p = Principal.FindByIdentity(context, accountName);
            }

            return p;
        }

        private static Principal FindInDomainOrMachineBySid(string sid)
        {
            PrincipalContext context = new PrincipalContext(ContextType.Domain);
            Principal p = Principal.FindByIdentity(context, IdentityType.Sid, sid);

            if (p == null)
            {
                context = new PrincipalContext(ContextType.Machine);
                p = Principal.FindByIdentity(context, sid);
            }

            return p;
        }
    }
}
