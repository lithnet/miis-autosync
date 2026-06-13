using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;

namespace Lithnet.Miiserver.AutoSync.UI.Controls
{
    /// <summary>
    /// Builds a ContextMenu (and its nested submenus) from a collection of menu-item view
    /// models as real MenuItem/Separator objects. Data-bound ItemContainerTemplate generation
    /// does not produce nested submenu items reliably, whereas concrete MenuItem objects do, so
    /// the menu is materialised directly here. Set ItemsSource on the ContextMenu; it rebuilds
    /// whenever the menu's data context changes (i.e. each time it is shown for a row).
    /// </summary>
    public static class ContextMenuBuilder
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.RegisterAttached(
            "ItemsSource",
            typeof(IEnumerable),
            typeof(ContextMenuBuilder),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public static void SetItemsSource(DependencyObject element, IEnumerable value)
        {
            element.SetValue(ItemsSourceProperty, value);
        }

        public static IEnumerable GetItemsSource(DependencyObject element)
        {
            return (IEnumerable)element.GetValue(ItemsSourceProperty);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ItemsControl menu)
            {
                // WPF-UI's MenuItem template draws text with the item's inherited Foreground and
                // does not supply a theme-aware default, so menu text stays black in dark mode.
                // Bind the menu's Foreground to the primary text brush so items inherit a
                // theme-aware colour (the template's disabled trigger still overrides it).
                menu.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
                BuildItems(menu, e.NewValue as IEnumerable);
            }
        }

        private static void BuildItems(ItemsControl parent, IEnumerable source)
        {
            parent.Items.Clear();

            if (source == null)
            {
                return;
            }

            foreach (object item in source)
            {
                if (item is SeparatorViewModel)
                {
                    parent.Items.Add(new Separator());
                }
                else if (item is SymbolMenuItemViewModel model)
                {
                    MenuItem menuItem = new MenuItem
                    {
                        Header = model.Header,
                        Command = model.Command,
                    };

                    if (model.Symbol != Wpf.Ui.Controls.SymbolRegular.Empty)
                    {
                        menuItem.Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = model.Symbol, FontSize = 16 };
                    }

                    if (model.MenuItems != null && model.MenuItems.Count > 0)
                    {
                        BuildItems(menuItem, model.MenuItems);
                    }

                    parent.Items.Add(menuItem);
                }
            }
        }
    }
}
