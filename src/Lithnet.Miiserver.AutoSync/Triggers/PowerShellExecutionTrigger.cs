using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.Serialization;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "powershell-trigger")]
    [Description(TypeDescription)]
    public class PowerShellExecutionTrigger : MAExecutionTrigger
    {
        private const string TypeDescription = "PowerShell script";

        private Task internalTask;

        private CancellationTokenSource cancellationToken;

        private PowerShell powershell;

        [DataMember(Name = "path")]
        public string ScriptPath { get; set; }

        [DataMember(Name = "exception-behaviour")]
        public ExecutionErrorBehaviour ExceptionBehaviour { get; set; }

        public bool HasCredentials => !string.IsNullOrEmpty(this.Username);

        public PSCredential GetCredentialPackage()
        {
            if (!this.HasCredentials)
            {
                return null;
            }

            return new PSCredential(this.Username, this.Password?.Value);
        }

        [DataMember(Name = "username")]
        public string Username { get; set; }

        [DataMember(Name = "password")]
        public ProtectedString Password { get; set; }

        public override string DisplayName => $"{this.Type}: {this.Description}";

        public override string Type => TypeDescription;

        public override string Description => $"{this.DisabledText}{System.IO.Path.GetFileName(this.ScriptPath)}";
        
        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        public override void Start(string managementAgentName)
        {
            if (this.Disabled)
            {
                this.Log("Trigger disabled");
                return;
            }

            this.ManagementAgentName = managementAgentName;

            if (!System.IO.File.Exists(this.ScriptPath))
            {
                this.LogError($"Could not start PowerShell trigger for MA {managementAgentName} as the script '{this.ScriptPath}' could not be found");
                return;
            }

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
                Thread.CurrentThread.SetThreadName($"{this.DisplayName} on {this.ManagementAgentName}");

                this.powershell = PowerShell.Create();
                this.powershell.AddScript(System.IO.File.ReadAllText(this.ScriptPath));
                this.powershell.Invoke();

                CommandInfo c = this.powershell.Runspace.SessionStateProxy.InvokeCommand.GetCommand("Get-RunProfileToExecute", CommandTypes.All);

                if (c == null)
                {
                    this.LogError($"The file '{this.ScriptPath}' did not contain a function called Get-RunProfileToExecute and will be ignored");
                    return;
                }

                bool cmdletRequiresCredentials = c.Parameters.ContainsKey("credentials");

                PSCredential creds = this.GetCredentialPackage();

                if (creds != null && !cmdletRequiresCredentials)
                {
                    this.LogError("Credentials were provided for the PowerShell script, but the Get-RunProfileToExecute function did not contain a 'credentials' parameter. See the wiki topic (https://github.com/lithnet/miis-autosync/wiki/Powershell-script-trigger) for more information");
                }
                
                while (!this.cancellationToken.Token.IsCancellationRequested)
                {
                    this.cancellationToken.Token.ThrowIfCancellationRequested();

                    this.powershell.ResetState();

                    if (cmdletRequiresCredentials)
                    {
                        this.powershell.AddCommand("Get-RunProfileToExecute").AddParameter("credentials", creds);
                    }
                    else
                    {
                        this.powershell.AddCommand("Get-RunProfileToExecute");
                    }

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
                        bool shouldTerminate = this.ExceptionBehaviour == ExecutionErrorBehaviour.Terminate;

                        this.LogError($"The PowerShell execution trigger '{this.DisplayName}' encountered an error", ex);

                        if (MessageSender.CanSendMail())
                        {
                            string messageContent = MessageBuilder.GetMessageBody(this.ManagementAgentName, this.Type, this.Description, DateTime.Now, shouldTerminate, ex);
                            MessageSender.SendMessage($"{this.ManagementAgentName}: {this.Type} trigger error", messageContent);
                        }

                        if (shouldTerminate)
                        {
                            this.Log("The PowerShell trigger has been terminated as specified by config");
                            break;
                        }
                        else
                        {
                            if (!this.cancellationToken.IsCancellationRequested)
                            {
                                this.cancellationToken.Token.WaitHandle.WaitOne(this.Interval);
                            }

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
                this.LogError("The PowerShell execution trigger encountered an error and has been terminated", ex);

                if (MessageSender.CanSendMail())
                {
                    string messageContent = MessageBuilder.GetMessageBody(this.ManagementAgentName, this.Type, this.Description, DateTime.Now, true, ex);
                    MessageSender.SendMessage($"{this.ManagementAgentName}: {this.Type} trigger error", messageContent);
                }
            }
        }

        public override void Stop()
        {
            try
            {
                this.Trace($"{this.DisplayName}: Stopping");

                this.cancellationToken?.Cancel();
                this.powershell?.Stop();

                if (this.internalTask != null && !this.internalTask.IsCompleted)
                {
                    if (!this.internalTask.Wait(TimeSpan.FromSeconds(10)))
                    {
                        this.Log($"Internal task did not stop");
                    }
                }

                this.Trace($"{this.DisplayName}: Stopped");
            }
            catch (AggregateException e)
            {
                if (!(e.InnerException is TaskCanceledException))
                {
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch (Exception ex)
            {
                this.LogError("An error occurred stopping the Powershell execution trigger", ex);
            }
        }

        public static bool CanCreateForMA(ManagementAgent ma)
        {
            return true;
        }

        public PowerShellExecutionTrigger(ManagementAgent ma)
        {
            this.ExceptionBehaviour = ExecutionErrorBehaviour.Terminate;
            this.Interval = new TimeSpan(0, 0, 30);
        }

        public override string ToString()
        {
            return $"{this.DisplayName}";
        }
    }
}
