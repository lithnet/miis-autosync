
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Lithnet.Miiserver.AutoSync.UI.Converters
{
    public class SortedListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Collections.IList collection = value as System.Collections.IList;

            if (collection == null)
            {
                IEnumerable i = value as IEnumerable;

                if (i == null)
                {
                    return value;
                }

                ArrayList list = new ArrayList();
                foreach (object item in i)
                {
                    list.Add(item);
                }

                collection = list;
            }

            if (parameter == null)
            {
                return value;
            }

            ListCollectionView view = new ListCollectionView(collection);
            SortDescription sort = new SortDescription(parameter.ToString(), ListSortDirection.Ascending);
            view.SortDescriptions.Add(sort);

            return view;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
