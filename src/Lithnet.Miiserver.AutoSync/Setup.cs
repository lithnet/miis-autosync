using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.Security.Authentication.Identity;
using Windows.Win32.System.Services;

namespace Lithnet.Miiserver.AutoSync
{
    /// <summary>
    /// Code-driven Windows service registration, invoked from the installer via the
    /// "setup install-service" / "setup uninstall-service" command-line verbs.
    ///
    /// The service lifecycle is handled here (rather than by the MSI's native
    /// ServiceInstall/ServiceControl tables) so that we control create/configure/delete
    /// ordering on upgrades (avoiding "service marked for deletion" / locked-service
    /// failures) and can grant the "log on as a service" right ourselves. This mirrors the
    /// approach used by Lithnet Access Manager, using CsWin32-generated P/Invoke.
    ///
    /// The service depends on the MIM Synchronization Service, is registered with the
    /// "/service" argument so it starts in service mode, and is created auto-start.
    /// </summary>
    public static class Setup
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const string ServiceName = "autosync";
        private const string ServiceDisplayName = "Lithnet AutoSync";
        private const string ServiceDescription = "Automatically operates the MIM synchronization service";
        private const string ServiceDependencies = "FIMSynchronizationService\0\0";
        private const string LogonAsServiceRight = "SeServiceLogonRight";

        private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;

        private const string EventLogSourceName = "AutoSync";
        private const string EventLogName = "Application";

        // Performance-counter names for the category created below. Keep in sync with the
        // counters consumed by MAControllerPerfCounters.
        private static readonly string[] PerformanceCounterNames =
        {
            "Queue length",
            "Runs/10 min",
            "Wait time % - sync lock",
            "Wait time % - exclusive lock",
            "Wait time %",
            "Wait time average - sync lock",
            "Wait time average - exclusive lock",
            "Wait time average",
            "Execution time average",
            "Execution time %",
            "Idle time %"
        };

