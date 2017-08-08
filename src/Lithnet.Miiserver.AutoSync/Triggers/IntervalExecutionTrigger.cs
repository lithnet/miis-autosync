using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Timers;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "interval-trigger")]
    [Description(TypeDescription)]
    public class IntervalExecutionTrigger : IMAExecutionTrigger
    {
        private const string TypeDescription = "Timed execution";
            
            private Timer checkTimer;

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        [DataMember(Name = "run-profile-name")]
        public string RunProfileName { get; set; }

        private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;
            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(this.RunProfileName));
        }

        public void Start()
        {
            if (this.RunProfileName == null)
            {
                Logger.WriteLine("Ignoring interval trigger with no run profile name");
                return;
            }

            Logger.WriteLine("Starting interval timer for {0} at {1}", LogLevel.Debug, this.RunProfileName, this.Interval);

            this.checkTimer = new Timer
            {
                Interval = this.Interval.TotalMilliseconds,
                AutoReset = true
            };

            this.checkTimer.Elapsed += this.CheckTimer_Elapsed;
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

        public string DisplayName => $"{this.Type} - {this.Description}";

        public string Type => TypeDescription;

        public string Description => $"{this.RunProfileName} every {this.Interval}";

        public override string ToString()
        {
            return $"{this.DisplayName}";
        }

        public event ExecutionTriggerEventHandler TriggerExecution;

        public static bool CanCreateForMA(ManagementAgent ma)
        {
            return true;
        }

        public IntervalExecutionTrigger(ManagementAgent ma)
        {
            this.RunProfileName = ma.RunProfiles?.Select(t => t.Key).FirstOrDefault();
            this.Interval = new TimeSpan(0, 15, 0);
        }
    }
}
