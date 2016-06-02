using System;
using System.IO;
using System.Management.Automation;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

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

            this.ScriptPath = Path.Combine(Settings.ConfigPath, Global.CleanMAName(ma.Name) + ".ps1");

            if (!File.Exists(this.ScriptPath))
            {
                return;
            }

            this.powershell = PowerShell.Create();
            this.powershell.AddScript(File.ReadAllText(this.ScriptPath));
            this.powershell.Invoke();

            if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("ShouldExecute", CommandTypes.All) != null)
            {
                Logger.WriteLine("{0}: Registering ShouldExecute handler", this.ma.Name);
                this.supportsShouldExecute = true;
            }

            if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("ExecutionComplete", CommandTypes.All) != null)
            {
                Logger.WriteLine("{0}: Registering ExecutionComplete handler", this.ma.Name);
                this.supportsExecutionComplete = true;
            }
        }

        public bool ShouldExecute(string runProfileName)
        {
            if (this.hasStoppedMA)
            {
                return false;
            }

            if (this.powershell == null || !this.supportsShouldExecute)
            {
                return true;
            }

            this.powershell.Commands.Clear();
            this.powershell.AddCommand("ShouldExecute");
            this.powershell.AddArgument(runProfileName);

            try
            {
                foreach (PSObject result in this.powershell.Invoke())
                {
                    this.powershell.ThrowOnPipelineError();

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
            if (this.powershell == null || !this.supportsExecutionComplete)
            {
                return;
            }

            this.powershell.Commands.Clear();
            this.powershell.AddCommand("ExecutionComplete");
            this.powershell.AddArgument(d);

            try
            {
                this.powershell.Invoke();
                this.powershell.ThrowOnPipelineError();
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