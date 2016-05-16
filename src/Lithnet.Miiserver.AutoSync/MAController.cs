using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Host;
using System.Threading.Tasks;
using System.Threading;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;
using System.IO;

namespace Lithnet.Miiserver.AutoSync
{
    public class MAController
    {
        private ManagementAgent ma;

        private PowerShell powershell;

        private bool hasStoppedMA;

        public string ScriptPath { get; set; }

        private bool supportsShouldExecute;

        private bool supportsExecutionComplete;

        public MAController(ManagementAgent ma)
        {
            this.ma = ma;

            this.ScriptPath = Path.Combine(Global.ScriptDirectory, Global.CleanMAName(ma.Name) + ".ps1");

            if (File.Exists(this.ScriptPath))
            {
                this.powershell = PowerShell.Create();
                this.powershell.AddScript(System.IO.File.ReadAllText(this.ScriptPath));
                this.powershell.Invoke();

                if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("ShouldExecute", CommandTypes.All) != null)
                {
                    Logger.WriteLine("{0}: Registering ShouldExecute handler", this.ma.Name);
                    supportsShouldExecute = true;
                }

                if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("ExecutionComplete", CommandTypes.All) != null)
                {
                    Logger.WriteLine("{0}: Registering ExecutionComplete handler", this.ma.Name);
                    supportsExecutionComplete = true;
                }
            }
        }

        public bool ShouldExecute(string runProfileName)
        {
            if (this.hasStoppedMA)
            {
                return false;
            }

            if (this.powershell == null || !supportsShouldExecute)
            {
                return true;
            }

            powershell.Commands.Clear();
            powershell.AddCommand("ShouldExecute");
            powershell.AddArgument(runProfileName);

            try
            {
                foreach (PSObject result in powershell.Invoke())
                {
                    bool? res = result.BaseObject as bool?;

                    if (res != null)
                    {
                        return res.Value;
                    }
                }
            }
            catch (RuntimeException ex)
            {
                if (ex.InnerException is UnexpectedChangeException)
                {
                    this.hasStoppedMA = true;
                    throw ex.InnerException;
                }
                else
                {
                    Logger.WriteLine("{0}: ShouldExecute handler threw an exception", this.ma.Name);
                    Logger.WriteException(ex);
                    return false;
                }
            }
            catch (UnexpectedChangeException)
            {
                this.hasStoppedMA = true;
                throw;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("{0}: ShouldExecute handler threw an exception", this.ma.Name);
                Logger.WriteException(ex);
                return false;
            }

            return true;
        }

        public void ExecutionComplete(RunDetails d)
        {
            if (this.powershell == null || !supportsExecutionComplete)
            {
                return;
            }

            powershell.Commands.Clear();
            powershell.AddCommand("ExecutionComplete");
            powershell.AddArgument(d);

            try
            {
                powershell.Invoke();
            }
            catch (RuntimeException ex)
            {
                if (ex.InnerException is UnexpectedChangeException)
                {
                    this.hasStoppedMA = true;
                    throw ex.InnerException;
                }
                else
                {
                    Logger.WriteLine("{0}: ShouldExecute handler threw an exception", this.ma.Name);
                    Logger.WriteException(ex);
                }
            }
            catch (UnexpectedChangeException)
            {
                this.hasStoppedMA = true;
                throw;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("{0}: ExecutionComplete handler threw an exception", this.ma.Name);
                Logger.WriteException(ex);
            }
        }
    }
}