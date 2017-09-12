using Lithnet.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lithnet.Miiserver.AutoSync.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal const string HelpBaseUrl = "https://github.com/lithnet/miis-autosync/wiki/";

        internal const string NullPlaceholder = "(none)";

        internal static char[] Separators = new char[] { ',', ';' };

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += this.CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException;
            Application.Current.DispatcherUnhandledException += this.Dispatcher_UnhandledException; 

#if DEBUG
            if (Debugger.IsAttached)
            {
                ServiceController sc = new ServiceController("autosync");

                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    // Must be started off the UI-thread
                    Task.Run(() =>
                    {
                        Program.SetupOutOfBandInstance();
                    }).Wait();
                }

                return;
            }
#endif

            try
            {
                ConfigClient c = App.GetDefaultConfigClient();
                Trace.WriteLine($"Attempting to connect to the AutoSync service at {c.Endpoint.Address}");
                c.Open();
                Trace.WriteLine($"Connected to the AutoSync service");
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show(
                    $"Could not contact the AutoSync service. Ensure the Lithnet AutoSync service is running",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
            catch (System.TimeoutException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show(
                    $"Could not contact the AutoSync service. Ensure the Lithnet AutoSync service is running",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
            catch (System.ServiceModel.Security.SecurityNegotiationException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"There was an error trying to establish a secure session with the AutoSync server\n\n{ex.Message}", "Security error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(5);
            }
            catch (System.ServiceModel.Security.SecurityAccessDeniedException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show("You do not have permission to manage the AutoSync service", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(5);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show(
                    $"An unexpected error occurred communicating with the AutoSync service. Restart the AutoSync service and try again",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private bool hasThrown;

        private object lockObject = new object();

        internal static string ToDelimitedString(IEnumerable<string> items)
        {
            if (items != null)
            {
                string result = string.Join(";", items);
                return result == string.Empty ? null : result;
            }
            else
            {
                return null;
            }
        }

        internal static HashSet<string> FromDelimitedString(string s)
        {
            if (s == null)
            {
                return null;
            }

            HashSet<string> items = new HashSet<string>();

            foreach (string i in s.Split(App.Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                items.Add(i.Trim());
            }

            return items;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            this.HandleException(e.Exception);
        }

        private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            this.HandleException(e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            this.HandleException((Exception)e.ExceptionObject);
        }

        private void HandleException(Exception e)
        {
            lock (this.lockObject)
            {
                if (this.hasThrown)
                {
                    return;
                }

                this.hasThrown = true;

                //try
                //{
                //    ServiceController sc = new ServiceController("autosync");
                //    if (sc.Status != ServiceControllerStatus.Running)
                //    {
                //        MessageBox.Show("The AutoSync service is not running. Please start the service and try again.",
                //            "Lithnet AutoSync",
                //            MessageBoxButton.OK,
                //            MessageBoxImage.Stop);
                //        Environment.Exit(1);
                //    }
                //}
                //catch (Exception)
                //{
                //}

                Logger.WriteLine("Unhandled exception in application");
                Logger.WriteLine(e.ToString());
                MessageBox.Show(
                    $"An unexpected error occurred and the editor will terminate\n\n {e.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }


        internal static EventClient GetDefaultEventClient(InstanceContext ctx)
        {
            if (string.Equals(UserSettings.AutoSyncServerHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return EventClient.GetNamedPipesClient(ctx);
            }
            else
            {
                return EventClient.GetNetTcpClient(ctx, UserSettings.AutoSyncServerHost, UserSettings.AutoSyncServerPort, UserSettings.AutoSyncServerIdentity);
            }
        }

        public static ConfigClient GetDefaultConfigClient()
        {
            if (string.Equals(UserSettings.AutoSyncServerHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigClient.GetNamedPipesClient();
            }
            else
            {
                return ConfigClient.GetNetTcpClient(UserSettings.AutoSyncServerHost, UserSettings.AutoSyncServerPort, UserSettings.AutoSyncServerIdentity);
            }
        }

        internal static BitmapImage GetImageResource(string name)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Resources/{name}", UriKind.Absolute));
        }
    }
}