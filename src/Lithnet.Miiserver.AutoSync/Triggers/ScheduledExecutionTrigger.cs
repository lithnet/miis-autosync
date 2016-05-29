using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class ScheduledExecutionTrigger : IMAExecutionTrigger
    {
        public Timer checkTimer;

        public DateTime StartDateTime { get; set; }

        public TimeSpan Interval { get; set; }

        public string RunProfileName { get; set; }

        private double remainingMiliseconds { get; set; }

        public ScheduledExecutionTrigger()
        {
        }

        private void SetRemainingMiliseconds()
        {
            if (this.Interval.TotalSeconds == 0)
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
            this.remainingMiliseconds = (triggerTime - now).TotalMilliseconds;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            if (registeredHandlers != null)
            {
                registeredHandlers(this, new ExecutionTriggerEventArgs(this.RunProfileName));
            }

            this.Start();
        }

        public void Start()
        {
            this.SetRemainingMiliseconds();
            this.checkTimer = new Timer(this.remainingMiliseconds);

            this.checkTimer.AutoReset = false;
            this.checkTimer.Elapsed += this.checkTimer_Elapsed;
            this.checkTimer.Start();
        }

        public void Stop()
        {
            if (this.checkTimer != null)
            {
                if (this.checkTimer.Enabled)
                {
                    this.checkTimer.Stop();
                }
            }
        }

        public string Name
        {
            get
            {
                return "Scheduled trigger";
            }
        }

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}
