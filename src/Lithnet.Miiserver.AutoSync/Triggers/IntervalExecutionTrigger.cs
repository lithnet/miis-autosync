using System;
using System.Runtime.Serialization;
using System.Timers;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "interval-trigger")]
    public class IntervalExecutionTrigger : IMAExecutionTrigger
    {
        private Timer checkTimer;

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        [DataMember(Name = "run-profile-name")]
        public string RunProfileName { get; set; }

        public IntervalExecutionTrigger(string runProfileName, TimeSpan interval)
        {
            this.Interval = interval;
            this.RunProfileName = runProfileName;
        }

        private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;
            registeredHandlers?.Invoke(this, new ExecutionTriggerEventArgs(this.RunProfileName));
        }

        public void Start()
        {
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

        public string Type => "Timed execution";

        public string Description => $"{this.RunProfileName} every {this.Interval}";

        public override string ToString()
        {
            return $"{this.DisplayName}";
        }

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}
