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
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Program
    {
        private static List<MAExecutor> maExecutors = new List<MAExecutor>();

        private static Timer runHistoryCleanupTimer;

        //private static Timer pingTimer;

        //private static bool pingFailed;

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

                //Program.pingTimer = new Timer();
                //Program.pingTimer.AutoReset = true;
                //Program.pingTimer.Elapsed += PingTimer_Elapsed;
                //Program.pingTimer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
                //Program.pingTimer.Start();

                EnumerateMAs();
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }

        //private static void PingTimer_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    if (!SyncServer.Ping())
        //    {
        //        Logger.WriteLine("Sync server ping failed!");
        //        if (!Program.pingFailed)
        //        {
        //            MessageSender.SendMessage("Sync server may not be responding", "The sync server has not responded to ping requests");
        //            Program.pingFailed = true;
        //        }
        //    }
        //    else
        //    {
        //        Logger.WriteLine("Sync server ping ok");
        //        Program.pingFailed = false;
        //    }
        //}

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
            try
            {
                if (Program.runHistoryCleanupTimer != null)
                {
                    if (Program.runHistoryCleanupTimer.Enabled)
                    {
                        Program.runHistoryCleanupTimer.Stop();
                    }
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

        internal static void EnumerateMAs()
        {
            ConfigFile loaded = Serializer.Read<ConfigFile>("D:\\temp\\config.xml");

            foreach(MAConfigParameters config in loaded.ManagementAgents)
            { 
                config.ResolveManagementAgent();
                if (config.IsMissing)
                {
                    Logger.WriteLine("Skipping missing management agent");
                }
               
                if (!config.Disabled)
                {
                    MAExecutor x = new MAExecutor(config);
                    Program.maExecutors.Add(x);
                }
                else
                {
                    Logger.WriteLine("{0}: Skipping management agent because it has been disabled in config", config.ManagementAgentName);
                }
            }

            //Serializer.Save("D:\\temp\\config.xml", file);

            foreach (MAExecutor x in Program.maExecutors)
            {
                x.Start();
            }
        }
    }
}
