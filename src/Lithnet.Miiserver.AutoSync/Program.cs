using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;
using System.IO;
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.Timers;
using System.Net.Mail;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Program
    {
        private static List<MAExecutor> maExecutors = new List<MAExecutor>();

        private static Timer runHistoryCleanupTimer;

        private static Timer pingTimer;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            bool runService = args != null && args.Contains("/service");
            Logger.LogPath = Settings.LogPath;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (!runService)
            {
                Logger.OutputToConsole = true;
            }

            if (runService)
            {
                try
                {
                    Logger.WriteLine("Starting service base");
                    ServiceBase[] servicesToRun = { new AutoSyncService() };
                    ServiceBase.Run(servicesToRun);
                    Logger.WriteLine("Exiting service");
                }
                catch (Exception ex)
                {
                    Logger.WriteException(ex);
                    throw;
                }
            }
            else
            {
                Logger.WriteLine("Starting standalone process");
                Start();
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.WriteException((Exception)e.ExceptionObject);
        }

        internal static void Start()
        {
            try
            {
                if (!IsInFimAdminsGroup())
                {
                    throw new UnauthorizedAccessException("The user must be a member of the FIMSyncAdmins group");
                }

                Logger.WriteSeparatorLine('-');
                Logger.WriteLine("--- Global settings ---");
                Logger.WriteLine(Settings.GetSettingsString());
                Logger.WriteSeparatorLine('-');

                if (Settings.RunHistoryAge > 0)
                {
                    Logger.WriteLine("Run history auto-cleanup enabled");
                    Program.runHistoryCleanupTimer = new Timer
                    {
                        AutoReset = true
                    };

                    Program.runHistoryCleanupTimer.Elapsed += RunHistoryCleanupTimer_Elapsed;
                    Program.runHistoryCleanupTimer.Interval = TimeSpan.FromHours(8).TotalMilliseconds;
                    Program.runHistoryCleanupTimer.Start();
                    
                }

                Program.pingTimer = new Timer();
                Program.pingTimer.AutoReset = true;
                Program.pingTimer.Elapsed += PingTimer_Elapsed;
                Program.pingTimer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
                Program.pingTimer.Start();

                EnumerateMAs();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        private static void PingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!SyncServer.Ping())
            {
                Logger.WriteLine("Sync server ping failed!");
                MessageSender.SendMessage("Sync server may not be responding", "The sync server has not responded to ping requests");
                pingTimer.Stop();
            }
            else
            {
                Logger.WriteLine("Sync server ping ok");
            }
        }

        private static bool IsInFimAdminsGroup()
        {
            return SyncServer.IsAdmin();
        }

        private static void RunHistoryCleanupTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Program.ClearRunHistory();
        }

        private static void ClearRunHistory()
        {
            if (Settings.RunHistoryAge <= 0)
            {
                return;
            }

            Logger.WriteLine("Clearing run history older than {0} days", Settings.RunHistoryAge);
            DateTime clearBeforeDate = DateTime.UtcNow.AddDays(-Settings.RunHistoryAge);

            if (Settings.RunHistorySave && Settings.RunHistoryPath != null)
            {
                string file = Path.Combine(Settings.RunHistoryPath, $"history-{DateTime.Now.ToString("yyyy-MM-ddThh.mm.ss")}.xml");
                SyncServer.ClearRunHistory(clearBeforeDate, file);
            }
            else
            {
                SyncServer.ClearRunHistory(clearBeforeDate);
            }
        }

        public static void LoadAndStartMAs()
        {
            maExecutors = new List<MAExecutor>();
            Logger.OutputToConsole = true;
            EnumerateMAs();
        }

        public static void Stop()
        {
            foreach (MAExecutor x in maExecutors)
            {
                x.Stop();
            }
        }

        internal static void EnumerateMAs()
        {
            foreach (ManagementAgent ma in ManagementAgent.GetManagementAgents())
            {
                IList<object> configItems = GetMAConfigParameters(ma).ToList();

                MAConfigParameters config = MAConfigDiscovery.GetConfig(ma, configItems);

                if (config == null)
                {
                    continue;
                }

                if (!config.Disabled)
                {
                    IList<IMAExecutionTrigger> triggers = MAExecutionTriggerDiscovery.GetExecutionTriggers(ma, config, configItems);
                    MAExecutor x = new MAExecutor(ma, config);
                    x.AttachTrigger(triggers.ToArray());
                    Program.maExecutors.Add(x);
                }
                else
                {
                    Logger.WriteLine("{0}: Skipping management agent because it has been disabled in config", ma.Name);
                }
            }

            foreach (MAExecutor x in Program.maExecutors)
            {
                x.Start();
            }
        }

        private static IEnumerable<object> GetMAConfigParameters(ManagementAgent ma)
        {
            string expectedFileName = Path.Combine(Settings.ConfigPath, $"Config-{Global.CleanMAName(ma.Name)}.ps1");
            if (!File.Exists(expectedFileName))
            {
                yield break;
            }

            Logger.WriteLine("{0}: Getting configuration from {1}", ma.Name, expectedFileName);

            PowerShell powershell = PowerShell.Create();
            powershell.AddScript(File.ReadAllText(expectedFileName));
            powershell.Invoke();

            if (powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("Get-MAConfiguration", CommandTypes.All) == null)
            {
                throw new ArgumentException($"The file '{expectedFileName}' must contain a function called Get-MAConfiguration");
            }

            powershell.AddCommand("Get-MAConfiguration");
            Collection<PSObject> results = powershell.Invoke();

            foreach (PSObject o in results)
            {
                Hashtable ht = o.BaseObject as Hashtable;

                if (ht != null)
                {
                    yield return new MAConfigParameters(ht);
                }
                else
                {
                    yield return o.BaseObject;
                }
            }
        }
    }
}
