namespace Lithnet.Miiserver.AutoSync
{
    public delegate void ExecutionTriggerEventHandler(object sender, ExecutionTriggerEventArgs e);

    public interface IMAExecutionTrigger
    {
        string DisplayName { get; }

        string Type { get; }

        string Description { get; }

        event ExecutionTriggerEventHandler TriggerExecution;

        void Start();

        void Stop();
    }
}
