using System;
using System.IO;
using System.Reflection;
using System.Configuration;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class Settings
    {
        private static RegistryKey key;

        public static RegistryKey BaseKey
        {
            get
            {
                if (Settings.key == null)
                {
                    Settings.key = Registry.LocalMachine.OpenSubKey("Software\\Lithnet\\MiisAutoSync");
                }

                return Settings.key;
            }
        }

        public static string LogFile
        {
            get
            {
                string logPath = Settings.BaseKey.GetValue("LogPath") as string;

                if (logPath != null)
                {
                    return Path.Combine(logPath, "autosync.log");
                }

                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                string dirName = Path.GetDirectoryName(path);

                return Path.Combine(dirName ?? Global.AssemblyDirectory, "Logs\\autosync.log");
            }
        }

        public static string ConfigPath
        {
            get
            {
                string value = Settings.BaseKey.GetValue("ConfigPath") as string;
                return value ?? Global.AssemblyDirectory;
            }
        }

        public static int MailMaxErrorItems
        {
            get
            {
                string s = Settings.BaseKey.GetValue("MailMaxErrors") as string;

                int value;

                return int.TryParse(s, out value) ? value : 10;
            }
        }

        public static int RunHistoryNumberOfDaysToKeep
        {
            get
            {
                string s = Settings.BaseKey.GetValue("RunHistoryAge") as string;

                int value;

                return int.TryParse(s, out value) ? value : 0;
            }
        }

        public static int ExecutionStaggerInterval
        {
            get
            {
                string s = Settings.BaseKey.GetValue("ExecutionStaggerInterval") as string;

                int value;

                if (int.TryParse(s, out value))
                {
                    return value >= 1 ? value : 1;
                }
                else
                {
                    return 5;
                }
            }
        }

        public static int UnmanagedChangesCheckInterval
        {
            get
            {
                string s = Settings.BaseKey.GetValue("UnmanagedChangesCheckIntervalMinutes") as string;

                int value;

                if (int.TryParse(s, out value))
                {
                    return value * 60 * 1000;
                }
                else
                {
                    return 60 * 60 * 1000;
                }
            }
        }

        public static string RunHistorySavePath => Settings.BaseKey.GetValue("RunHistoryPath") as string;

        public static string MailServer => Settings.BaseKey.GetValue("MailServer") as string;

        public static int MailServerPort
        {
            get
            {
                string s = Settings.BaseKey.GetValue("MailServerPort") as string;

                int value;

                if (int.TryParse(s, out value))
                {
                    return value > 0 ? value : 25;
                }
                else
                {
                    return 25;
                }
            }
        }

        public static bool MailSendOncePerStateChange => Settings.BaseKey.GetValue("MailSendOncePerStateChange") as string != "0";

        public static bool SaveRunHistory => Settings.BaseKey.GetValue("RunHistorySave") as string == "1";

        public static bool MailEnabled => Settings.BaseKey.GetValue("MailEnabled") as string == "1";

        public static bool UseAppConfigMailSettings => Settings.BaseKey.GetValue("UseAppConfigMailSettings") as string == "1";

        public static string MailFrom => Settings.BaseKey.GetValue("MailFrom") as string;

        public static string[] MailTo => (Settings.BaseKey.GetValue("MailTo") as string)?.Split(';');

        public static string[] MailIgnoreReturnCodes
        {
            get
            {
                string value = Settings.BaseKey.GetValue("IgnoreReturnCodes") as string;

                return value?.Split(';') ?? new[] { "completed-no-objects", "success" };
            }
        }
    }
}

