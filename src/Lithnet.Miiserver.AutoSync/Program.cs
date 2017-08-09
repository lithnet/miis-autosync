using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;
using System.IO;
using System.Timers;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Threading;
using Timer = System.Timers.Timer;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Program
    {
        private static ConfigFile activeConfig;

        private static ConfigFile currentConfig;

        private static ExecutionEngine engine;

        private static Timer runHistoryCleanupTimer;

        private static ServiceHost configServiceHost;

        private static AppDomain executionDomain;

        internal static ConfigFile ActiveConfig
        {
            get => Program.activeConfig;
            set
            {
                Program.activeConfig = value;
                Program.currentConfig = value;
                Program.PendingRestart = false;
                Logger.WriteLine("Active config has been set");
            }
        }

        internal static ConfigFile CurrentConfig
        {
            get => Program.currentConfig;
            set
            {
                Program.currentConfig = value;
                Program.PendingRestart = true;
                Logger.WriteLine("Current config has been set");
            }
        }

        internal static bool PendingRestart { get; set; }

        private static bool hasConfig;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            bool runService = args != null && args.Contains("/service");
            Logger.LogPath = RegistrySettings.LogPath;

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
                Thread.Sleep(Timeout.Infinite);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.WriteLine("An unhandled exception has occurred in the service");
            Logger.WriteException((Exception)e.ExceptionObject);
            Environment.Exit(1);
        }

        public static void StartConfigServiceHost()
        {
            if (Program.configServiceHost == null || Program.configServiceHost.State != CommunicationState.Opened)
            {
                Program.configServiceHost = ConfigService.CreateInstance();
                Logger.WriteLine("Initialized service host");
            }
        }

        internal static void Start()
        {
            try
            {
                if (!CheckSyncEnginePermissions())
                {
                    throw new UnauthorizedAccessException("The user must be a member of the FIMSyncAdmins or FIMSyncOperators group");
                }

                Program.LoadConfiguration();
                Program.StartConfigServiceHost();

                if (!Program.hasConfig)
                {
                    Logger.WriteLine("Service is not yet configured. Run the editor initialize the configuration");
                    return;
                }

                Program.InitializeRunHistoryCleanup();
                Program.CreateExecutionEngineInstance();
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
                Program.RestartExecutionEngine();
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

        private static bool CheckSyncEnginePermissions()
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
                    Program.ShutdownExecutionEngineInstance();
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

        private static void CreateExecutionEngineInstance()
        {
            Logger.WriteLine("Starting execution engine");
            Program.engine = new ExecutionEngine();
            Program.engine.Start();
        }

        private static void ShutdownExecutionEngineInstance()
        {
            Logger.WriteLine("Stopping execution engine");
            Program.engine?.Stop();
            Program.engine = null;
        }

        private static void RestartExecutionEngine()
        {
            Logger.WriteLine("Restarting execution engine");

            Program.ShutdownExecutionEngineInstance();
            Program.CreateExecutionEngineInstance();
        }
    }
}