        private static void SetupNLog()
        {
            var configuration = new NLog.Config.LoggingConfiguration();

            var serviceLog = new NLog.Targets.FileTarget("autosync-installer")
            {
                FileName = Path.Combine(Path.GetTempPath(), "autosync-installer.log"),
                ArchiveEvery = NLog.Targets.FileArchivePeriod.Day,
                MaxArchiveFiles = 7,
                Layout = "${longdate:universalTime=true}|${level:uppercase=true:padding=5}|${logger}|${message}${onexception:inner=${newline}${exception:format=ToString}}"
            };

            configuration.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, serviceLog);
            NLog.LogManager.Configuration = configuration;
        }

        public static unsafe void Install(string username, string password)
        {
            try
            {
                // The service must launch in service mode, so register the binary with /service.
                string binaryPath = "\"" + System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + "\" /service";

                logger.Info("Opening service control manager");
                using var serviceManager = PInvoke.OpenSCManager((string)null, null, PInvoke.SC_MANAGER_ALL_ACCESS);
                if (serviceManager.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                logger.Info($"Checking for existing {ServiceName} service");
                using var serviceHandle = PInvoke.OpenService(serviceManager, ServiceName, PInvoke.SERVICE_ALL_ACCESS);

                if (serviceHandle.IsInvalid)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != ERROR_SERVICE_DOES_NOT_EXIST)
                    {
                        throw new Win32Exception(err);
                    }

                    logger.Info($"Existing {ServiceName} service not found");
                    logger.Info($"Creating service {ServiceName} for user {username} at {binaryPath}");

                    // lpdwTagId must be NULL: a tag is only valid for a driver service with a
                    // boot/system start type that belongs to a load-ordering group. Requesting a
                    // tag for this auto-start SERVICE_WIN32_OWN_PROCESS (no load order group) is an
                    // invalid combination and CreateService fails with ERROR_INVALID_PARAMETER.
                    // We deliberately use the CsWin32 overload that omits lpdwTagId (it passes NULL),
                    // matching the native contract. Do NOT switch to the "out uint lpdwTagId" overload.
                    using var newService = PInvoke.CreateService(serviceManager, ServiceName, ServiceDisplayName, PInvoke.SERVICE_ALL_ACCESS, ENUM_SERVICE_TYPE.SERVICE_WIN32_OWN_PROCESS, SERVICE_START_TYPE.SERVICE_AUTO_START, SERVICE_ERROR.SERVICE_ERROR_NORMAL, binaryPath, null, ServiceDependencies, username, password);

                    if (newService.IsInvalid)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    logger.Info($"Created {ServiceName} service");
                    SetDescription(newService);
                }
                else
                {
                    logger.Info($"Found existing {ServiceName} service, updating its configuration");

                    // As with CreateService above, omit lpdwTagId (the overload passes NULL). Passing
                    // a non-NULL tag for this non-driver service yields ERROR_INVALID_PARAMETER.
                    if (!PInvoke.ChangeServiceConfig(serviceHandle, ENUM_SERVICE_TYPE.SERVICE_WIN32_OWN_PROCESS, SERVICE_START_TYPE.SERVICE_AUTO_START, SERVICE_ERROR.SERVICE_ERROR_NORMAL, binaryPath, null, ServiceDependencies, username, password, ServiceDisplayName))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    logger.Info($"Updated {ServiceName} service parameters");
                    SetDescription(serviceHandle);
                }

                TryGrantLogonAsAService(username);
                RegisterPerformanceCounters();
                RegisterEventLogSource();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unable to install service");
                throw;
            }
        }

        private static unsafe void SetDescription(SafeHandle serviceHandle)
        {
            fixed (char* description = ServiceDescription)
            {
                var info = new SERVICE_DESCRIPTIONW { lpDescription = new PWSTR(description) };
                logger.Info("Updating service description");
                if (!PInvoke.ChangeServiceConfig2W(serviceHandle, SERVICE_CONFIG.SERVICE_CONFIG_DESCRIPTION, &info))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private static unsafe void TryGrantLogonAsAService(string username)
        {
            try
            {
                logger.Info($"Granting the logon as a service right to {username}");

                var sid = (SecurityIdentifier)new NTAccount(username).Translate(typeof(SecurityIdentifier));
                byte[] sidBytes = new byte[sid.BinaryLength];
                sid.GetBinaryForm(sidBytes, 0);

                var objectAttributes = new LSA_OBJECT_ATTRIBUTES();

                NTSTATUS status = PInvoke.LsaOpenPolicy(null, objectAttributes, (uint)(PInvoke.POLICY_CREATE_ACCOUNT | PInvoke.POLICY_LOOKUP_NAMES), out LsaCloseSafeHandle policyHandle);
                if (status.Value != 0)
                {
                    throw new Win32Exception((int)PInvoke.LsaNtStatusToWinError(status));
                }

                using (policyHandle)
                {
                    fixed (char* rightName = LogonAsServiceRight)
                    fixed (byte* sidPtr = sidBytes)
                    {
                        var userRight = new LSA_UNICODE_STRING
                        {
                            Buffer = new PWSTR(rightName),
                            Length = (ushort)(LogonAsServiceRight.Length * sizeof(char)),
                            MaximumLength = (ushort)((LogonAsServiceRight.Length + 1) * sizeof(char))
                        };

                        status = PInvoke.LsaAddAccountRights(policyHandle, new PSID(sidPtr), new ReadOnlySpan<LSA_UNICODE_STRING>(&userRight, 1));
                        if (status.Value != 0)
                        {
                            throw new Win32Exception((int)PInvoke.LsaNtStatusToWinError(status));
                        }
                    }
                }

                logger.Info($"Granted the logon as a service right to {username}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The service account could not be granted the 'logon as a service' right");
            }
        }

        // Creates the "Lithnet AutoSync" performance counter category consumed by
        // MAControllerPerfCounters. Best-effort: counters are non-critical telemetry, so a
        // failure here is logged but never fails the installation.
        private static void RegisterPerformanceCounters()
        {
            try
            {
                if (PerformanceCounterCategory.Exists(MAControllerPerfCounters.CategoryName))
                {
                    logger.Info($"Removing existing performance counter category '{MAControllerPerfCounters.CategoryName}'");
                    PerformanceCounterCategory.Delete(MAControllerPerfCounters.CategoryName);
                }

                var counters = new CounterCreationDataCollection();
                foreach (string name in PerformanceCounterNames)
                {
                    counters.Add(new CounterCreationData(name, GetCounterHelp(name), PerformanceCounterType.NumberOfItems64));
                }

                logger.Info($"Registering performance counter category '{MAControllerPerfCounters.CategoryName}'");
                PerformanceCounterCategory.Create(MAControllerPerfCounters.CategoryName, "Lithnet AutoSync performance counters", PerformanceCounterCategoryType.MultiInstance, counters);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not register the '{MAControllerPerfCounters.CategoryName}' performance counter category");
            }
        }

        private static string GetCounterHelp(string name)
        {
            switch (name)
            {
                case "Queue length":
                    return "Shows the current number of jobs in the execution queue";

                case "Runs/10 min":
                    return "Shows the total number of jobs executed";

                default:
                    return name;
            }
        }

        private static void UnregisterPerformanceCounters()
        {
            try
            {
                if (PerformanceCounterCategory.Exists(MAControllerPerfCounters.CategoryName))
                {
                    logger.Info($"Removing performance counter category '{MAControllerPerfCounters.CategoryName}'");
                    PerformanceCounterCategory.Delete(MAControllerPerfCounters.CategoryName);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not remove the '{MAControllerPerfCounters.CategoryName}' performance counter category");
            }
        }

        // Creates the "AutoSync" Application event log source, pointing at the .NET framework's
        // generic message file so events render cleanly. Best-effort, like the counters.
        private static void RegisterEventLogSource()
        {
            try
            {
                if (EventLog.SourceExists(EventLogSourceName))
                {
                    logger.Info($"Event log source '{EventLogSourceName}' already exists");
                    return;
                }

                logger.Info($"Registering event log source '{EventLogSourceName}' in the '{EventLogName}' log");
                var data = new EventSourceCreationData(EventLogSourceName, EventLogName)
                {
                    MessageResourceFile = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "EventLogMessages.dll")
                };
                EventLog.CreateEventSource(data);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not register the '{EventLogSourceName}' event log source");
            }
        }

        /// <summary>
        /// Writes an error to the Windows event log as a last-resort diagnostics channel,
        /// used by the global unhandled-exception handler. The service-registration verbs run
        /// as a deferred LocalSystem MSI custom action, a context in which NLog's temp-file
        /// logging is unreliable (the write is silently dropped) and in which msiexec does not
        /// capture the process output — so without this channel an install failure produces no
        /// actionable diagnostics beyond a generic MSI "Error 1722". The event log is always
        /// reachable from the LocalSystem context. This method never throws and never logs
        /// sensitive data (callers must not pass the service password).
        /// </summary>
        internal static void WriteEventLogError(string message)
        {
            if (TryWriteEventLog(EventLogSourceName, message))
            {
                return;
            }

            // Our own "AutoSync" source may be unusable: not yet registered (a first install
            // that fails before RegisterEventLogSource runs), registered against a different
            // log, or only just created within this same process. Fall back to the
            // always-present "Application" source so the message is never lost.
            TryWriteEventLog(EventLogName, message);
        }

        private static bool TryWriteEventLog(string source, string message)
        {
            try
            {
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, EventLogName);
                }

                EventLog.WriteEntry(source, message, EventLogEntryType.Error);
                return true;
            }
            catch
            {
                // Best-effort only: never mask the original failure with a logging error.
                return false;
            }
        }

        private static void UnregisterEventLogSource()
        {
            try
            {
                if (EventLog.SourceExists(EventLogSourceName))
                {
                    logger.Info($"Removing event log source '{EventLogSourceName}'");
                    EventLog.DeleteEventSource(EventLogSourceName);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not remove the '{EventLogSourceName}' event log source");
            }
        }

        public static void Uninstall()
        {
            try
            {
                UnregisterPerformanceCounters();
                UnregisterEventLogSource();

                logger.Info("Opening service control manager");
                using var serviceManager = PInvoke.OpenSCManager((string)null, null, PInvoke.SC_MANAGER_ALL_ACCESS);
                if (serviceManager.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                logger.Info($"Checking for existing {ServiceName} service");
                using var serviceHandle = PInvoke.OpenService(serviceManager, ServiceName, PInvoke.SERVICE_ALL_ACCESS);
                if (serviceHandle.IsInvalid)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == ERROR_SERVICE_DOES_NOT_EXIST)
                    {
                        logger.Info($"Existing {ServiceName} service not found");
                        return;
                    }

                    throw new Win32Exception(err);
                }

                if (!PInvoke.DeleteService(serviceHandle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                logger.Info($"Deleted existing {ServiceName} service");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unable to uninstall service");
            }
        }

        /// <summary>
        /// Handles the "setup ..." command-line verbs. Returns true if a setup verb was
        /// recognised and handled (the caller should then exit).
        /// </summary>
        public static bool Process(string[] args)
        {
            if (args == null || args.Length < 2 || args[0] != "setup")
            {
                return false;
            }

            SetupNLog();

            switch (args[1])
            {
                case "install-service":
                    // setup install-service <username> [password]
                    if (args.Length < 3)
                    {
                        throw new ArgumentException("Invalid number of arguments for install-service");
                    }

                    string username = args[2];
                    string password = null;

                    if (args.Length >= 4 && !string.IsNullOrWhiteSpace(args[3]))
                    {
                        password = args[3];
                    }

                    Install(username, password);
                    return true;

                case "uninstall-service":
                    Uninstall();
                    return true;

                default:
                    return false;
            }
        }
    }
}
