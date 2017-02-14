using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;
using Lithnet.Logging;
using System.ComponentModel;

namespace Lithnet.Miiserver.AutoSync
{
    public class PowerShellExecutionTrigger : IMAExecutionTrigger
    {
        private bool run = true;

        private Task internalTask;

        private CancellationTokenSource cancellationToken;

        private PowerShell powershell;

        public string ScriptPath { get; set; }

        public string Name => $"PowerShell: {System.IO.Path.GetFileName(this.ScriptPath)}";

        public event ExecutionTriggerEventHandler TriggerExecution;

        public void Start()
        {
            this.cancellationToken = new CancellationTokenSource();

            this.internalTask = new Task(this.Run, this.cancellationToken.Token);

            this.internalTask.Start();
        }

        private void Run()
        {
            try
            {
                this.powershell = PowerShell.Create();
                this.powershell.AddScript(System.IO.File.ReadAllText(this.ScriptPath));
                this.powershell.Invoke();

                if (this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("Get-RunProfileToExecute", CommandTypes.All) == null)
                {
                    Logger.WriteLine("The file '{0}' did not contain a function called Get-RunProfileToExecute and will be ignored", this.ScriptPath);
                    return;
                }

                while (this.run)
                {
                    this.cancellationToken.Token.ThrowIfCancellationRequested();

                    this.powershell.Commands.Clear();
                    this.powershell.AddCommand("Get-RunProfileToExecute");

                    Collection<PSObject> results;

                    try
                    {
                        results = this.powershell.Invoke();
                        this.cancellationToken.Token.ThrowIfCancellationRequested();
                        this.powershell.ThrowOnPipelineError();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine("The PowerShell execution trigger encountered an error and has been terminated");
                        Logger.WriteException(ex);

                        if (MessageSender.CanSendMail())
                        {
                            MessageSender.SendMessage($"The PowerShell execution trigger '{this.Name}' encountered an error and has been terminated", ex.ToString());
                        }

                        break;
                    }

                    foreach (PSObject result in results)
                    {
                        string runProfileName = result.BaseObject as string;

                        if (runProfileName != null)
                        {
                            this.Fire(runProfileName);
                            continue;
                        }

                        ExecutionParameters p = result.BaseObject as ExecutionParameters;

                        if (p == null)
                        {
                            continue;
                        }

                        this.Fire(p);
                    }

                    this.cancellationToken.Token.ThrowIfCancellationRequested();
                    this.cancellationToken.Token.WaitHandle.WaitOne(Settings.PSExecutionQueryInterval);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.WriteLine("The PowerShell execution trigger encountered an error and has been terminated");
                Logger.WriteException(ex);

                if (MessageSender.CanSendMail())
                {
                    MessageSender.SendMessage($"The PowerShell execution trigger '{this.Name}' encountered an error and has been terminated", ex.ToString());
                }
            }
        }

        public void Stop()
        {
            this.run = false;

            this.cancellationToken?.Cancel();
            this.powershell?.Stop();

            if (this.internalTask != null && !this.internalTask.IsCompleted)
            {
                this.internalTask.Wait();
            }
        }

        public void Fire(string runProfileName)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(runProfileName));
        }

        public void Fire(ExecutionParameters p)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(p));
        }

        public override string ToString()
        {
            return $"{this.Name}";
        }
    }
}
