using System;
using System.IO;
using System.Management.Automation;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public class MAController
    {
        private MAConfigParameters config;

        private PowerShell powershell;

        internal bool HasStoppedMA;

        public string ScriptPath { get; set; }

        internal bool SupportsShouldExecute { get; private set; }

        internal bool SupportsExecutionComplete { get; private set; }

        public MAController(MAConfigParameters config)
        {
            this.ScriptPath = config.MAControllerPath;
            this.config = config;

            if (string.IsNullOrEmpty(this.ScriptPath))
            {
                return;
            }

            if (!File.Exists(this.ScriptPath))
            {
                Logger.WriteLine("{0}: Warning: Controller script not found: {1}", config.ManagementAgentName, this.ScriptPath);
                return;
            }

            this.InitializePowerShellSession();
        }

        private void InitializePowerShellSession()
        {
            this.powershell = PowerShell.Create();
            this.powershell.AddScript(File.ReadAllText(this.ScriptPath));
            this.powershell.Invoke();

            if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("ShouldExecute", CommandTypes.All) != null)
            {
                Logger.WriteLine("{0}: Registering ShouldExecute handler", this.config.ManagementAgentName);
                this.SupportsShouldExecute = true;
            }

            if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("ExecutionComplete", CommandTypes.All) != null)
            {
                Logger.WriteLine("{0}: Registering ExecutionComplete handler", this.config.ManagementAgentName);
                this.SupportsExecutionComplete = true;
            }

            if (!(this.SupportsExecutionComplete || this.SupportsShouldExecute))
            {
                Logger.WriteLine("{0}: Controller script does not implement ShouldExecute or ExecutionComplete: {1}", this.config.ManagementAgentName, this.ScriptPath);
            }
        }

        public bool ShouldExecute(string runProfileName)
        {
            if (this.HasStoppedMA)
            {
                return false;
            }

            if (this.powershell == null || !this.SupportsShouldExecute)
            {
                return true;
            }

            this.powershell.ResetState();
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
                    this.HasStoppedMA = true;
                    throw ex.InnerException;
                }
                else
                {
                    Logger.WriteLine("{0}: ShouldExecute handler threw an exception", this.config.ManagementAgentName);
                    Logger.WriteException(ex);
                    return false;
                }
            }
            catch (UnexpectedChangeException)
            {
                this.HasStoppedMA = true;
                throw;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("{0}: ShouldExecute handler threw an exception", this.config.ManagementAgentName);
                Logger.WriteException(ex);
                return false;
            }

            return true;
        }

        public void ExecutionComplete(RunDetails d)
        {
            if (this.powershell == null || !this.SupportsExecutionComplete)
            {
                return;
            }

            this.powershell.ResetState();
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
                    this.HasStoppedMA = true;
                    throw ex.InnerException;
                }
                else
                {
                    Logger.WriteLine("{0}: ShouldExecute handler threw an exception", this.config.ManagementAgentName);
                    Logger.WriteException(ex);
                }
            }
            catch (UnexpectedChangeException)
            {
                this.HasStoppedMA = true;
                throw;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("{0}: ExecutionComplete handler threw an exception", this.config.ManagementAgentName);
                Logger.WriteException(ex);
            }
        }
    }
}