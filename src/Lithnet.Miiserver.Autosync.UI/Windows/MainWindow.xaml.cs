using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using MahApps.Metro.Controls;

namespace Lithnet.Miiserver.AutoSync.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            this.InitializeComponent();

            MainWindowViewModel m = new MainWindowViewModel();
            //m.ConfigFile = new ConfigFileViewModel(new ConfigFile());

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

            ConfigClient c = new ConfigClient();
            m.ConfigFile = new ConfigFileViewModel(c.GetConfig());
            // m.ConfigFile = new ConfigFileViewModel(ConfigFile.Load("D:\\temp\\config2.xml"));

            this.DataContext = m;
        }
    }
}
