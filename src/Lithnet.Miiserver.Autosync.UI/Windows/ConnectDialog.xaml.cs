using System.Windows;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using MahApps.Metro.Controls;

namespace Lithnet.Miiserver.AutoSync.UI.Windows
{
    /// <summary>
    /// Interaction logic for Connect.xaml
    /// </summary>
    public partial class ConnectDialog : MetroWindow
    {
        public ConnectDialog()
        {
            this.InitializeComponent();
        }

        private async void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            App.UpdateFocusedBindings();
            ConnectDialogViewModel vm = (ConnectDialogViewModel)this.DataContext;

            try
            {
                if (await vm.ValidateConnection())
                {
                    this.DialogResult = true;
                }
                else
                {
                    this.Activate();
                }
            }
            catch
            {
                this.Activate();
                throw;
            }
        }
    }
}
