using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using MahApps.Metro.Controls;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Lithnet.Miiserver.AutoSync.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool hasThrown;

        private object lockObject = new object();

        private static Logger logger;
        
        internal const string HelpBaseUrl = "https://github.com/lithnet/miis-autosync/wiki/";

        internal const string NullPlaceholder = "(none)";

        internal static char[] Separators = new char[] { ',', ';' };

        public App()
        {
            App.SetupLogger();
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
            MainWindow window = new MainWindow();
            Application.Current.MainWindow = window;
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindowViewModel m = new MainWindowViewModel();
            window.DataContext = m;
            window.Show();

            Task.Factory.StartNew(() =>
            {
                ConnectionManager.TryConnectWithDialog(false, true, window);
                m.Initialize();
            });
        }


        private static void SetupLogger()
        {
#if DEBUG
            if (LogManager.Configuration == null)
            {
                LogManager.Configuration = new LoggingConfiguration();
            }

            if (Debugger.IsAttached)
            {
                DebuggerTarget debug = new DebuggerTarget("debug-window") { Layout = "${longdate}|${level:uppercase=true:padding=5}| ${message}" };
                LogManager.Configuration.AddTarget(debug);
                LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, debug));
            }

            TraceTarget trace = new TraceTarget("trace-target") { Layout = "${longdate}|${level:uppercase=true:padding=5}| ${message}" };
            LogManager.Configuration.AddTarget(trace);
            LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, trace));

            if (LogManager.Configuration != null)
            {
                foreach (LoggingRule item in LogManager.Configuration.LoggingRules.Where(t => t.Targets.Any(u => u.Name == "autosync-ui-file")))
                {
                    item.EnableLoggingForLevels(LogLevel.Trace, LogLevel.Fatal);
                }

                LogManager.ReconfigExistingLoggers();
            }
#endif

            App.logger = LogManager.GetCurrentClassLogger();
        }

        internal static void Reconnect(MainWindowViewModel vm)
        {
            if (ConnectionManager.TryConnectWithDialog(true, false, Application.Current.MainWindow))
            {
                vm.AbortExecutionMonitors();
                vm.Initialize();
            }
        }

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

                logger.Error(e, "Unhandled exception in application");
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