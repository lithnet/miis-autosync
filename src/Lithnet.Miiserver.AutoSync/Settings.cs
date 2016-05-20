using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace Lithnet.Miiserver.AutoSync
{
    using System.IO;
    using System.Reflection;

    public static class Settings
    {
        public static string LogFile
        {
            get
            {
                string logFile = ConfigurationManager.AppSettings["logFile"];

                if (logFile != null)
                {
                    return logFile;
                }

                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                string dirName = Path.GetDirectoryName(path);
                return Path.Combine(dirName, "Logs\\autosync.log");
            }
        }
        
        public static int MailMaxErrorItems
        {
            get
            {
                string s = ConfigurationManager.AppSettings["mailMaxErrors"];

                int value = 0;

                if (int.TryParse(s, out value))
                {
                    return value;
                }
                else
                {
                    return 10;
                }
            }
        }

        public static int RunHistoryNumberOfDaysToKeep
        {
            get
            {
                string s = ConfigurationManager.AppSettings["runHistoryDaysToKeep"];

                int value = 0;

                if(int.TryParse(s, out value))
                {
                    return value;
                }
                else
                {
                    return 0;
                }
            }
        }

        public static int ExecutionStaggerInterval
        {
            get
            {
                string s = ConfigurationManager.AppSettings["executionStaggerInterval"];

                int value;

                if (int.TryParse(s, out value))
                {
                    return value >= 1 ? value : 1;
                }
                else
                {
                    return 2;
                }
            }
        }

        public static int UnmanagedChangesCheckInterval
        {
            get
            {
                string s = ConfigurationManager.AppSettings["unmanagedChangesCheckIntervalMinutes"];

                int value = 0;

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

        public static string RunHistorySavePath => ConfigurationManager.AppSettings["runHistoryExportPath"];

        public static string MailServer => ConfigurationManager.AppSettings["mailServer"];

        public static bool MailSendOncePerStateChange => ConfigurationManager.AppSettings["mailSendOncePerStateChange"] == null || Convert.ToBoolean(ConfigurationManager.AppSettings["mailSendOncePerStateChange"]);

        public static string MailFrom => ConfigurationManager.AppSettings["mailFrom"];

        public static string[] MailTo => ConfigurationManager.AppSettings["mailTo"]?.Split(';');

        public static string[] MailIgnoreReturnCodes => ConfigurationManager.AppSettings["mailIgnoreReturnCodes"]?.Split(';');
    }
}
