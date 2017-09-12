using System;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync.UI
{
    internal class UserSettings
    {
        private static RegistryKey userSettingsKey;

        public static RegistryKey UserSettingsKey
        {
            get
            {
                if (UserSettings.userSettingsKey == null)
                {
                    UserSettings.userSettingsKey = Registry.CurrentUser.CreateSubKey("Software\\Lithnet\\AutoSync");
                }

                return UserSettings.userSettingsKey;
            }
        }

        public static int ReconnectInterval
        {
            get
            {
                return (int)UserSettings.UserSettingsKey.GetValue(nameof(ReconnectInterval), 10000);
            }
        }

        public static string AutoSyncServerHost
        {
            get
            {
                return (string)UserSettings.UserSettingsKey.GetValue(nameof(AutoSyncServerHost), "localhost");
            }
        }

        public static string AutoSyncServerPort
        {
            get
            {
                return (string)UserSettings.UserSettingsKey.GetValue(nameof(AutoSyncServerPort), "54338");
            }
        }

        public static string AutoSyncServerIdentity
        {
            get
            {
                return (string)UserSettings.UserSettingsKey.GetValue(nameof(AutoSyncServerIdentity), "autosync/{0}");
            }
        }

    }
}
