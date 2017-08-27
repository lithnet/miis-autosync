using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;
using System.IO;
using System.Timers;
using System.ServiceModel;
using System.Threading;
using Timer = System.Timers.Timer;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Program
    {
        private static ConfigFile activeConfig;

        private static ExecutionEngine engine;

        private static Timer runHistoryCleanupTimer;

        private static ServiceHost configServiceHost;

        internal static ConfigFile ActiveConfig
        {
            get => Program.activeConfig;
            set
            {
                Program.activeConfig = value;
                Logger.WriteLine("Active config has been set");
            }
        }

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
                Program.CreateExecutionEngineInstance();

                if (!Program.hasConfig)
                {
                    Logger.WriteLine("Service is not yet configured. Run the editor initialize the configuration");
                    return;
                }

                Program.InitializeRunHistoryCleanup();
                Program.StartExecutionEngineInstance();
            }
            catch (Exception ex)
            {
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

        public static void CreateExecutionEngineInstance()
        {
            Logger.WriteLine("Creating execution engine");

            try
            {
                Program.engine = new ExecutionEngine();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Could not create execution engine instance");
                Logger.WriteException(ex);
            }
        }

        public static void StartExecutionEngineInstance()
        {
            Logger.WriteLine("Starting execution engine");

            if (RegistrySettings.AutoStartEnabled)
            {
                Program.engine.Start();
            }
            else
            {
                Logger.WriteLine("Execution engine auto-start disabled");
            }
        }

        private static void ShutdownExecutionEngineInstance()
        {
            Logger.WriteLine("Stopping execution engine");
            Program.engine?.Stop();
            Program.engine?.ShutdownService();
            Program.engine = null;
        }

        private static void RestartExecutionEngine()
        {
            Logger.WriteLine("Restarting execution engine");

            Program.ShutdownExecutionEngineInstance();
            Program.CreateExecutionEngineInstance();
        }

        internal static IList<MAStatus> GetMAState()
        {
            return engine?.GetMAState();
        }

        internal static MAStatus GetMAState(string maName)
        {
            return engine?.GetMAState(maName);
        }


        internal static void StopExecutors()
        {
            engine?.Stop();
        }

        internal static void StartExecutors()
        {
            engine?.Start();
        }

        internal static ExecutorState GetEngineState()
        {
            return engine?.State ?? ExecutorState.Stopped;
        }

        public static IList<string> GetManagementAgentsPendingRestart()
        {
            return engine?.GetManagementAgentsPendingRestart();
        }

        internal static void RestartChangedExecutors()
        {
            engine?.RestartChangedExecutors();
        }

        internal static void RestartManagementAgents(params string[] managementAgentNames)
        {
            foreach (string ma in managementAgentNames)
            {
                engine?.Stop(ma);
            }

            foreach (string ma in managementAgentNames)
            {
                engine?.Start(ma);
            }
        }

        internal static void StopExecutor(string managementAgentName)
        {
            engine?.Stop(managementAgentName);
        }

        internal static void StartExecutor(string managementAgentName)
        {
            engine?.Start(managementAgentName);
        }
    }
}