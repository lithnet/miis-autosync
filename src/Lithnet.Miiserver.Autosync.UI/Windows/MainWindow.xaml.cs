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

            this.DataContext = m;
            m.ResetConfigViewModel();
            m.ConfigFile.ManagementAgents.IsSelected = true;
        }
    }
}
