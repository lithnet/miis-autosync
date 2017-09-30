using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NLog;

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
        protected static Logger Logger = LogManager.GetCurrentClassLogger();

        public static IList<Type> SingleInstanceTriggers = new List<Type>() {typeof(ActiveDirectoryChangeTrigger), typeof(FimServicePendingImportTrigger)};
        
        public abstract string DisplayName { get; }

        public abstract string Type { get; }

        public abstract string Description { get; }

        public event ExecutionTriggerEventHandler TriggerFired;

        public event TriggerMessageEventHandler Message;

        public event TriggerMessageEventHandler Error;

        protected string ManagementAgentName { get; set; }

        public abstract void Start(string managementAgentName);

        public abstract void Stop();

        protected void Fire(string runProfileName)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(runProfileName));
        }

        protected void Fire(string runProfileName, bool exclusive)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(runProfileName, exclusive));
        }

        protected void Fire(MARunProfileType type, Guid partitionID)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;
            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(type, partitionID));
        }

        protected void Fire(MARunProfileType type, string partitionName)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;
            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(type, partitionName));
        }

        protected void Fire(ExecutionParameters p)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerFired;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(p));
        }

        protected void Trace(string message)
        {
            Logger.Trace(message);
        }

        protected void Log(string message)
        {
            Logger.Trace($"{message}");
            this.Message?.Invoke(this, new TriggerMessageEventArgs(message));
        }

        protected void Log(string message, string detail)
        {
            Logger.Trace($"{message}\n{detail}");
            this.Message?.Invoke(this, new TriggerMessageEventArgs(message, detail));
        }

        protected void LogError(string message)
        {
            Logger.Error(message);
            this.Error?.Invoke(this, new TriggerMessageEventArgs(message));
        }

        protected void LogError(string message, Exception ex)
        {
            Logger.Error(ex, message);
            this.Error?.Invoke(this, new TriggerMessageEventArgs(message, ex.ToString()));
        }
    }
}
