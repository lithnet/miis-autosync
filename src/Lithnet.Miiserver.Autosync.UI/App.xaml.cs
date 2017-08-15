using System;
using System.Diagnostics;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
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

            //if (!SyncServer.IsAdmin())
            //{
            //    MessageBox.Show("You must be a member of the MIM Synchronization Administrators group to use the AutoSync editor",
            //        "Lithnet AutoSync",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Stop);
            //    Environment.Exit(5);
            //}


#if DEBUG
            if (Debugger.IsAttached)
            {
                ServiceController sc = new ServiceController("miisautosync");
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    // Must be started off the UI-thread
                    Task.Run(() =>
                    {
                        Program.LoadConfiguration();
                        Program.StartConfigServiceHost();
                        Program.CreateExecutionEngineInstance();

                    }).Wait();
                }
            }
#endif
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Trace.WriteLine(e.ExceptionObject);
        }

        internal static BitmapImage GetImageResource(string name)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Resources/{name}", UriKind.Absolute));
        }
    }
}