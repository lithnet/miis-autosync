using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Configuration;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync
{
    using System.Text;

    internal static class Settings
    {
        private static RegistryKey key;

        private static HashSet<string> retryCodes;

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

        public static string LogPath
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

        public static int MailMaxErrors
        {
            get
            {
                string s = Settings.BaseKey.GetValue("MailMaxErrors") as string;

                int value;

                return int.TryParse(s, out value) ? value : 10;
            }
        }

        public static int RunHistoryAge
        {
            get
            {
                string s = Settings.BaseKey.GetValue("RunHistoryAge") as string;

                int value;

                return int.TryParse(s, out value) ? value : 0;
            }
        }

        public static int RetryCount
        {
            get
            {
                string s = Settings.BaseKey.GetValue("RetryCount") as string;

                int value;

                return int.TryParse(s, out value) ? value : 5;
            }
        }

        public static TimeSpan ExecutionStaggerInterval
        {
            get
            {
                string s = Settings.BaseKey.GetValue("ExecutionStaggerInterval") as string;

                int seconds;

                if (int.TryParse(s, out seconds))
                {
                    return new TimeSpan(0, 0, seconds >= 1 ? seconds : 1);
                }
                else
                {
                    return new TimeSpan(0, 0, 2);
                }
            }
        }

        /// <summary>
        /// The amount of time after the run profile completes before analysis of the run profile results starts
        /// </summary>
        public static TimeSpan PostRunInterval
        {
            get
            {
                string s = Settings.BaseKey.GetValue("PostRunInterval") as string;

                int seconds;

                if (int.TryParse(s, out seconds))
                {
                    return new TimeSpan(0, 0, seconds >= 1 ? seconds : 1);
                }
                else
                {
                    return new TimeSpan(0, 0, 2);
                }
            }
        }


        /// <summary>
        /// The amount of time to sleep after a deadlock event before retrying
        /// </summary>
        public static TimeSpan RetrySleepInterval
        {
            get
            {
                string s = Settings.BaseKey.GetValue("RetrySleepInterval") as string;

                int seconds;

                if (int.TryParse(s, out seconds))
                {
                    return new TimeSpan(0, 0, seconds >= 1 ? seconds : 1);
                }
                else
                {
                    return new TimeSpan(0, 0, 5);
                }
            }
        }

        public static HashSet<string> RetryCodes
        {
            get
            {
                if (retryCodes == null)
                {
                    retryCodes = new HashSet<string>();

                    string[] values = Settings.BaseKey.GetValue("RetryCodes") as string[];

                    if (values != null && values.Length > 0)
                    {
                        foreach (string s in values)
                        {
                            retryCodes.Add(s);
                        }
                    }
                    else
                    {
                        retryCodes.Add("stopped-deadlocked");
                    }
                }

                return retryCodes;
            }
        }

        public static TimeSpan UnmanagedChangesCheckInterval
        {
            get
            {
                string s = Settings.BaseKey.GetValue("UnmanagedChangesCheckInterval") as string;

                int seconds;

                if (int.TryParse(s, out seconds))
                {
                    return new TimeSpan(0, 0, seconds > 0 ? seconds : 3600);
                }
                else
                {
                    return new TimeSpan(0, 60, 0);
                }
            }
        }

        public static string RunHistoryPath => Settings.BaseKey.GetValue("RunHistoryPath") as string;

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


        public static bool RunSyncExclusive => Settings.BaseKey.GetValue("ExclusiveSync") as string == "1";

        public static bool RunAllExclusive => Settings.BaseKey.GetValue("ExclusiveAll") as string == "1";


        public static bool MailSendOncePerStateChange => Settings.BaseKey.GetValue("MailSendOncePerStateChange") as string != "0";

        public static bool RunHistorySave => Settings.BaseKey.GetValue("RunHistorySave") as string == "1";

        public static bool MailEnabled => Settings.BaseKey.GetValue("MailEnabled") as string == "1";

        public static bool UseAppConfigMailSettings => Settings.BaseKey.GetValue("UseAppConfigMailSettings") as string == "1";

        public static string MailFrom => Settings.BaseKey.GetValue("MailFrom") as string;

        public static string[] MailTo => (Settings.BaseKey.GetValue("MailTo") as string)?.Split(';');

        public static string[] MailIgnoreReturnCodes
        {
            get
            {
                string value = Settings.BaseKey.GetValue("MailIgnoreReturnCodes") as string;

                return value?.Split(';') ?? new[] { "completed-no-objects", "success" };
            }
        }

        public static string GetSettingsString()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine($"{nameof(Settings.ConfigPath)}: {Settings.ConfigPath}");
            builder.AppendLine($"{nameof(Settings.LogPath)}: {Settings.LogPath}");
            builder.AppendLine($"{nameof(Settings.MailEnabled)}: {Settings.MailEnabled}");
            builder.AppendLine($"{nameof(Settings.UseAppConfigMailSettings)}: {Settings.UseAppConfigMailSettings}");
            builder.AppendLine($"{nameof(Settings.MailFrom)}: {Settings.MailFrom}");
            builder.AppendLine($"{nameof(Settings.MailIgnoreReturnCodes)}: {string.Join(",", Settings.MailIgnoreReturnCodes)}");
            builder.AppendLine($"{nameof(Settings.MailMaxErrors)}: {Settings.MailMaxErrors}");
            builder.AppendLine($"{nameof(Settings.MailSendOncePerStateChange)}: {Settings.MailSendOncePerStateChange}");
            builder.AppendLine($"{nameof(Settings.MailServer)}: {Settings.MailServer}");
            builder.AppendLine($"{nameof(Settings.MailServerPort)}: {Settings.MailServerPort}");
            builder.AppendLine($"{nameof(Settings.MailTo)}: {string.Join(",", Settings.MailTo)}");
            builder.AppendLine($"{nameof(Settings.RunHistoryAge)}: {Settings.RunHistoryAge}");
            builder.AppendLine($"{nameof(Settings.RunHistoryPath)}: {Settings.RunHistoryPath}");
            builder.AppendLine($"{nameof(Settings.RunHistorySave)}: {Settings.RunHistorySave}");
            builder.AppendLine($"{nameof(Settings.UnmanagedChangesCheckInterval)}: {Settings.UnmanagedChangesCheckInterval}");
            builder.AppendLine($"{nameof(Settings.ExecutionStaggerInterval)}: {Settings.ExecutionStaggerInterval}");
            builder.AppendLine($"{nameof(Settings.RunSyncExclusive)}: {Settings.RunSyncExclusive}");
            builder.AppendLine($"{nameof(Settings.RunAllExclusive)}: {Settings.RunAllExclusive}");
            builder.AppendLine($"{nameof(Settings.PostRunInterval)}: {Settings.PostRunInterval}");
            builder.AppendLine($"{nameof(Settings.RetryCount)}: {Settings.RetryCount}");
            builder.AppendLine($"{nameof(Settings.RetrySleepInterval)}: {Settings.RetrySleepInterval}");
            builder.AppendLine($"{nameof(Settings.RetryCodes)}: {string.Join(",", Settings.RetryCodes)}");

            return builder.ToString();
        }
    }
}

