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
        private static ConfigFile activeConfig;

        private static ConfigFile currentConfig;
        
        private static List<MAExecutor> maExecutors;

        private static Timer runHistoryCleanupTimer;

        private static ServiceHost configServiceHost;

        internal static ConfigFile ActiveConfig
        {
            get => Program.activeConfig;
            set
            {
                Program.activeConfig = value;
                Program.CurrentConfig = value;
                Logger.WriteLine("Active config has been set");
            }
        }

        internal static ConfigFile CurrentConfig
        {
            get => Program.currentConfig;
            set
            {
                Program.currentConfig = value;
                Logger.WriteLine("Current config has been set");
            }
        }

        private static bool hasConfig;

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
            if (Program.configServiceHost == null || Program.configServiceHost.State != CommunicationState.Opened)
            {
                Program.configServiceHost = ConfigService.CreateInstance();
            }
        }

        internal static void Start()
        {
            try
            {
                if (!IsInFimAdminsGroup())
                {
                    throw new UnauthorizedAccessException("The user must be a member of the FIMSyncAdmins group");
                }

                Program.LoadConfiguration();
                Program.StartConfigServiceHost();

                if (!Program.hasConfig)
                {
                    Logger.WriteLine("Service is not yet configured. Run the editor initialize the configuration");
                    return;
                }

                Program.InitializeRunHistoryCleanup();
                Program.StartMAExecutors();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        internal static void Reload()
        {
            try
            {
                Program.StopRunHistoryCleanupTimer();
                Program.StopMAExecutors();
                Program.Start();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred during the service reload process");
                Logger.WriteException(ex);
                throw;
            }
        }

        private static void InitializeRunHistoryCleanup()
        {
            if (ActiveConfig.Settings.RunHistoryClear)
            {
                Logger.WriteLine("Run history auto-cleanup enabled");

                Program.runHistoryCleanupTimer = new Timer
                {
                    AutoReset = true
                };

                Program.runHistoryCleanupTimer.Elapsed += Program.RunHistoryCleanupTimer_Elapsed;
                Program.runHistoryCleanupTimer.Interval = RegistrySettings.RunHistoryTimerInterval.TotalMilliseconds;
                Program.runHistoryCleanupTimer.Start();
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
            if (ActiveConfig.Settings.RunHistoryAge.TotalSeconds <= 0)
            {
                return;
            }

            try
            {
                Logger.WriteLine("Clearing run history older than {0}", ActiveConfig.Settings.RunHistoryAge);
                DateTime clearBeforeDate = DateTime.UtcNow.Add(-ActiveConfig.Settings.RunHistoryAge);

                if (ActiveConfig.Settings.RunHistorySave
                    && ActiveConfig.Settings.RunHistoryPath != null)
                {
                    string file = Path.Combine(ActiveConfig.Settings.RunHistoryPath, $"history-{DateTime.Now:yyyy-MM-ddThh.mm.ss}.xml");
                    SyncServer.ClearRunHistory(clearBeforeDate, file);
                }
                else
                {
                    SyncServer.ClearRunHistory(clearBeforeDate);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred clearing the run history");
                Logger.WriteException(ex);
            }
        }

        public static void Stop()
        {
            try
            {
                Program.StopRunHistoryCleanupTimer();

                if (configServiceHost != null && configServiceHost.State == CommunicationState.Opened)
                {
                    configServiceHost.Close();
                }

                try
                {
                    Program.StopMAExecutors();
                }
                catch (System.TimeoutException)
                {
                    Environment.Exit(2);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred during termination");
                Logger.WriteException(ex);
                Environment.Exit(3);
            }
        }

        private static void StopRunHistoryCleanupTimer()
        {
            if (Program.runHistoryCleanupTimer != null)
            {
                if (Program.runHistoryCleanupTimer.Enabled)
                {
                    Program.runHistoryCleanupTimer.Stop();
                }
            }
        }

        private static void StopMAExecutors()
        {
            if (maExecutors == null)
            {
                return;
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
                throw new System.TimeoutException();
            }
            else
            {
                Logger.WriteLine("Executors stopped successfully");
            }
        }

        public static void LoadConfiguration()
        {
            string path = RegistrySettings.ConfigurationFile;

            if (!File.Exists(path))
            {
                Program.ActiveConfig = new ConfigFile();
                Program.ActiveConfig.ValidateManagementAgents();
                Program.hasConfig = false;
            }
            else
            {
                Program.ActiveConfig = ConfigFile.Load(path);
                Program.hasConfig = true;
            }
        }

        private static void StartMAExecutors()
        {
            Program.maExecutors = new List<MAExecutor>();

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
                Task.Run(x.Start);
            }
        }
    }
}
