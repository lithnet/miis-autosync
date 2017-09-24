using System;
using System.IO;
using System.Management.Automation;
using NLog;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public class MAControllerScript
    {
        private MAControllerConfiguration config;

        private PowerShell powershell;

        private static Logger logger = LogManager.GetCurrentClassLogger();

        internal bool HasStoppedMA;

        public string ScriptPath { get; set; }

        internal bool SupportsShouldExecute { get; private set; }

        internal bool SupportsExecutionComplete { get; private set; }

        public MAControllerScript(MAControllerConfiguration config)
        {
            this.ScriptPath = config.MAControllerPath;
            this.config = config;

            if (string.IsNullOrEmpty(this.ScriptPath))
            {
                return;
            }

            if (!File.Exists(this.ScriptPath))
            {
                logger.Warn($"{config.ManagementAgentName}: Controller script not found: {this.ScriptPath}");
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
                logger.Info($"{this.config.ManagementAgentName}: Registering ShouldExecute handler");
                this.SupportsShouldExecute = true;
            }

            if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("ExecutionComplete", CommandTypes.All) != null)
            {
                logger.Info($"{this.config.ManagementAgentName}: Registering ExecutionComplete handler");
                this.SupportsExecutionComplete = true;
            }

            if (!(this.SupportsExecutionComplete || this.SupportsShouldExecute))
            {
                logger.Warn($"{this.config.ManagementAgentName}: Controller script does not implement ShouldExecute or ExecutionComplete: {this.ScriptPath}");
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
                    logger.Error(ex, $"{this.config.ManagementAgentName}: ShouldExecute handler threw an exception");
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
                logger.Error(ex, $"{this.config.ManagementAgentName}: ShouldExecute handler threw an exception");
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
                    logger.Error(ex, $"{this.config.ManagementAgentName}: ExecutionComplete handler threw an exception");
                }
            }
            catch (UnexpectedChangeException)
            {
                this.HasStoppedMA = true;
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"{this.config.ManagementAgentName}: ExecutionComplete handler threw an exception");
            }
        }
    }
}