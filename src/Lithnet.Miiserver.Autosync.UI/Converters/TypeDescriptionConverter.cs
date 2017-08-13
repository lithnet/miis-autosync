using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Lithnet.Miiserver.AutoSync.UI
{
    public class TypeDescriptionConverter : IValueConverter
    {
        private string GetTypeDescription(Type t)
        {
            return t.GetCustomAttributes(false).OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Type t = value as Type;

            if (t == null)
            {
                return null;
            }

            return this.GetTypeDescription(t) ?? t.Name;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Empty;
        }
    }
}
