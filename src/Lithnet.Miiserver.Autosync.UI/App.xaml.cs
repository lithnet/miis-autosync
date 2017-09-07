using System;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal const string NullPlaceholder = "(none)";

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += this.CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException;
            Application.Current.DispatcherUnhandledException += this.Dispatcher_UnhandledException; // WPF app

            ServiceController sc = new ServiceController("autosync");

#if DEBUG
            if (Debugger.IsAttached)
            {
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

            sc = new ServiceController("fimsynchronizationservice");
            if (sc.Status != ServiceControllerStatus.Running)
            {
                MessageBox.Show("The MIM Synchronization service is not running. Please start the service and try again.",
                    "Lithnet AutoSync",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);
                Environment.Exit(1);
            }

            sc = new ServiceController("autosync");
            if (sc.Status != ServiceControllerStatus.Running)
            {
                MessageBox.Show("The AutoSync service is not running. Please start the service and try again.",
                    "Lithnet AutoSync",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);
                Environment.Exit(1);
            }

            try
            {
                ConfigClient c = new ConfigClient();
                c.Open();
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show(
                    $"Could not contact the AutoSync service. Ensure the Lithnet MIIS AutoSync service is running",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
            catch (System.ServiceModel.Security.SecurityAccessDeniedException)
            {
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

                try
                {
                    ServiceController sc = new ServiceController("autosync");
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        MessageBox.Show("The AutoSync service is not running. Please start the service and try again.",
                            "Lithnet AutoSync",
                            MessageBoxButton.OK,
                            MessageBoxImage.Stop);
                        Environment.Exit(1);
                    }
                }
                catch (Exception)
                {
                }

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

        internal static BitmapImage GetImageResource(string name)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Resources/{name}", UriKind.Absolute));
        }
    }
}