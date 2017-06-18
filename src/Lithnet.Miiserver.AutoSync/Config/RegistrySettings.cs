using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync
{
    using System.Text;

    internal static class RegistrySettings
    {
        private static RegistryKey key;

        private static HashSet<string> retryCodes;

        public static RegistryKey BaseKey
        {
            get
            {
                if (RegistrySettings.key == null)
                {
                    RegistrySettings.key = Registry.LocalMachine.OpenSubKey("Software\\Lithnet\\MiisAutoSync");
                }

                return RegistrySettings.key;
            }
        }

        public static string LogPath
        {
            get
            {
                string logPath = RegistrySettings.BaseKey.GetValue("LogPath") as string;

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
                string value = RegistrySettings.BaseKey.GetValue("ConfigPath") as string;
                return value ?? Global.AssemblyDirectory;
            }
        }

        public static int MailMaxErrors
        {
            get
            {
                string s = RegistrySettings.BaseKey.GetValue("MailMaxErrors") as string;

                int value;

                return int.TryParse(s, out value) ? value : 10;
            }
        }

        public static int RunHistoryAge
        {
            get
            {
                string s = RegistrySettings.BaseKey.GetValue("RunHistoryAge") as string;

                int value;

                return int.TryParse(s, out value) ? value : 0;
            }
        }

        public static int RetryCount
        {
            get
            {
                string s = RegistrySettings.BaseKey.GetValue("RetryCount") as string;

                int value;

                return int.TryParse(s, out value) ? value : 5;
            }
        }

        public static TimeSpan ExecutionStaggerInterval
        {
            get
            {
                string s = RegistrySettings.BaseKey.GetValue("ExecutionStaggerInterval") as string;

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
                string s = RegistrySettings.BaseKey.GetValue("PostRunInterval") as string;

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
                string s = RegistrySettings.BaseKey.GetValue("RetrySleepInterval") as string;

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

        /// <summary>
        /// The amount of time to sleep in between querying for Get-RunProfileToExecute
        /// </summary>
        public static TimeSpan PSExecutionQueryInterval
        {
            get
            {
                string s = RegistrySettings.BaseKey.GetValue("PSExecutionQueryInterval") as string;

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

                    string[] values = RegistrySettings.BaseKey.GetValue("RetryCodes") as string[];

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
                string s = RegistrySettings.BaseKey.GetValue("UnmanagedChangesCheckInterval") as string;

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

        public static string RunHistoryPath => RegistrySettings.BaseKey.GetValue("RunHistoryPath") as string;

        public static string MailServer => RegistrySettings.BaseKey.GetValue("MailServer") as string;

        public static int MailServerPort
        {
            get
            {
                string s = RegistrySettings.BaseKey.GetValue("MailServerPort") as string;

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


        public static bool RunSyncExclusive => RegistrySettings.BaseKey.GetValue("ExclusiveSync") as string == "1";

        public static bool RunAllExclusive => RegistrySettings.BaseKey.GetValue("ExclusiveAll") as string == "1";


        public static bool MailSendOncePerStateChange => RegistrySettings.BaseKey.GetValue("MailSendOncePerStateChange") as string != "0";

        public static bool RunHistorySave => RegistrySettings.BaseKey.GetValue("RunHistorySave") as string == "1";

        public static bool MailEnabled => RegistrySettings.BaseKey.GetValue("MailEnabled") as string == "1";

        public static bool UseAppConfigMailSettings => RegistrySettings.BaseKey.GetValue("UseAppConfigMailSettings") as string == "1";

        public static string MailFrom => RegistrySettings.BaseKey.GetValue("MailFrom") as string;

        public static string[] MailTo => (RegistrySettings.BaseKey.GetValue("MailTo") as string)?.Split(';');

        public static string[] MailIgnoreReturnCodes
        {
            get
            {
                string value = RegistrySettings.BaseKey.GetValue("MailIgnoreReturnCodes") as string;

                return value?.Split(';') ?? new[] { "completed-no-objects", "success" };
            }
        }

        public static string GetSettingsString()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine($"{nameof(RegistrySettings.ConfigPath)}: {RegistrySettings.ConfigPath}");
            builder.AppendLine($"{nameof(RegistrySettings.LogPath)}: {RegistrySettings.LogPath}");
            builder.AppendLine($"{nameof(RegistrySettings.MailEnabled)}: {RegistrySettings.MailEnabled}");
            builder.AppendLine($"{nameof(RegistrySettings.UseAppConfigMailSettings)}: {RegistrySettings.UseAppConfigMailSettings}");
            builder.AppendLine($"{nameof(RegistrySettings.MailFrom)}: {RegistrySettings.MailFrom}");
            builder.AppendLine($"{nameof(RegistrySettings.MailIgnoreReturnCodes)}: {string.Join(",", RegistrySettings.MailIgnoreReturnCodes)}");
            builder.AppendLine($"{nameof(RegistrySettings.MailMaxErrors)}: {RegistrySettings.MailMaxErrors}");
            builder.AppendLine($"{nameof(RegistrySettings.MailSendOncePerStateChange)}: {RegistrySettings.MailSendOncePerStateChange}");
            builder.AppendLine($"{nameof(RegistrySettings.MailServer)}: {RegistrySettings.MailServer}");
            builder.AppendLine($"{nameof(RegistrySettings.MailServerPort)}: {RegistrySettings.MailServerPort}");
            builder.AppendLine($"{nameof(RegistrySettings.MailTo)}: {string.Join(",", RegistrySettings.MailTo)}");
            builder.AppendLine($"{nameof(RegistrySettings.RunHistoryAge)}: {RegistrySettings.RunHistoryAge}");
            builder.AppendLine($"{nameof(RegistrySettings.RunHistoryPath)}: {RegistrySettings.RunHistoryPath}");
            builder.AppendLine($"{nameof(RegistrySettings.RunHistorySave)}: {RegistrySettings.RunHistorySave}");
            builder.AppendLine($"{nameof(RegistrySettings.UnmanagedChangesCheckInterval)}: {RegistrySettings.UnmanagedChangesCheckInterval}");
            builder.AppendLine($"{nameof(RegistrySettings.ExecutionStaggerInterval)}: {RegistrySettings.ExecutionStaggerInterval}");
            builder.AppendLine($"{nameof(RegistrySettings.RunSyncExclusive)}: {RegistrySettings.RunSyncExclusive}");
            builder.AppendLine($"{nameof(RegistrySettings.RunAllExclusive)}: {RegistrySettings.RunAllExclusive}");
            builder.AppendLine($"{nameof(RegistrySettings.PostRunInterval)}: {RegistrySettings.PostRunInterval}");
            builder.AppendLine($"{nameof(RegistrySettings.RetryCount)}: {RegistrySettings.RetryCount}");
            builder.AppendLine($"{nameof(RegistrySettings.RetrySleepInterval)}: {RegistrySettings.RetrySleepInterval}");
            builder.AppendLine($"{nameof(RegistrySettings.RetryCodes)}: {string.Join(",", RegistrySettings.RetryCodes)}");

            return builder.ToString();
        }
    }
}

