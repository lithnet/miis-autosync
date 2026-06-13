using System.Windows;
using Wpf.Ui.Controls;

namespace Lithnet.Miiserver.AutoSync.UI.Windows
{
    /// <summary>
    /// Interaction logic for AddTriggerWindow.xaml
    /// </summary>
    public partial class AddTriggerWindow : FluentWindow
    {
        public AddTriggerWindow()
        {
            this.InitializeComponent();
        }

        private void Button_Click_OK(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
