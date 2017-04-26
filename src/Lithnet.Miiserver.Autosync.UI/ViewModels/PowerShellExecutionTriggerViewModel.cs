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
    public class PowerShellExecutionTriggerViewModel
    {
        private PowerShellExecutionTrigger model;

        public PowerShellExecutionTriggerViewModel(PowerShellExecutionTrigger model)
        {
            this.model = model;
            this.Commands = new CommandMap();
            this.Commands.Add("Browse", new DelegateCommand(t => this.Browse()));
        }

        public string ScriptPath
        {
            get => this.model.ScriptPath;
            set => this.model.ScriptPath = value;
        }

        public CommandMap Commands { get; private set; }

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
