using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace Lithnet.Miiserver.AutoSync.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += this.CurrentDomain_UnhandledException;

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // Must be started off the UI-thread
                Task.Run(() =>
                {
                    Program.StartConfigServiceHost();
                    Program.LoadConfiguration();
                }).Wait();
            }
#endif
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Trace.WriteLine(e.ExceptionObject);
        }
    }
}