using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class RegistrySettings
    {
        private static RegistryKey parametersKey;

        private static RegistryKey serviceKey;

        private static HashSet<string> retryCodes;

        public static RegistryKey ParametersKey
        {
            get
            {
                if (RegistrySettings.parametersKey == null)
                {
                    RegistrySettings.parametersKey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\autosync\\Parameters", true);
                }

                return RegistrySettings.parametersKey;
            }
        }

        public static RegistryKey ServiceKey
        {
            get
            {
                if (RegistrySettings.serviceKey == null)
                {
                    RegistrySettings.serviceKey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services\\autosync", false);
                }

                return RegistrySettings.serviceKey;
            }
        }

        public static string ServiceAccount => (string) RegistrySettings.ServiceKey.GetValue("ObjectName", null);

        public static bool AutoStartEnabled
        {
            get
            {
                int? value = RegistrySettings.ParametersKey.GetValue(nameof(AutoStartEnabled), 1) as int?;
                return value.HasValue && value.Value != 0;
            }
            set
            {
                RegistrySettings.ParametersKey.SetValue(nameof(AutoStartEnabled), value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        public static bool GetSyncLockForFimMAExport
        {
            get
            {
                int? value = RegistrySettings.ParametersKey.GetValue(nameof(GetSyncLockForFimMAExport), 0) as int?;
                return value.HasValue && value.Value != 0;
            }
        }

        public static string LogPath
        {
            get
            {
                string logPath = RegistrySettings.ParametersKey.GetValue(nameof(RegistrySettings.LogPath)) as string;

                if (logPath != null)
                {
                    return logPath;
                }

                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                string dirName = Path.GetDirectoryName(path);

                return Path.Combine(dirName ?? Global.AssemblyDirectory, "Logs");
            }
        }

        public static string LogFile
        {
            get
            {
                string logPath = RegistrySettings.LogPath;

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

        public static string ServiceAdminsGroup
        {
            get
            {
                string value = RegistrySettings.ParametersKey.GetValue(nameof(ServiceAdminsGroup)) as string;

                if (value != null)
                {
                    return value;
                }

                return "FimSyncAdmins";
            }
        }

        public static bool NetTcpServerEnabled
        {
            get
            {
                int? value = RegistrySettings.ParametersKey.GetValue(nameof(NetTcpServerEnabled), 0) as int?;
                return value.HasValue && value.Value != 0;
            }
        }

        public static string NetTcpBindAddress
        {
            get
            {
                return (string) RegistrySettings.ParametersKey.GetValue(nameof(NetTcpBindAddress), Environment.MachineName);
            }
        }

        public static int NetTcpBindPort
        {
            get
            {
                return (int) RegistrySettings.ParametersKey.GetValue(nameof(NetTcpBindPort), 54338);
            }
        }

        public static string ConfigurationFile
        {
            get
            {
                string path = RegistrySettings.ParametersKey.GetValue("ConfigFile") as string;

                if (path != null)
                {
                    return path;
                }

                return Path.Combine(Global.AssemblyDirectory, "config.xml");
            }
        }

        public static HashSet<string> RetryCodes
        {
            get
            {
                if (retryCodes == null)
                {
                    retryCodes = new HashSet<string>();

                    string[] values = RegistrySettings.ParametersKey.GetValue(nameof(RetryCodes)) as string[];

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

        public static int RetryCount
        {
            get
            {
                return (int) RegistrySettings.ParametersKey.GetValue(nameof(RetryCount), 5);
            }
        }

        public static TimeSpan RetrySleepInterval
        {
            get
            {
                int seconds = (int)RegistrySettings.ParametersKey.GetValue(nameof(RetrySleepInterval), 0);

                if (seconds > 0)
                {
                    return new TimeSpan(0, 0, seconds);
                }
                else
                {
                    return TimeSpan.FromSeconds(5);
                }
            }
        }

        public static TimeSpan ExecutionStaggerInterval
        {
            get
            {
                int seconds = (int)RegistrySettings.ParametersKey.GetValue(nameof(ExecutionStaggerInterval), 0);

                if (seconds > 0)
                {
                    return new TimeSpan(0, 0, seconds);
                }
                else
                {
                    return TimeSpan.FromSeconds(2);
                }
            }
        }

        public static TimeSpan PostRunInterval
        {
            get
            {
                int seconds = (int)RegistrySettings.ParametersKey.GetValue(nameof(PostRunInterval), 0);

                if (seconds > 0)
                {
                    return new TimeSpan(0, 0, seconds);
                }
                else
                {
                    return TimeSpan.FromSeconds(2);
                }
            }
        }

        public static TimeSpan UnmanagedChangesCheckInterval
        {
            get
            {
                int minutes = (int)RegistrySettings.ParametersKey.GetValue(nameof(UnmanagedChangesCheckInterval), 0);

                if (minutes > 0)
                {
                    return new TimeSpan(0, minutes, 0);
                }
                else
                {
                    return TimeSpan.FromHours(1);
                }
            }
        }

        public static TimeSpan RunHistoryTimerInterval
        {
            get
            {
                int minutes = (int) RegistrySettings.ParametersKey.GetValue(nameof(RunHistoryTimerInterval), 0);

                if (minutes > 0)
                {
                    return new TimeSpan(0, minutes, 0);
                }
                else
                {
                    return TimeSpan.FromHours(8);
                }
            }
        }
    }
}
