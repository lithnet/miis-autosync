using System;
using System.Timers;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class ScheduledExecutionTrigger : IMAExecutionTrigger
    {
        private Timer checkTimer;

        public DateTime StartDateTime { get; set; }

        public TimeSpan Interval { get; set; }

        public string RunProfileName { get; set; }

        private double RemainingMilliseconds { get; set; }

        private void SetRemainingMilliseconds()
        {
            if (this.Interval.TotalSeconds < 1)
            {
                throw new ArgumentException("The interval cannot be zero");
            }

            if (this.StartDateTime == new DateTime(0))
            {
                this.StartDateTime = DateTime.Now;
            }

            DateTime triggerTime = this.StartDateTime;
            DateTime now = DateTime.Now;

            while(triggerTime < now)
            {
                triggerTime = triggerTime.Add(this.Interval);
            }

            Logger.WriteLine("Scheduling event for " + triggerTime, LogLevel.Debug);
            this.RemainingMilliseconds = (triggerTime - now).TotalMilliseconds;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(this.RunProfileName));

            this.Start();
        }

        public void Start()
        {
            this.SetRemainingMilliseconds();
            this.checkTimer = new Timer
            {
                Interval = this.RemainingMilliseconds,
                AutoReset = false
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

        public string Name => "Scheduled trigger";

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}
