using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Timers;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "scheduled-trigger")]
    [Description(TypeDescription)]
    public class ScheduledExecutionTrigger : IMAExecutionTrigger
    {
        private const string TypeDescription = "Scheduled trigger";

        private Timer checkTimer;

        [DataMember(Name = "start-date")]
        public DateTime StartDateTime { get; set; }

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        [DataMember(Name = "run-profile-name")]
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

            while (triggerTime < now)
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
            if (this.RunProfileName == null)
            {
                Logger.WriteLine("Ignoring scheduled trigger with no run profile name");
                return;
            }

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

        public string DisplayName => $"{this.Type}: {this.Description}";

        public string Type => TypeDescription;

        public string Description => $"{this.RunProfileName} every {this.Interval} start from {this.StartDateTime}";

        public event ExecutionTriggerEventHandler TriggerExecution;

        public override string ToString()
        {
            return $"{this.DisplayName}";
        }


        public static bool CanCreateForMA(ManagementAgent ma)
        {
            return true;
        }

        public  ScheduledExecutionTrigger (ManagementAgent ma)
        {
            this.RunProfileName = ma.RunProfiles?.Select(u => u.Key).FirstOrDefault();
            this.Interval = new TimeSpan(24, 0, 0);
            this.StartDateTime = DateTime.Now;
        }
    }
}
