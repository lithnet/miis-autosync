using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;
using System.IO;
using System.Timers;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Program
    {
        private static List<MAExecutor> maExecutors = new List<MAExecutor>();

        private static Timer runHistoryCleanupTimer;

        private static ServiceHost configServiceHost;

        internal static ConfigFile ActiveConfig;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            bool runService = args != null && args.Contains("/service");
            Logger.LogPath = RegistrySettings.LogPath;
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

        public static void StartConfigServiceHost()
        {
            Program.configServiceHost = ConfigService.CreateInstance();
        }

        internal static void Start()
        {
            try
            {
                if (!IsInFimAdminsGroup())
                {
                    throw new UnauthorizedAccessException("The user must be a member of the FIMSyncAdmins group");
                }

                Program.StartConfigServiceHost();

                Logger.WriteSeparatorLine('-');
                Logger.WriteLine("--- Global settings ---");
                Logger.WriteLine(RegistrySettings.GetSettingsString());
                Logger.WriteSeparatorLine('-');

                if (RegistrySettings.RunHistoryAge > 0)
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

                Program.StartMAExecutors();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
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
            if (RegistrySettings.RunHistoryAge <= 0)
            {
                return;
            }

            Logger.WriteLine("Clearing run history older than {0} days", RegistrySettings.RunHistoryAge);
            DateTime clearBeforeDate = DateTime.UtcNow.AddDays(-RegistrySettings.RunHistoryAge);

            if (RegistrySettings.RunHistorySave && RegistrySettings.RunHistoryPath != null)
            {
                string file = Path.Combine(RegistrySettings.RunHistoryPath, $"history-{DateTime.Now.ToString("yyyy-MM-ddThh.mm.ss")}.xml");
                SyncServer.ClearRunHistory(clearBeforeDate, file);
            }
            else
            {
                SyncServer.ClearRunHistory(clearBeforeDate);
            }
        }

        public static void Stop()
        {
            try
            {
                if (Program.runHistoryCleanupTimer != null)
                {
                    if (Program.runHistoryCleanupTimer.Enabled)
                    {
                        Program.runHistoryCleanupTimer.Stop();
                    }
                }

                if (configServiceHost != null && configServiceHost.State == CommunicationState.Opened)
                {
                    configServiceHost.Close();
                }

                List<Task> stopTasks = new List<Task>();

                foreach (MAExecutor x in maExecutors)
                {
                    stopTasks.Add(Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            x.Stop();
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }));
                }

                Logger.WriteLine("Waiting for executors to stop");

                if (!Task.WaitAll(stopTasks.ToArray(), 90000))
                {
                    Logger.WriteLine("Timeout waiting for executors to stop");
                    Environment.Exit(2);
                }
                else
                {
                    Logger.WriteLine("Shutdown complete");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred during termination");
                Logger.WriteException(ex);
                Environment.Exit(3);
            }
        }

        public static void LoadConfiguration()
        {
            string path = RegistrySettings.ConfigurationFile;

            if (!File.Exists(path))
            {
                Program.ActiveConfig = new ConfigFile();
                Program.ActiveConfig.ValidateManagementAgents();
            }
            else
            {
                Program.ActiveConfig = ConfigFile.Load(path);
            }
        }

        private static void StartMAExecutors()
        {
            foreach (MAConfigParameters config in Program.ActiveConfig.ManagementAgents)
            {
                if (config.IsNew)
                {
                    Logger.WriteLine("{0}: Skipping management agent because it does not yet have any configuration defined", config.ManagementAgentName);
                    continue;
                }

                if (config.IsMissing)
                {
                    Logger.WriteLine("{0}: Skipping management agent because it is missing from the Sync Engine", config.ManagementAgentName);
                    continue;
                }

                if (config.Disabled)
                {
                    Logger.WriteLine("{0}: Skipping management agent because it has been disabled in config", config.ManagementAgentName);
                    continue;
                }

                MAExecutor x = new MAExecutor(config);
                Program.maExecutors.Add(x);
            }

            foreach (MAExecutor x in Program.maExecutors)
            {
                x.Start();
            }
        }
    }
}
