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
        private RunProfileExecutionCompleteEventArgs e;

        public RunProfileResultViewModel(RunProfileExecutionCompleteEventArgs e)
        {
            this.e = e;
        }

        public string FormattedEndDate => this.EndDate?.ToString("g");

        public string FormattedStartDate => this.StartDate?.ToString("g");

        public DateTime? EndDate => this.e.EndDate;

        public DateTime? StartDate => this.e.StartDate;

        public int RunNumber => this.e.RunNumber;

        public string RunProfileName => this.e.RunProfileName;
        
        public string Result => this.e.Result;

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
