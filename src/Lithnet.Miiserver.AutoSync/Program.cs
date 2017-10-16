using System;
using System.Linq;
using System.ServiceProcess;
using Lithnet.Miiserver.Client;
using System.IO;
using System.Timers;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Program
    {
        private static ConfigFile activeConfig;

        internal static ExecutionEngine Engine { get; set; }

        private static Timer runHistoryCleanupTimer;

        private static ServiceHost npConfigServiceHost;

        private static ServiceHost tcpConfigServiceHost;

        internal static ConfigFile ActiveConfig
        {
            get => Program.activeConfig;
            set
            {
                Program.activeConfig = value;
                logger.Info("Active config has been set");
            }
        }

        private static bool hasConfig;

        private static Logger logger;

        private static void SetupLogger()
        {
#if DEBUG

            if (System.Diagnostics.Debugger.IsAttached)
            {
                DebuggerTarget debug = new DebuggerTarget("debug-window") { Layout = "${longdate}|${level:uppercase=true:padding=5}| ${message}" };
                LogManager.Configuration.AddTarget(debug);
                LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, debug));
            }

            if (LogManager.Configuration != null)
            {
                foreach (LoggingRule item in LogManager.Configuration.LoggingRules.Where(t => t.Targets.Any(u => u.Name == "autosync-service-file")))
                {
                    item.EnableLoggingForLevels(LogLevel.Trace, LogLevel.Fatal);
                }

                LogManager.ReconfigExistingLoggers();
            }
#endif

            Program.logger = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            Program.SetupLogger();

            bool runService = args != null && args.Contains("/service");

            if (runService)
            {
                try
                {
                    Program.logger.Info("Starting service base");
                    ServiceBase[] servicesToRun = { new AutoSyncService() };
                    ServiceBase.Run(servicesToRun);
                    Program.logger.Info("Exiting service");
                }
                catch (Exception ex)
                {
                    Program.logger.Fatal(ex, "The service could not be started");
                    throw;
                }
            }
            else
            {
                Program.logger.Info("Starting standalone process");
                Program.Start();
                Thread.Sleep(Timeout.Infinite);
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Program.logger.Error(e.Exception, "A task exception was not observed");
            e.SetObserved();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Program.logger.Fatal((Exception)e.ExceptionObject, "An unhandled exception has occurred in the service");
            Environment.Exit(1);
        }

        private static void StartConfigServiceHost()
        {
            if (Program.npConfigServiceHost == null || Program.npConfigServiceHost.State != CommunicationState.Opened)
            {
                Program.npConfigServiceHost = ConfigService.CreateNetNamedPipeInstance();
                Program.logger.Info("Initialized service host pipe");

                if (RegistrySettings.NetTcpServerEnabled)
                {
                    Program.tcpConfigServiceHost = ConfigService.CreateNetTcpInstance();
                    Program.logger.Info("Initialized service host tcp");
                }
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
                    Program.logger.Warn("Service is not yet configured. Run the editor initialize the configuration");
                    return;
                }

                Program.InitializeRunHistoryCleanup();
                Program.StartExecutionEngineInstance();
            }
            catch (Exception ex)
            {
                Program.logger.Error(ex, "Unable to start service");
                throw;
            }
        }

        private static void InitializeRunHistoryCleanup()
        {
            if (ActiveConfig.Settings.RunHistoryClear)
            {
                Program.logger.Info("Run history auto-cleanup enabled");

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
                Program.logger.Info("Clearing run history older than {0}", ActiveConfig.Settings.RunHistoryAge);
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
                Program.logger.Error(ex, "An error occurred clearing the run history");
            }
        }

        internal static void Stop()
        {
            try
            {
                Program.StopRunHistoryCleanupTimer();

                if (Program.npConfigServiceHost != null && Program.npConfigServiceHost.State == CommunicationState.Opened)
                {
                    Program.npConfigServiceHost.Close();
                }

                if (Program.tcpConfigServiceHost != null && Program.tcpConfigServiceHost.State == CommunicationState.Opened)
                {
                    Program.tcpConfigServiceHost.Close();
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
                Program.logger.Fatal(ex, "An error occurred during termination");
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

        private static void LoadConfiguration()
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
            Program.logger.Info("Creating execution engine");

            try
            {
                Program.Engine = new ExecutionEngine();
            }
            catch (Exception ex)
            {
                Program.logger.Fatal(ex, "Could not create execution engine instance");
                throw;
            }
        }

        private static void StartExecutionEngineInstance()
        {
            Program.logger.Info("Starting execution engine");

            if (RegistrySettings.AutoStartEnabled)
            {
                Program.Engine.Start();
            }
            else
            {
                Program.logger.Info("Execution engine auto-start disabled");
            }
        }

        private static void ShutdownExecutionEngineInstance()
        {
            Program.logger.Info("Stopping execution engine");
            Program.Engine?.Stop(false);
            Program.Engine?.ShutdownService();
            Program.Engine = null;
        }

#if DEBUG
        public static void SetupOutOfBandInstance()
        {
            LogManager.Configuration = new XmlLoggingConfiguration(@"D:\github\lithnet\miis-autosync\src\Lithnet.Miiserver.AutoSync\bin\Debug\Lithnet.Miiserver.AutoSync.exe.config");

            foreach (LoggingRule item in LogManager.Configuration.LoggingRules.Where(t => t.Targets.Any(u => u.Name == "autosync-service-file")))
            {
                item.EnableLoggingForLevels(LogLevel.Trace, LogLevel.Fatal);
            }

            LogManager.ReconfigExistingLoggers();

            Program.SetupLogger();
            Program.LoadConfiguration();
            Program.StartConfigServiceHost();
            Program.CreateExecutionEngineInstance();
        }
#endif
    }
}