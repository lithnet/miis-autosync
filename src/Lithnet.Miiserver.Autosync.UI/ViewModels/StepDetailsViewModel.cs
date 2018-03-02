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
    public class StepDetailsViewModel : ViewModelBase<StepDetails>
    {
        private Func<Guid, string, IEnumerable<CSObjectRef>> stepDetailsFunc;

        public StepDetailsViewModel(StepDetails stepDetails, Func<Guid, string, IEnumerable<CSObjectRef>> stepDetailsFunc)
            : base(stepDetails)
        {
            this.Commands.AddItem("ShowStepDetail", this.ShowStepDetail);
            this.stepDetailsFunc = stepDetailsFunc;
        }

        [DependsOn(nameof(StepResult))]
        public BitmapImage ResultIcon => App.GetIconForRunResult(this.StepResult);

        public string FormattedStartDate => this.Model.StartDate?.ToString("g");

        public string FormattedEndDate => this.Model.EndDate?.ToString("g");

        public string StepResult => this.Model.StepResult;

        public int StepNumber => this.Model.StepNumber;

        public string DisplayName => $"{this.Model.StepNumber} {this.StepTypeDescription}";

        public string Server => this.Model.MAConnection?.Server;

        public string ServerConnectionResult => this.Model.MAConnection?.ConnectionResult;

        public string StepTypeDescription => this.Model.StepDefinition?.StepTypeDescription;

        public string ServerConnectionDetails
        {
            get
            {
                if (this.Model.MAConnection == null)
                {
                    return null;
                }

                return $"{this.Model.MAConnection?.Server} - {this.Model.MAConnection?.ConnectionResult}";
            }
        }

        public bool ShowExportCounters => this.Model.StepDefinition.IsExportStep;

        public bool ShowImportCounters => this.Model.StepDefinition.IsImportStep;

        public bool ShowSyncCounters => this.Model.StepDefinition.IsSyncStep;
        
        public ExportCounters ExportCounters => this.Model.ExportCounters;

        public StagingCounters StagingCounters  => this.Model.StagingCounters;

        public MADiscoveryCounters DiscoveryCounters => this.Model.MADiscoveryCounters;

        public IReadOnlyList<MAObjectError> MADiscoveryErrors => this.Model.MADiscoveryErrors;

        private void ShowStepDetail(object t)
        {
            CounterDetail d = t as CounterDetail;
            
            var result = this.stepDetailsFunc(d.StepId, d.Type);

            MessageBox.Show(string.Join("\n", result.Select(u => u.DN)));
        }
    }
}
