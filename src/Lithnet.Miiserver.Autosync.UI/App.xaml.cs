using Lithnet.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using Lithnet.Miiserver.AutoSync.UI.Windows;

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

        internal static string ConnectedHost { get; set; }

        internal static int ConnectedPort { get; set; }

        internal static bool ConnectedToLocalHost { get; set; }

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
            }
#endif
            this.InitializeComponent();

            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            MainWindow window = new UI.MainWindow();
            Application.Current.MainWindow = window;
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();

            App.Connect(false, true);

            MainWindowViewModel m = new MainWindowViewModel();
            window.DataContext = m;
            m.Initialize();
        }

        internal static void Reconnect(MainWindowViewModel vm)
        {
            if (App.Connect(true, false))
            {
                vm.AbortExecutionMonitors();
                vm.Initialize();
            }
        }

        internal static bool TryDefaultConnection()
        {
            if (!UserSettings.AutoConnect)
            {
                return false;
            }

            return App.TryConnectionWithDialog(UserSettings.AutoSyncServerHost, UserSettings.AutoSyncServerPort);
        }

        internal static bool TryConnectionWithDialog(string host, int port)
        {
            ConnectingDialog connectingDialog = new ConnectingDialog();

            try
            {
                connectingDialog.CaptionText = $"Connecting to {host}";
                connectingDialog.DataContext = connectingDialog;

                connectingDialog.Show();
                connectingDialog.Activate();

                App.DoEvents();

                return App.TryConnect(host, port);
            }
            finally
            {
                connectingDialog.Hide();
            }
        }

        internal static bool TryConnect(string host, int port)
        {
            try
            {
                ConfigClient c = App.GetConfigClient(host, port);
                Trace.WriteLine($"Attempting to connect to the AutoSync service at {c.Endpoint.Address}");
                c.Open();
                Trace.WriteLine($"Connected to the AutoSync service");
                return true;
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show(
                    $"Could not contact the AutoSync service. Ensure the Lithnet AutoSync service is running",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (System.TimeoutException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show(
                    $"Could not contact the AutoSync service. Ensure the Lithnet AutoSync service is running",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (System.ServiceModel.Security.SecurityNegotiationException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"There was an error trying to establish a secure session with the AutoSync server\n\n{ex.Message}", "Security error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (System.ServiceModel.Security.SecurityAccessDeniedException ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show("You do not have permission to manage the AutoSync service", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show(
                    $"An unexpected error occurred communicating with the AutoSync service. Restart the AutoSync service and try again",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        internal static bool Connect(bool forceShowDialog, bool exitOnCancel)
        {
            if (!forceShowDialog)
            {
                if (App.TryDefaultConnection())
                {
                    App.ConnectedHost = UserSettings.AutoSyncServerHost;
                    App.ConnectedPort = UserSettings.AutoSyncServerPort;
                    App.ConnectedToLocalHost = App.IsLocalhost(UserSettings.AutoSyncServerHost);
                    return true;
                }
            }

            ConnectDialogViewModel vm = new ConnectDialogViewModel();
            vm.HostnameRaw = UserSettings.AutoSyncServerHost;
            vm.AutoConnect = UserSettings.AutoConnect;

            if (UserSettings.AutoSyncServerPort != UserSettings.DefaultTcpPort)
            {
                vm.HostnameRaw += $":{UserSettings.AutoSyncServerPort}";
            }

            ConnectDialog dialog = new ConnectDialog();
            dialog.DataContext = vm;

            if (!dialog.ShowDialog() ?? false)
            {
                if (exitOnCancel)
                {
                    Environment.Exit(0);
                }
                else
                {
                    return false;
                }
            }

            UserSettings.AutoSyncServerHost = vm.Hostname;
            UserSettings.AutoSyncServerPort = vm.Port;
            UserSettings.AutoConnect = vm.AutoConnect;

            App.ConnectedHost = vm.Hostname;
            App.ConnectedPort = vm.Port;
            App.ConnectedToLocalHost = App.IsLocalhost(vm.Hostname);

            return true;
        }

        private Window ShowDummyWindow()
        {
            Window t = new Window() { AllowsTransparency = true, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Background = Brushes.Transparent };
            t.Show();
            return t;
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

        internal static void DoEvents()
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Background);
        }

        internal static EventClient GetDefaultEventClient(InstanceContext ctx)
        {
            if (App.IsLocalhost(App.ConnectedHost))
            {
                return EventClient.GetNamedPipesClient(ctx);
            }
            else
            {
                return EventClient.GetNetTcpClient(ctx, App.ConnectedHost, App.ConnectedPort, UserSettings.AutoSyncServerIdentity);
            }
        }

        public static ConfigClient GetConfigClient(string host, int port)
        {
            if (App.IsLocalhost(host))
            {
                return ConfigClient.GetNamedPipesClient();
            }
            else
            {
                return ConfigClient.GetNetTcpClient(host, port, UserSettings.AutoSyncServerIdentity);
            }
        }

        public static ConfigClient GetDefaultConfigClient()
        {
            return App.GetConfigClient(App.ConnectedHost, App.ConnectedPort);
        }

        public static bool IsLocalhost(string hostname)
        {
            return string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        internal static BitmapImage GetImageResource(string name)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Resources/{name}", UriKind.Absolute));
        }
    }
}