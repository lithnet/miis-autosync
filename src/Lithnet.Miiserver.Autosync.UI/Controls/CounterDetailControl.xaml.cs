using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Lithnet.Miiserver.AutoSync.UI.Controls
{
    public partial class CounterDetailControl : UserControl
    {
        public CounterDetailControl()
        {
            this.InitializeComponent();
            this.DetailBlock.Visibility = Visibility.Collapsed;
            this.NoDetailBlock.Visibility = Visibility.Visible;
            this.DetailBlock.TextAlignment = TextAlignment.Right;
        }

        public ICommand Command
        {
            get => (ICommand)this.GetValue(CounterDetailControl.CommandProperty);
            set => this.SetValue(CounterDetailControl.CommandProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(CounterDetailControl),
                new FrameworkPropertyMetadata(null));

        public object CommandParameter
        {
            get => this.GetValue(CounterDetailControl.CommandParameterProperty);
            set => this.SetValue(CounterDetailControl.CommandParameterProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(CounterDetailControl),
                new FrameworkPropertyMetadata(null));
        
        public int Value
        {
            get => (int)this.GetValue(CounterDetailControl.ValueProperty);
            set => this.SetValue(CounterDetailControl.ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(CounterDetailControl),
                new FrameworkPropertyMetadata(0));


        public bool ShowDetail
        {
            get => (bool)this.GetValue(CounterDetailControl.ShowDetailProperty);
            set => this.SetValue(CounterDetailControl.ShowDetailProperty, value);
        }

        public static readonly DependencyProperty ShowDetailProperty =
            DependencyProperty.Register("ShowDetail", typeof(bool), typeof(CounterDetailControl),
                new FrameworkPropertyMetadata(false, new PropertyChangedCallback(CounterDetailControl.OnShowDetailChanged)));

        private static void OnShowDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is CounterDetailControl control))
            {
                return;
            }

            if (control.ShowDetail)
            {
                control.NoDetailBlock.Visibility = Visibility.Collapsed;
                control.DetailBlock.Visibility = Visibility.Visible;
            }
            else
            {
                control.NoDetailBlock.Visibility = Visibility.Collapsed;
                control.DetailBlock.Visibility = Visibility.Hidden;
            }
        }
    }
}
