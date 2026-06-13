using System.Windows.Media;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    /// <summary>
    /// Shared, frozen brushes used to colour status and result icons. The colours are chosen
    /// to read clearly against both the light and dark application themes.
    /// </summary>
    internal static class StatusBrushes
    {
        public static readonly Brush Running = Frozen(Color.FromRgb(0x26, 0xA6, 0x41));

        public static readonly Brush Success = Frozen(Color.FromRgb(0x26, 0xA6, 0x41));

        public static readonly Brush Warning = Frozen(Color.FromRgb(0xCA, 0x8A, 0x04));

        public static readonly Brush Paused = Frozen(Color.FromRgb(0xCA, 0x8A, 0x04));

        public static readonly Brush Error = Frozen(Color.FromRgb(0xE0, 0x31, 0x31));

        public static readonly Brush Inactive = Frozen(Color.FromRgb(0x9A, 0xA0, 0xA6));

        public static readonly Brush Transitional = Frozen(Color.FromRgb(0x3B, 0x82, 0xF6));

        private static Brush Frozen(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
