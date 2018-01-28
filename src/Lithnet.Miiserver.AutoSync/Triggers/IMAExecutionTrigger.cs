namespace Lithnet.Miiserver.AutoSync
{
    public delegate void ExecutionTriggerEventHandler(object sender, ExecutionTriggerEventArgs e);

    public delegate void TriggerMessageEventHandler(object sender, TriggerMessageEventArgs e);
    
    public interface IMAExecutionTrigger
    {
        bool Disabled { get; }

        string DisplayName { get; }

        string Type { get; }

        string Description { get; }

        event ExecutionTriggerEventHandler TriggerFired;

        event TriggerMessageEventHandler Message;

        event TriggerMessageEventHandler Error;
        
        void Start(string managementAgentName);

        void Stop();
    }
}
