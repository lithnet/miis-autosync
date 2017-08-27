using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name ="ma-execution-trigger")]
    [KnownType(typeof(ActiveDirectoryChangeTrigger))]
    [KnownType(typeof(FimServicePendingImportTrigger))]
    [KnownType(typeof(IntervalExecutionTrigger))]
    [KnownType(typeof(PowerShellExecutionTrigger))]
    [KnownType(typeof(ScheduledExecutionTrigger))]
    public abstract class MAExecutionTrigger : IMAExecutionTrigger
    {
        public abstract string DisplayName { get; }

        public abstract string Type { get; }

        public abstract string Description { get; }

        public event ExecutionTriggerEventHandler TriggerFired;

        public event TriggerMessageEventHandler Message;

        public event TriggerMessageEventHandler Error;

        public abstract void Start();

        public abstract void Stop();

        protected void Fire(string runProfileName)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(runProfileName));
        }

        protected void Fire(MARunProfileType type)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;
            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(type));
        }

        protected void Fire(ExecutionParameters p)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(p));
        }

        protected void Log(string message)
        {
            Trace.WriteLine($"{message}");
            this.Message?.Invoke(this, new TriggerMessageEventArgs(message));
        }

        protected void Log(string message, string detail)
        {
            Trace.WriteLine($"{message}\n{detail}");
            this.Message?.Invoke(this, new TriggerMessageEventArgs(message, detail));
        }

        protected void LogError(string message)
        {
            Trace.WriteLine($"ERROR: {message}");
            this.Error?.Invoke(this, new TriggerMessageEventArgs(message));
        }

        protected void LogError(string message, Exception ex)
        {
            Trace.WriteLine($"ERROR: {message}");
            Trace.WriteLine(ex);

            this.Error?.Invoke(this, new TriggerMessageEventArgs(message, ex.ToString()));
        }
    }
}
