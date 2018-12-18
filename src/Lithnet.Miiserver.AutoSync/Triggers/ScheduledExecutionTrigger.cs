using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Timers;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "scheduled-trigger")]
    [Description(TypeDescription)]
    public class ScheduledExecutionTrigger : MAExecutionTrigger
    {
        private const string TypeDescription = "Scheduled interval";

        private Timer checkTimer;

        [DataMember(Name = "start-date")]
        public DateTime StartDateTime { get; set; }

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        [DataMember(Name = "run-profile-name")]
        public string RunProfileName { get; set; }

        [DataMember(Name = "exclusive")]
        public bool Exclusive { get; set; }

        [DataMember(Name = "run-immediate")]
        public bool RunImmediate { get; set; }

        private bool HasFired { get; set; }

        private TimeSpan GetFirstInterval()
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

            if (triggerTime > now)
            {
                return triggerTime - now;
            }
            
            TimeSpan difference = now - triggerTime;

            int intervals = (int)(difference.Ticks / this.Interval.Ticks);

            triggerTime = triggerTime.AddTicks(intervals * this.Interval.Ticks);

            while (triggerTime < now)
            {
                triggerTime = triggerTime.Add(this.Interval);
            }

            this.Log($"Scheduling first event for {triggerTime} and will repeat every {this.Interval}");
            return triggerTime - now;
        }

        private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionParameters p = new ExecutionParameters
            {
                RunProfileName = this.RunProfileName,
                Exclusive = this.Exclusive,
                RunImmediate = this.RunImmediate
            };

            this.Fire(p);

            if (!this.HasFired)
            {
                this.RevertToStandardInterval();
            }
        }

        public override void Start(string managementAgentName)
        {
            if (this.Disabled)
            {
                this.Log("Trigger disabled");
                return;
            }

            this.ManagementAgentName = managementAgentName;

            if (this.RunProfileName == null)
            {
                this.LogError("Ignoring scheduled trigger with no run profile name");
                return;
            }

            this.HasFired = false;
            this.SetupTimer();
        }

        private void SetupTimer()
        {
            this.checkTimer = new Timer
            {
                Interval = this.GetFirstInterval().TotalMilliseconds,
                AutoReset = false
            };

            this.checkTimer.Elapsed += this.CheckTimer_Elapsed;
            this.checkTimer.Start();
        }

        private void RevertToStandardInterval()
        {
            this.checkTimer.Interval = this.Interval.TotalMilliseconds;
            this.checkTimer.AutoReset = true;
            this.checkTimer.Start();
            this.HasFired = true;
        }

        public override void Stop()
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

        public override string DisplayName => $"{this.Type}: {this.Description}";

        public override string Type => TypeDescription;

        public override string Description => $"{this.DisabledText}{this.RunProfileName} every {this.Interval} starting from {this.StartDateTime:F}";

        public override string ToString()
        {
            return $"{this.DisplayName}";
        }

        public static bool CanCreateForMA(ManagementAgent ma)
        {
            return true;
        }

        public ScheduledExecutionTrigger(ManagementAgent ma)
        {
            this.RunProfileName = ma.RunProfiles?.Select(u => u.Key).FirstOrDefault();
            this.Interval = new TimeSpan(24, 0, 0);
            this.StartDateTime = DateTime.Now;
            this.StartDateTime = this.StartDateTime.AddSeconds(-this.StartDateTime.Second);
        }
    }
}
