using System;
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
        public Wpf.Ui.Controls.SymbolRegular DisplaySymbol
        {
            get
            {
                if (this.Result == null)
                {
                    return Wpf.Ui.Controls.SymbolRegular.Empty;
                }

                if (this.Result == "success")
                {
                    return Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24;
                }

                if (this.Result.StartsWith("completed-", StringComparison.InvariantCultureIgnoreCase))
                {
                    return Wpf.Ui.Controls.SymbolRegular.Warning24;
                }

                return Wpf.Ui.Controls.SymbolRegular.ErrorCircle24;
            }
        }

        [DependsOn(nameof(Result))]
        public System.Windows.Media.Brush DisplayBrush
        {
            get
            {
                if (this.Result == null)
                {
                    return null;
                }

                if (this.Result == "success")
                {
                    return StatusBrushes.Success;
                }

                if (this.Result.StartsWith("completed-", StringComparison.InvariantCultureIgnoreCase))
                {
                    return StatusBrushes.Warning;
                }

                return StatusBrushes.Error;
            }
        }
    }
}
