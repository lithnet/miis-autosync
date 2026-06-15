using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Lithnet.Miiserver.AutoSync.UI.Controls
{
    /// <summary>
    /// Copies the current WPF-UI theme into a ContextMenu's own resource scope. A ContextMenu is
    /// shown in a popup with its own visual tree that does not receive the application's theme
    /// resources, so without this its DynamicResource brushes resolve against the default light
    /// theme and the menu renders with light colours in dark mode. Set IsThemeAware="True" on the
    /// ContextMenu to enable it; the theme is refreshed for all menus when the application theme
    /// changes.
    /// </summary>
    public static class ContextMenuThemeBehavior
    {
        public static readonly DependencyProperty IsThemeAwareProperty = DependencyProperty.RegisterAttached(
            "IsThemeAware",
            typeof(bool),
            typeof(ContextMenuThemeBehavior),
            new PropertyMetadata(false, OnIsThemeAwareChanged));

        public static void SetIsThemeAware(DependencyObject element, bool value)
        {
            element.SetValue(IsThemeAwareProperty, value);
        }

        public static bool GetIsThemeAware(DependencyObject element)
        {
            return (bool)element.GetValue(IsThemeAwareProperty);
        }

        private static readonly HashSet<ContextMenu> themedMenus = new HashSet<ContextMenu>();

        private static bool isThemeChangeHooked;

        private static void OnIsThemeAwareChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ContextMenu menu && (bool)e.NewValue)
            {
                menu.Opened -= OnMenuOpened;
                menu.Opened += OnMenuOpened;
            }
        }

        private static void OnMenuOpened(object sender, RoutedEventArgs e)
        {
            if (!(sender is ContextMenu menu))
            {
                return;
            }

            if (themedMenus.Add(menu))
            {
                ApplicationThemeManager.Apply(menu);

                if (!isThemeChangeHooked)
                {
                    isThemeChangeHooked = true;
                    ApplicationThemeManager.Changed += OnApplicationThemeChanged;
                }
            }
        }

        private static void OnApplicationThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
        {
            foreach (ContextMenu menu in themedMenus)
            {
                ApplicationThemeManager.Apply(menu);
            }
        }
    }
}
