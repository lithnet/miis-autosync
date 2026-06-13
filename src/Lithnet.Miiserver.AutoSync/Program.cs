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
            // Register the global unhandled-exception handlers before any other work so
            // that failures in the installer service-registration verbs are also captured.
            // Those verbs run as a deferred LocalSystem MSI custom action, a context where
            // NLog's temp-file logging is unreliable (the write is silently dropped) and
            // where msiexec does not capture the process output, so an install failure would
            // otherwise produce no actionable diagnostics. The Windows event log written by
            // the handler (via Setup.WriteEventLogError) is the reliable channel there.
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Handle installer service-registration verbs ("setup install-service" /
            // "setup uninstall-service") before any normal startup. Setup.Process returns
            // true when it has handled a verb, in which case we exit.
            if (Setup.Process(args))
            {
                return;
            }

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
            // The logger is not configured until SetupLogger() runs, which is after the
            // installer verb path; guard against a null logger so the handler is safe to
            // register early.
            if (Program.logger != null)
            {
                Program.logger.Error(e.Exception, "A task exception was not observed");
            }

            e.SetObserved();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            // Write to the Windows event log first. This handler also covers the installer
            // service-registration path, which runs before SetupLogger() (so Program.logger
            // is null) and executes as a LocalSystem MSI custom action where file-based
            // logging is unreliable. The event log is always reachable in that context, so
            // it is the catch-all that guarantees an install/service failure is diagnosable.
            Setup.WriteEventLogError("An unhandled exception has occurred in Lithnet AutoSync." + Environment.NewLine + (ex != null ? ex.ToString() : Convert.ToString(e.ExceptionObject)));

            if (Program.logger != null)
            {
                try
                {
                    Program.logger.Fatal(ex, "An unhandled exception has occurred in the service");
                }
                catch
                {
                    // NLog logging is best-effort here; the event log above is the reliable channel.
                }
            }

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
            LogManager.Configuration = new XmlLoggingConfiguration(@"D:\dev\git\lithnet\miis-autosync\src\Lithnet.Miiserver.AutoSync\bin\Debug\Lithnet.Miiserver.AutoSync.exe.config");

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