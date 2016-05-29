using System.Timers;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class IntervalExecutionTrigger : IMAExecutionTrigger
    {
        private Timer checkTimer;

        private int TriggerInterval { get; }

        public MARunProfileType RunProfileTargetType { get; set; }

        public IntervalExecutionTrigger(MARunProfileType type, int intervalSeconds)
        {
            this.TriggerInterval = intervalSeconds;
            this.RunProfileTargetType = type;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(this.RunProfileTargetType));
        }

        public void Start()
        {
            Logger.WriteLine("Starting interval timer for {0} at {1} seconds", LogLevel.Debug, this.RunProfileTargetType, this.TriggerInterval);

            this.checkTimer = new Timer
            {
                Interval = this.TriggerInterval * 1000,
                AutoReset = true
            };

            this.checkTimer.Elapsed += this.checkTimer_Elapsed;
            this.checkTimer.Start();
        }

        public void Stop()
        {
            if (this.checkTimer == null)
            {
                return;
            }

            if (this.checkTimer.Enabled)
            {
                this.checkTimer.Stop();
            }
        }

        public string Name => $"{this.TriggerInterval} second interval";

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}
