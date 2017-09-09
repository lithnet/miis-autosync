using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Lithnet.Miiserver.AutoSync.UI
{
    internal static class OnlineHelpProvider
    {
        public static readonly DependencyProperty HelpTopicProperty = DependencyProperty.RegisterAttached("HelpTopic", typeof(string), typeof(OnlineHelpProvider));

        static OnlineHelpProvider()
        {
            CommandManager.RegisterClassCommandBinding(typeof(FrameworkElement), new CommandBinding(ApplicationCommands.Help, new ExecutedRoutedEventHandler(Executed), new CanExecuteRoutedEventHandler(CanExecute)));
        }

        private static void CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;

            if (OnlineHelpProvider.GetHelpTopic(senderElement) != null)
            {
                e.CanExecute = true;
            }
        }

        private static void Executed(object sender, ExecutedRoutedEventArgs e)
        {
            string page = OnlineHelpProvider.GetHelpTopic(sender as FrameworkElement);

            if (page == null)
            {
                return;
            }

            string url = App.HelpBaseUrl + page;
            Process.Start(url);
        }

        public static string GetHelpTopic(DependencyObject obj)
        {
            return (string)obj.GetValue(OnlineHelpProvider.HelpTopicProperty);
        }

        public static void SetHelpTopic(DependencyObject obj, string value)
        {
            obj.SetValue(OnlineHelpProvider.HelpTopicProperty, value);
        }
    }
}
