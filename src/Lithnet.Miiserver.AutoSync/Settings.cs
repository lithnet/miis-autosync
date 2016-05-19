using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Settings
    {
        public static int MailMaxErrorItems
        {
            get
            {
                return 10;
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

        public static string RunHistorySavePath
        {
            get
            {
                return ConfigurationManager.AppSettings["runHistoryExportPath"];
            }
        }

        public static string MailServer
        {
            get
            {
                return ConfigurationManager.AppSettings["mailServer"];
            }
        }

        public static bool MailSendOncePerStateChange
        {
            get
            {
                return ConfigurationManager.AppSettings["mailSendOncePerStateChange"] == null ? true : Convert.ToBoolean(ConfigurationManager.AppSettings["mailSendOncePerStateChange"]);
            }
        }

        public static string MailFrom
        {
            get
            {
                return ConfigurationManager.AppSettings["mailFrom"];
            }
        }

        public static string[] MailTo
        {
            get
            {
                return ConfigurationManager.AppSettings["mailTo"]?.Split(';');
            }
        }

        public static string[] MailIgnoreReturnCodes
        {
            get
            {
                return ConfigurationManager.AppSettings["mailIgnoreReturnCodes"]?.Split(';');
            }
        }
    }
}
