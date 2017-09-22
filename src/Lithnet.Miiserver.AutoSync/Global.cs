using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class Global
    {
        private static Random random = new Random();

        private static Dictionary<Guid, string> maNametoIDMapping;

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public static string CleanMAName(string name)
        {
            string cleanName = name;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanName = cleanName.Replace(c, '-');
            }

            return cleanName;
        }

        public static int RandomizeOffset(double number)
        {
            return RandomizeOffset((int)number, 10);
        }

        public static int RandomizeOffset(int number)
        {
            return RandomizeOffset(number, 10);
        }

        public static int RandomizeOffset(int number, int offsetPercent)
        {
            return random.Next(number - (number / offsetPercent), number + (number / offsetPercent));
        }

        private static ServiceController serviceController = new ServiceController("fimsynchronizationservice");

        public static bool IsSyncEngineRunning()
        {
            return serviceController.Status == ServiceControllerStatus.Running;
        }

        public static Guid? FindManagementAgent(string name, Guid id)
        {
            foreach (KeyValuePair<Guid, string> k in Global.MANameIDMapping)
            {
                if (id == k.Key)
                {
                    return k.Key;
                }

                if (string.Equals(name, k.Value, StringComparison.CurrentCultureIgnoreCase))
                {
                    return k.Key;
                }
            }

            return null;
        }

        public static string GetManagementAgentName(Guid id)
        {
            if (Global.MANameIDMapping.ContainsKey(id))
            {
                return Global.MANameIDMapping[id];
            }
            else
            {
                return null;
            }
        }

        public static Guid? GetManagementAgentID(string name)
        {
            foreach (KeyValuePair<Guid, string> kvp in Global.MANameIDMapping)
            {
                if (string.Equals(kvp.Value, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            return null;
        }


        public static Dictionary<Guid, string> MANameIDMapping
        {
            get
            {
                if (Global.maNametoIDMapping == null)
                {
                    Global.maNametoIDMapping = ManagementAgent.GetManagementAgentNameAndIDPairs();
                }

                return Global.maNametoIDMapping;
            }
        }

        public static void ThrowOnSyncEngineNotRunning()
        {
            if (!IsSyncEngineRunning())
            {
                throw new SyncEngineStoppedException("The MIM Synchronization service is not running");
            }
        }

        public static string[] GetNtAccountName(string accountName)
        {
            if (accountName.Contains("\\"))
            {
                return accountName.Split('\\');
            }

            if (accountName.Contains("@"))
            {
                NTAccount account = new NTAccount(accountName);
                SecurityIdentifier sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
                string sidString = sid.Value;

                sid = new SecurityIdentifier(sidString);

                account = (NTAccount)sid.Translate(typeof(NTAccount));

                return account.Value.Split('\\');
            }

            throw new ArgumentException($"The username '{accountName}' was not in a supported format");
        }
    }
}