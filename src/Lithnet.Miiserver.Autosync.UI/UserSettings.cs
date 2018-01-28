using System;
using Microsoft.Win32;

namespace Lithnet.Miiserver.AutoSync.UI
{
    internal class UserSettings
    {
        public const int DefaultTcpPort = 54338;

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

        public static int ReconnectInterval => (int)UserSettings.UserSettingsKey.GetValue(nameof(ReconnectInterval), 10000);

        public static string AutoSyncServerHost
        {
            get => (string)UserSettings.UserSettingsKey.GetValue(nameof(AutoSyncServerHost), "localhost");
            set => UserSettings.UserSettingsKey.SetValue(nameof(AutoSyncServerHost), value);
        }

        public static int AutoSyncServerPort
        {
            get => (int)UserSettings.UserSettingsKey.GetValue(nameof(AutoSyncServerPort), DefaultTcpPort);
            set => UserSettings.UserSettingsKey.SetValue(nameof(AutoSyncServerPort), value > 0 ? value : DefaultTcpPort);
        }

        public static bool AutoConnect
        {
            get => (int)UserSettings.UserSettingsKey.GetValue(nameof(UserSettings.AutoConnect), 0) != 0;
            set => UserSettings.UserSettingsKey.SetValue(nameof(AutoConnect), value ? 1 : 0);
        }

        public static string AutoSyncServerIdentity => (string)UserSettings.UserSettingsKey.GetValue(nameof(AutoSyncServerIdentity), "autosync/{0}");
    }
}
