using Lithnet.Miiserver.Autosync.UI.ViewModels;
using MahApps.Metro.Controls;

namespace Lithnet.Miiserver.Autosync.UI
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

            m.ConfigFile = new  ConfigFileViewModel(AutoSync.ConfigFile.Load("D:\\temp\\config.xml"));

            this.DataContext = m;
        }
    }
}
