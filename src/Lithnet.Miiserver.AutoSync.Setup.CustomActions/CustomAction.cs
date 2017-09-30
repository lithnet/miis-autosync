using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.Security.Principal;
using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using System.DirectoryServices.AccountManagement;
using ActiveDs;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync.Setup.CustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult SetIsLocalProperty(Session session)
        {
            string sid = session["GROUP_FIM_SYNC_ADMINS"];
            FindInDomainOrMachineBySid(sid, out bool isMachine);
            session["GROUP_FIM_SYNC_ADMINS_IS_LOCAL"] = isMachine ? "1" : null;
            session["GROUP_FIM_SYNC_ADMINS_IS_DOMAIN"] = !isMachine ? "1" : null;

            return ActionResult.Success;
        }

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
                //#warning remove this
                //sid = (SecurityIdentifier)new NTAccount("Fim-dev1\\idm-gg-fimadmins").Translate(typeof(SecurityIdentifier));

                session.Log("Got administrators group SID");
                session["GROUP_FIM_SYNC_ADMINS"] = sid.ToString();
                session["GROUP_FIM_SYNC_ADMINS_NAME"] = sid.Translate(typeof(NTAccount)).Value;
            }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult AddServiceAccountToFimSyncAdmins(Session session)
        {
            string account = session.CustomActionData["SERVICE_USERNAME"];
            string group = session.CustomActionData["GROUP_FIM_SYNC_ADMINS"];
            string groupName = session.CustomActionData["GROUP_FIM_SYNC_ADMINS_NAME"];

            while (true)
            {
                try
                {
                    session.Log($"Attempting to add user {account} to {groupName} ({group})");
                    AddUserToGroup(session, account, group, groupName);
                    session.Log("Done");
                    return ActionResult.Success;
                }
                catch (Exception ex)
                {
                    session.Log($"Could not add user {account} to group {groupName} ({group})");
                    session.Log(ex.ToString());

                    const int val = (int)InstallMessage.User | (int)MessageButtons.OKCancel | (int)MessageIcon.Error;

                    MessageResult result = session.Message((InstallMessage)val,
                        new Record($"Unable to add '{account}' to the group '{groupName}'. Please add this user manually to the group and press OK to continue, or press Cancel to exit.\n{ex.Message}"));

                    if (result != MessageResult.OK)
                    {
                        return ActionResult.Failure;
                    }
                }
            }
        }

        private static void AddUserToGroup(Session session, string account, string groupSid, string groupName)
        {
            bool isMachine;

            GroupPrincipal group = CustomActions.FindInDomainOrMachineBySid(groupSid, out isMachine) as GroupPrincipal;

            if (group == null)
            {
                throw new NoMatchingPrincipalException($"The group {groupName} ({groupSid}) could not be found");
            }

            UserPrincipal user = (UserPrincipal)CustomActions.FindInDomainOrMachine(account, out isMachine);

            if (user == null)
            {
                throw new NoMatchingPrincipalException($"The user {account} could not be found");
            }

            DirectoryEntry gde = (DirectoryEntry)group.GetUnderlyingObject();
            IADsGroup nativeGroup = (IADsGroup)gde.NativeObject;

            foreach (object item in nativeGroup.Members())
            {
                byte[] s = (byte[])item.GetType().InvokeMember("ObjectSid", System.Reflection.BindingFlags.GetProperty, null, item, null);
                SecurityIdentifier sid = new SecurityIdentifier(s, 0);
                if (user.Sid == sid)
                {
                    session.Log($"User {account} was already in group {groupName} ({groupSid})");
                    return;
                }
            }

            session.Log($"User {account} was not in group {groupName} ({groupSid})");

            try
            {
                if (gde.Path.StartsWith("winnt", StringComparison.OrdinalIgnoreCase))
                {
                    session.Log($"Adding WINNT://{user.Sid} to group {gde.Path}");
                    nativeGroup.Add($"WINNT://{user.Sid}");
                }
                else
                {
                    DirectoryEntry ude = (DirectoryEntry)user.GetUnderlyingObject();
                    session.Log($"Adding {ude.Path} to group {gde.Path}");
                    nativeGroup.Add(ude.Path);
                }
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
                if (e.HResult == -2147019886) //unchecked((int)0x80071392))
                {
                    session.Log($"User {account} was already in group {groupName} ({groupSid}) - 0x80071392");
                    return;
                }

                throw;
            }
        }

        private static Principal FindInDomainOrMachine(string accountName, out bool isMachine)
        {
            isMachine = false;
            PrincipalContext context = new PrincipalContext(ContextType.Domain);
            Principal p = Principal.FindByIdentity(context, accountName);

            if (p == null)
            {
                context = new PrincipalContext(ContextType.Machine);
                p = Principal.FindByIdentity(context, accountName);
                isMachine = true;
            }

            return p;
        }

        private static Principal FindInDomainOrMachineBySid(string sid, out bool isMachine)
        {
            isMachine = false;
            PrincipalContext context = new PrincipalContext(ContextType.Domain);
            Principal p = Principal.FindByIdentity(context, IdentityType.Sid, sid);

            if (p == null)
            {
                context = new PrincipalContext(ContextType.Machine);
                p = Principal.FindByIdentity(context, sid);
                isMachine = true;
            }

            return p;
        }
    }
}
