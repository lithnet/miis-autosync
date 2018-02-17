using System.Threading;
using System.Windows;
using MahApps.Metro.Controls;

namespace Lithnet.Miiserver.AutoSync.UI.Windows
{
    /// <summary>
    /// Interaction logic for Connect.xaml
    /// </summary>
    public partial class ConnectingDialog : MetroWindow
    {
        public ConnectingDialog()
        {
            this.InitializeComponent();
            this.DataContext = this;
            this.CancellationTokenSource = new CancellationTokenSource();
        }

        public CancellationTokenSource CancellationTokenSource { get; private set; }

        public string CaptionText { get; set; }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.CancellationTokenSource.Cancel();
            this.ButtonCancel.IsEnabled = false;
            this.ButtonCancel.Content = "Canceling...";
        }
    }
}
