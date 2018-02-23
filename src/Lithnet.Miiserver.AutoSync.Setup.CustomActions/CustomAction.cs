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
            try
            {
                Trace.WriteLine("Starting set is local");
                string group = session["GROUP_FIM_SYNC_ADMINS_NAME"];

                Principal result = FindInDomainOrMachine(group);

                if (result == null)
                {
                    Trace.WriteLine($"group {group} not found");

                    session["GROUP_FIM_SYNC_ADMINS_NOT_FOUND"] = "1";
                    session["GROUP_FIM_SYNC_ADMINS_IS_DOMAIN"] = null;
                    session["GROUP_FIM_SYNC_ADMINS_IS_LOCAL"] = null;
                    return ActionResult.Success;
                }

                Trace.WriteLine($"Found principal {result.SamAccountName} in context {result.ContextType}:{result.Context.ConnectedServer}");

                if (!(result is GroupPrincipal))
                {
                    Trace.WriteLine($"The specified object was not a group {group}");

                    session["GROUP_FIM_SYNC_ADMINS_NOT_FOUND"] = "1";
                    session["GROUP_FIM_SYNC_ADMINS_IS_DOMAIN"] = null;
                    session["GROUP_FIM_SYNC_ADMINS_IS_LOCAL"] = null;
                    return ActionResult.Success;
                }

                session["GROUP_FIM_SYNC_ADMINS_NOT_FOUND"] = null;
                session["GROUP_FIM_SYNC_ADMINS_IS_LOCAL"] = result.ContextType == ContextType.Machine ? "1" : null;
                session["GROUP_FIM_SYNC_ADMINS_IS_DOMAIN"] = result.ContextType == ContextType.Domain ? "1" : null;

                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log(ex.ToString());
                return ActionResult.Failure;
            }
            finally
            {
                Trace.WriteLine($"Set IsLocal to {session["GROUP_FIM_SYNC_ADMINS_IS_LOCAL"]}");
                Trace.WriteLine($"Set IsDomain to {session["GROUP_FIM_SYNC_ADMINS_IS_DOMAIN"]}");
                Trace.WriteLine($"Set GroupNotFound to {session["GROUP_FIM_SYNC_ADMINS_NOT_FOUND"]}");
            }
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

                session.Log($"Got administrators group SID: {sid}");
                session["GROUP_FIM_SYNC_ADMINS_NAME"] = sid.Translate(typeof(NTAccount)).Value;
            }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult AddServiceAccountToFimSyncAdmins(Session session)
        {
            string account = session.CustomActionData["SERVICE_USERNAME"];
            string groupName = session.CustomActionData["GROUP_FIM_SYNC_ADMINS_NAME"];

            while (true)
            {
                try
                {
                    session.Log($"Attempting to add user {account} to {groupName}");
                    AddUserToGroup(session, account, groupName);
                    session.Log("Done");
                    return ActionResult.Success;
                }
                catch (Exception ex)
                {
                    session.Log($"Could not add user {account} to group {groupName}");
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

        [CustomAction]
        public static ActionResult AddSpnsToServiceAccount(Session session)
        {
            string account = session.CustomActionData["SERVICE_USERNAME"];

            try
            {
                SecurityIdentifier wellKnownSid = CustomActions.GetSidIfWellKnownNetworkOrSystemAccount(account);
                if (wellKnownSid != null)
                {
                    session.Log($"Skipping SPN addition as the target is a well-known account");
                    return ActionResult.Success;
                }

                session.Log($"Attempting add SPNs to user {account}");
                AddSpns(session, account);
                session.Log("Done");
                return ActionResult.Success;

            }
            catch (Exception ex)
            {
                session.Log($"Could not add SPNs to {account}");
                session.Log(ex.ToString());
                return ActionResult.Failure;
            }
        }

        private static void AddSpns(Session session, string account)
        {
            Principal user = CustomActions.FindInDomainOrMachine(account);

            if (user == null)
            {
                throw new NoMatchingPrincipalException($"The user {account} could not be found");
            }

            if (user.ContextType != ContextType.Domain)
            {
                session.Log($"Cannot add SPN to a local account. Exiting.");
                return;
            }

            HashSet<string> hostnames = new HashSet<string>();

            hostnames.Add(SpnInterop.GetComputerDnsName(ComputerNameFormat.ComputerNameDnsFullyQualified));
            hostnames.Add(SpnInterop.GetComputerDnsName(ComputerNameFormat.ComputerNameNetBios));

            SpnInterop.SetSpn("autosync", hostnames.ToArray(), user.DistinguishedName);

            foreach (string hostname in hostnames)
            {
                session.Log($"Added spn autosync/{hostname} to {user.DistinguishedName}");
            }
        }

        private static void AddUserToGroup(Session session, string account, string groupName)
        {
            if (!(CustomActions.FindInDomainOrMachine(groupName) is GroupPrincipal group))
            {
                throw new NoMatchingPrincipalException($"The group {groupName} could not be found");
            }

            SecurityIdentifier wellKnownSid = CustomActions.GetSidIfWellKnownNetworkOrSystemAccount(account);
            Principal principal;

            if (wellKnownSid == null)
            {
                principal = CustomActions.FindInDomainOrMachine(account);

                if (principal == null)
                {
                    throw new NoMatchingPrincipalException($"The user {account} could not be found");
                }
            }
            else
            {
                if (group.ContextType == ContextType.Machine)
                {
                    PrincipalContext context = new PrincipalContext(ContextType.Machine);
                    principal = Principal.FindByIdentity(context, IdentityType.Sid, wellKnownSid.ToString());
                }
                else
                {
                    PrincipalContext context = new PrincipalContext(ContextType.Domain);
                    principal = Principal.FindByIdentity(context, Environment.MachineName);
                }
            }

            IADsGroup nativeGroup = (IADsGroup)((DirectoryEntry)group.GetUnderlyingObject()).NativeObject;

            if (CustomActions.IsPrincipalInGroup(nativeGroup, principal))
            {
                session.Log($"User {account} was already in group {groupName}");
                return;
            }
            else
            {
                session.Log($"User {account} was not in group {groupName}");
            }

            CustomActions.AddPrincipalToGroup(nativeGroup, principal);
        }

        private static void AddPrincipalToGroup(IADsGroup nativeGroup, Principal principal)
        {
            SecurityIdentifier sid = principal.Sid;

            try
            {
                string path;

                if (nativeGroup.ADsPath.StartsWith("winnt", StringComparison.OrdinalIgnoreCase))
                {
                    path = $"WINNT://{sid}";
                }
                else
                {
                    path = ((DirectoryEntry)principal.GetUnderlyingObject()).Path;
                }

                nativeGroup.Add(path);
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
                if ((uint)e.HResult == 0x80071392 || (uint)e.HResult == 0x80070562)
                {
                    //session.Log($"User {account} was already in group {groupName} - 0x80071392");
                    return;
                }

                throw;
            }
        }

        private static bool IsPrincipalInGroup(IADsGroup nativeGroup, Principal principal)
        {
            return CustomActions.IsSidInGroup(nativeGroup, principal.Sid);
        }

        private static bool IsSidInGroup(IADsGroup nativeGroup, SecurityIdentifier userSid)
        {
            foreach (object item in nativeGroup.Members())
            {
                byte[] s = (byte[])item.GetType().InvokeMember("ObjectSid", System.Reflection.BindingFlags.GetProperty, null, item, null);
                SecurityIdentifier sid = new SecurityIdentifier(s, 0);
                if (userSid == sid)
                {
                    return true;
                }
            }

            return false;
        }

        private static Principal FindInDomainOrMachine(string accountName)
        {
            PrincipalContext context = new PrincipalContext(ContextType.Domain);
            Principal p = Principal.FindByIdentity(context, accountName);

            if (p == null)
            {
                string authority = accountName.Split('\\')[0];

                if ((!accountName.Contains("\\")) || authority.Equals(Environment.MachineName, StringComparison.InvariantCultureIgnoreCase) || authority.Equals("."))
                {
                    context = new PrincipalContext(ContextType.Machine);
                    p = Principal.FindByIdentity(context, accountName);
                    return p;
                }
            }

            return p;
        }

        public static SecurityIdentifier GetSidIfWellKnownNetworkOrSystemAccount(string accountName)
        {
            try
            {
                NTAccount account = new NTAccount(accountName);
                SecurityIdentifier sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));

                if (sid.IsWellKnown(WellKnownSidType.NetworkServiceSid) || sid.IsWellKnown(WellKnownSidType.LocalSystemSid))
                {
                    return sid;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Well known account SID not found");
                Trace.WriteLine(ex);
            }

            return null;
        }
    }
}
