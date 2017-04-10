using System;
using System.Timers;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class IntervalExecutionTrigger : IMAExecutionTrigger
    {
        private Timer checkTimer;

        public TimeSpan Interval { get; set; }

        public MARunProfileType RunProfileTargetType { get; set; }

        public string RunProfileName { get; set; }

        public IntervalExecutionTrigger(MARunProfileType type, TimeSpan interval)
        {
            this.Interval = interval;
            this.RunProfileTargetType = type;
        }

        public IntervalExecutionTrigger(string runProfileName, TimeSpan interval)
        {
            this.Interval = interval;
            this.RunProfileName = runProfileName;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            if (this.RunProfileName == null)
            {
                registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(this.RunProfileTargetType));
            }
            else
            {
                registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(this.RunProfileName));
            }
        }

        public void Start()
        {
            Logger.WriteLine("Starting interval timer for {0} at {1} seconds", LogLevel.Debug, this.RunProfileName ?? this.RunProfileTargetType.ToString(), this.Interval);

            this.checkTimer = new Timer
            {
                Interval = this.Interval.TotalMilliseconds,
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

        public string Name => $"{this.RunProfileName ?? this.RunProfileTargetType.ToString()} at {this.Interval} second intervals";

        public override string ToString()
        {
            return $"{this.Name}";
        }

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}
