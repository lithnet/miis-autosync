using System.Windows;
using System.Windows.Controls;

namespace Lithnet.Miiserver.AutoSync.UI.Controls
{
    /// <summary>
    /// Selects the container template for a data-bound menu item by its data type. A
    /// &lt;ItemContainerTemplate DataType="..."&gt; declared in XAML is keyed by DataTemplateKey, but
    /// the default item-container selector only looks up an ItemContainerTemplateKey, so the
    /// template is never matched. This selector resolves the template by DataTemplateKey, which is
    /// how it is actually keyed.
    /// </summary>
    public class MenuItemContainerTemplateSelector : ItemContainerTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, ItemsControl parentItemsControl)
        {
            if (item == null)
            {
                return null;
            }

            return parentItemsControl.TryFindResource(new DataTemplateKey(item.GetType())) as DataTemplate;
        }
    }
}
