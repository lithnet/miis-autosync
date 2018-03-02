using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class RunProfileResultViewModel : ViewModelBase
    {
        private RunProfileExecutionCompleteEventArgs e;

        private Action<int> openRunDelegate;

        public RunProfileResultViewModel(Action<int> openRunDelegate, RunProfileExecutionCompleteEventArgs e)
        {
            this.Commands.AddItem("OpenRun", this.OpenRun);
            this.e = e;
            this.openRunDelegate = openRunDelegate;
        }

        public string FormattedEndDate => this.EndDate?.ToString("g");

        public string FormattedStartDate => this.StartDate?.ToString("g");

        public DateTime? EndDate => this.e.EndDate;

        public DateTime? StartDate => this.e.StartDate;

        public int RunNumber => this.e.RunNumber;

        public string RunProfileName => this.e.RunProfileName;
        
        public string Result => this.e.Result;

        [DependsOn(nameof(Result))]
        public BitmapImage ResultIcon => App.GetIconForRunResult(this.Result);
       
        private void OpenRun(object parameter)
        {
            this.openRunDelegate(this.RunNumber);
        }
    }
}
