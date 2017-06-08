using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;
using Microsoft.Win32;

namespace Lithnet.Miiserver.Autosync.UI.ViewModels
{
    public class PowerShellExecutionTriggerViewModel : ViewModelBase<PowerShellExecutionTrigger>
    {
        public PowerShellExecutionTriggerViewModel(PowerShellExecutionTrigger model)
            :base(model)
        {
            this.Commands.Add("Browse", new DelegateCommand(t => this.Browse()));
        }

        public string ScriptPath
        {
            get => this.Model.ScriptPath;
            set => this.Model.ScriptPath = value;
        }

        private void Browse()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            if (openFileDialog.ShowDialog() == true)
            {
                this.ScriptPath = openFileDialog.FileName;
            }
        }
    }
}
