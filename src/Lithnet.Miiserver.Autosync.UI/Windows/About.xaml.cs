using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;

namespace Lithnet.Miiserver.AutoSync.UI.Windows
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About 
    {
        public About()
        {
            this.InitializeComponent();
            this.TxVersion.Text = $"Build {Assembly.GetExecutingAssembly().GetName().Version}";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
