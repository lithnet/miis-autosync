using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class RunProfileResultViewModel : ViewModelBase
    {
        public string RunProfileName { get; set; }

        public string Result { get; set; }

        [DependsOn(nameof(Result))]
        public new BitmapImage DisplayIcon
        {
            get
            {
                if (this.Result == null)
                {
                    return null;
                }

                if (this.Result == "success")
                {
                    return App.GetImageResource("circle-green.ico");
                }

                if (this.Result.StartsWith("completed-", StringComparison.InvariantCultureIgnoreCase))
                {
                    return App.GetImageResource("circle-yellow.ico");
                }

                return App.GetImageResource("circle-red.ico");
            }
        }
    }
}
