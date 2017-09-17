using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;
using System.DirectoryServices.AccountManagement;
using Lithnet.Miiserver.Client;

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
                sid = SyncServer.GetAdministratorsGroupSid();
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
        public static ActionResult AddServiceAccountToFimSyncAdmins(Session session)
        {
            string account = session["SERVICE_USERNAME"];
            string group = session["GROUP_FIM_SYNC_ADMINS"];
            string groupName = session["GROUP_FIM_SYNC_ADMINS_NAME"];

            while (true)
            {
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
                    session.Log("Could not add user {0} to group {1}", account, group);
                    session.Log(ex.ToString());

                    const int val = (int) InstallMessage.User | (int) MessageButtons.OKCancel | (int) MessageIcon.Error;

                    MessageResult result = session.Message((InstallMessage) val,
                        new Record($"Unable to add '{account}' to the group '{groupName}'. Please add this user manually to the group and press OK to continue, or press Cancel to exit.\n{ex.Message}"));

                    if (result != MessageResult.OK)
                    {
                        return ActionResult.Failure;
                    }
                }
            }
        }

        private static void AddUserToGroup(string account, string groupSid)
        { 
            GroupPrincipal group = CustomActions.FindInDomainOrMachineBySid(groupSid) as GroupPrincipal;

            bool mustSave = false;

            if (group == null)
            {
                throw new NoMatchingPrincipalException($"The group {groupSid} could not be found");
            }

            Principal user = CustomActions.FindInDomainOrMachine(account);

            if (user == null)
            {
                throw new NoMatchingPrincipalException($"The user {account} could not be found");
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
