using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    /// <summary>
    /// A context-menu item that carries a WPF-UI Fluent symbol instead of a bitmap icon.
    /// </summary>
    public class SymbolMenuItemViewModel : MenuItemViewModel
    {
        public Wpf.Ui.Controls.SymbolRegular Symbol { get; set; }
    }
}
