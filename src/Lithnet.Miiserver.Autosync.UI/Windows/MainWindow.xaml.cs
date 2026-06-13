using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Lithnet.Miiserver.AutoSync.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Track later Windows light/dark theme changes once the window has a handle.
            this.Loaded += (s, e) => SystemThemeWatcher.Watch(this);

            MainWindowViewModel m = new MainWindowViewModel();

            this.DataContext = m;
            m.ResetConfigViewModel();

            if (m.ExecutionMonitor != null)
            {
                m.ExecutionMonitor.IsSelected = true;
            }
        }

        private void ToggleTheme_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplicationTheme current = ApplicationThemeManager.GetAppTheme();
            ApplicationTheme next = current == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationThemeManager.Apply(next);
        }
    }
}
