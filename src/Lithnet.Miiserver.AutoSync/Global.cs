using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.ServiceProcess;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Global
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
            if (Global.maNametoIDMapping == null)
            {
                Global.maNametoIDMapping = ManagementAgent.GetManagementAgentNameAndIDPairs();
            }

            foreach (KeyValuePair<Guid, string> k in Global.maNametoIDMapping)
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

        public static void ThrowOnSyncEngineNotRunning()
        {
            if (!IsSyncEngineRunning())
            {
                throw new SyncEngineStoppedException("The MIM Synchronization service is not running");
            }
        }
    }
}