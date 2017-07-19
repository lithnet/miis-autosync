using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;
using Lithnet.Logging;
using System.Runtime.Serialization;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "powershell-trigger")]
    public class PowerShellExecutionTrigger : IMAExecutionTrigger
    {
        private bool run = true;

        private Task internalTask;

        private CancellationTokenSource cancellationToken;

        private PowerShell powershell;

        [DataMember(Name = "path")]
        public string ScriptPath { get; set; }

        [DataMember(Name = "exception-behaviour")]
        public ExecutionErrorBehaviour ExceptionBehaviour { get; set; }

        public string DisplayName => $"{this.Type}: {this.Description}";

        public string Type => "PowerShell script";

        public string Description => $"{System.IO.Path.GetFileName(this.ScriptPath)}";

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        public event ExecutionTriggerEventHandler TriggerExecution;

        public void Start()
        {
            this.cancellationToken = new CancellationTokenSource();

            if (this.Interval.TotalSeconds <= 0)
            {
                this.Interval = TimeSpan.FromSeconds(5);
            }

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
                        string message;

                        if (this.ExceptionBehaviour == ExecutionErrorBehaviour.Terminate)
                        {
                            message = $"The PowerShell execution trigger '{this.DisplayName}' encountered an error and has been terminated";
                        }
                        else
                        {
                            message = $"The PowerShell execution trigger '{this.DisplayName}' encountered an error";
                        }

                        Logger.WriteLine(message);
                        Logger.WriteException(ex);

                        if (MessageSender.CanSendMail())
                        {
                            MessageSender.SendMessage(message, ex.ToString());
                        }

                        if (this.ExceptionBehaviour == ExecutionErrorBehaviour.Terminate)
                        {
                            break;
                        }
                        else
                        {
                            continue;
                        }
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
                    this.cancellationToken.Token.WaitHandle.WaitOne(this.Interval);
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
                    MessageSender.SendMessage($"The PowerShell execution trigger '{this.DisplayName}' encountered an error and has been terminated", ex.ToString());
                }
            }
        }

        public void Stop()
        {
            try
            {
                this.run = false;

                this.cancellationToken?.Cancel();
                this.powershell?.Stop();

                if (this.internalTask != null && !this.internalTask.IsCompleted)
                {
                    this.internalTask.Wait();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred stopping the Powershell execution trigger");
                Logger.WriteException(ex);
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

        public static bool CanCreateForMA(ManagementAgent ma)
        {
            return true;
        }

        public  PowerShellExecutionTrigger (ManagementAgent ma)
        {
            this.ExceptionBehaviour = ExecutionErrorBehaviour.Terminate;
            this.Interval = new TimeSpan(0, 0, 5);
        }

        public override string ToString()
        {
            return $"{this.DisplayName}";
        }
    }
}
