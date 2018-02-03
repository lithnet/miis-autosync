using Lithnet.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using MahApps.Metro.Controls;
using Misuzilla.Security;

namespace Lithnet.Miiserver.AutoSync.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static NetworkCredential connectedCredential;

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

            App.Connect(false, true, window);

            MainWindowViewModel m = new MainWindowViewModel();
            window.DataContext = m;
            m.Initialize();
        }

        internal static void Reconnect(MainWindowViewModel vm)
        {
            if (App.Connect(true, false, Application.Current.MainWindow))
            {
                vm.AbortExecutionMonitors();
                vm.Initialize();
            }
        }

        internal static bool TryDefaultConnection(Window owner)
        {
            if (!UserSettings.AutoConnect)
            {
                return false;
            }

            return App.TryConnectionWithProgressDialog(UserSettings.AutoSyncServerHost, UserSettings.AutoSyncServerPort, null, owner);
        }

        internal static bool TryConnectionWithProgressDialog(string host, int port, NetworkCredential credential, Window owner)
        {
            ConnectingDialog connectingDialog = new ConnectingDialog();

            try
            {
                connectingDialog.CaptionText = $"Connecting to {host}";

                connectingDialog.Show();
                connectingDialog.Owner = owner;
                owner.IsEnabled = false;
                connectingDialog.Activate();

                App.DoEvents();

                WindowInteropHelper windowHelper = new WindowInteropHelper(connectingDialog);

                return App.TryConnect(host, port, credential, windowHelper.Handle);
            }
            finally
            {
                connectingDialog.Hide();
                owner.IsEnabled = true;
            }
        }

        internal static bool TryConnect(string host, int port, NetworkCredential credential, IntPtr hwnd)
        {
            bool attempted = false;

            try
            {
                while (true)
                {
                    int errorCode;
                    bool hasSaved = false;

                    if (credential == null)
                    {
                        credential = App.GetSavedCredentialsOrNull(host);

                        if (credential != null)
                        {
                            hasSaved = true;
                        }
                    }

                    try
                    {
                        ConfigClient c = App.GetConfigClient(host, port, credential);
                        Trace.WriteLine($"Attempting to connect to the AutoSync service at {c.Endpoint.Address}");
                        c.Open();
                        Trace.WriteLine($"Connected to the AutoSync service");
                        App.connectedCredential = credential;
                        return true;
                    }
                    catch (System.ServiceModel.Security.SecurityAccessDeniedException)
                    {
                        errorCode = 5;
                    }
                    catch (System.ServiceModel.Security.SecurityNegotiationException)
                    {
                        errorCode = 1326;
                    }

                    CredentialUI.PromptForWindowsCredentialsOptions options = new CredentialUI.PromptForWindowsCredentialsOptions("Lithnet AutoSync", $"Enter credentials for {host}");
                    if (attempted)
                    {
                        options.AuthErrorCode = errorCode;
                    }
                    else
                    {
                        attempted = true;
                    }

                    options.IsSaveChecked = hasSaved;
                    options.HwndParent = hwnd;
                    options.Flags = CredentialUI.PromptForWindowsCredentialsFlag.CREDUIWIN_CHECKBOX | CredentialUI.PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC;

                    PromptCredentialsResult result;

                    if (hasSaved)
                    {
                        result = CredentialUI.PromptForWindowsCredentials(options, credential.UserName, credential.Password);
                    }
                    else
                    {
                        result = CredentialUI.PromptForWindowsCredentials(options, $"{Environment.UserDomainName}\\{Environment.UserName}", null);

                    }

                    if (result == null)
                    {
                        // User canceled the operation
                        return false;
                    }

                    credential = new NetworkCredential(result.UserName, result.Password);

                    if (result.IsSaveChecked)
                    {
                        CredentialManager.Write(App.GetCredentialTargetName(host), CredentialType.Generic, CredentialPersistence.LocalMachine, result.UserName, result.Password);
                    }
                    else
                    {
                        CredentialManager.TryDelete(App.GetCredentialTargetName(host), CredentialType.Generic);
                    }
                }
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

        private static string GetCredentialTargetName(string host)
        {
            return $"autosync/{host.ToLowerInvariant()}";
        }

        private static NetworkCredential GetSavedCredentialsOrNull(string host)
        {
            NetworkCredential credential = null;

            try
            {
                var storedCreds = CredentialManager.Read(App.GetCredentialTargetName(host), CredentialType.Generic);

                credential = new NetworkCredential();
                credential.UserName = storedCreds.UserName;
                credential.Password = storedCreds.Password;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1168)
                {
                    Trace.Write(ex.ToString());
                }
            }

            return credential;
        }

        internal static bool Connect(bool forceShowDialog, bool exitOnCancel, Window owner)
        {
            if (!forceShowDialog)
            {
                if (App.TryDefaultConnection(owner))
                {
                    App.ConnectedHost = UserSettings.AutoSyncServerHost;
                    App.ConnectedPort = UserSettings.AutoSyncServerPort;
                    App.ConnectedToLocalHost = App.IsLocalhost(UserSettings.AutoSyncServerHost);
                    return true;
                }
            }

            ConnectDialog dialog = new ConnectDialog();
            dialog.Owner = owner;
            ConnectDialogViewModel vm = new ConnectDialogViewModel(dialog);
            vm.HostnameRaw = UserSettings.AutoSyncServerHost;
            vm.AutoConnect = UserSettings.AutoConnect;

            if (UserSettings.AutoSyncServerPort != UserSettings.DefaultTcpPort)
            {
                vm.HostnameRaw += $":{UserSettings.AutoSyncServerPort}";
            }

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
            EventClient c;

            if (App.IsLocalhost(App.ConnectedHost))
            {
                c = EventClient.GetNamedPipesClient(ctx);
            }
            else
            {
                c = EventClient.GetNetTcpClient(ctx, App.ConnectedHost, App.ConnectedPort, UserSettings.AutoSyncServerIdentity);
                c.ClientCredentials.Windows.ClientCredential = App.connectedCredential;
            }

            return c;
        }

        internal static ConfigClient GetConfigClient(string host, int port, NetworkCredential credential)
        {
            ConfigClient c;

            if (App.IsLocalhost(host))
            {
                c = ConfigClient.GetNamedPipesClient();
            }
            else
            {
                c = ConfigClient.GetNetTcpClient(host, port, UserSettings.AutoSyncServerIdentity);
                c.ClientCredentials.Windows.ClientCredential = credential;
            }

            return c;
        }

        internal static ConfigClient GetDefaultConfigClient()
        {
            return App.GetConfigClient(App.ConnectedHost, App.ConnectedPort, App.connectedCredential);
        }

        internal static bool IsLocalhost(string hostname)
        {
            return string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        internal static BitmapImage GetImageResource(string name)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Resources/{name}", UriKind.Absolute));
        }

        internal static void UpdateFocusedBindings()
        {
            object focusedItem = Keyboard.FocusedElement;

            if (focusedItem == null)
            {
                return;
            }

            BindingExpression expression = (focusedItem as TextBox)?.GetBindingExpression(TextBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as ComboBox)?.GetBindingExpression(ComboBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as PasswordBox)?.GetBindingExpression(PasswordBoxBindingHelper.PasswordProperty);
            expression?.UpdateSource();

            expression = (focusedItem as TimeSpanControl)?.GetBindingExpression(TimeSpanControl.ValueProperty);
            expression?.UpdateSource();

            expression = (focusedItem as DateTimePicker)?.GetBindingExpression(DateTimePicker.SelectedDateProperty);
            expression?.UpdateSource();
        }
    }
}