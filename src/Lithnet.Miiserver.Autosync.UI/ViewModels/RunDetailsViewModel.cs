using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.Client;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class RunDetailsViewModel : ViewModelBase<RunDetails>
    {
        public RunDetailsViewModel(RunDetails runDetails, Func<Guid, string, IEnumerable<CSObjectRef>> stepDetailsFunc)
            : base(runDetails)
        {
            this.Steps = new StepDetailsCollectionViewModel(this.Model.StepDetails?.ToList() ?? new List<StepDetails>(), stepDetailsFunc);
        }

        public string WindowTitle => $"{this.Model.MAName} - {this.Model.RunProfileName} - {this.Model.RunNumber}";

        public string FormattedEndDate => this.Model.EndTime?.ToString("g");

        public string FormattedStartDate => this.Model.StartTime?.ToString("g");

        public int RunNumber => this.Model.RunNumber;

        public string RunProfileName => this.Model.RunProfileName;

        public string Result => this.Model.LastStepStatus;

        public string SecurityID => this.Model.SecurityID;

        public StepDetailsCollectionViewModel Steps { get; }

        [DependsOn(nameof(Result))]
        public BitmapImage ResultIcon => App.GetIconForRunResult(this.Result);
    }
}