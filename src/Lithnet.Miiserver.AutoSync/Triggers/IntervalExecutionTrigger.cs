﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Timers;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "interval-trigger")]
    [Description(TypeDescription)]
    public class IntervalExecutionTrigger : MAExecutionTrigger
    {
        private const string TypeDescription = "Timed execution";

        private Timer checkTimer;

        [DataMember(Name = "interval")]
        public TimeSpan Interval { get; set; }

        [DataMember(Name = "run-profile-name")]
        public string RunProfileName { get; set; }

        private void CheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Fire(this.RunProfileName);
        }

        public override void Start()
        {
            if (this.RunProfileName == null)
            {
                this.LogError("Ignoring interval trigger with no run profile name");
                return;
            }

            Trace.WriteLine($"Starting interval timer for {this.RunProfileName} at {this.Interval}");

            this.checkTimer = new Timer
            {
                Interval = this.Interval.TotalMilliseconds,
                AutoReset = true
            };

            this.checkTimer.Elapsed += this.CheckTimer_Elapsed;
            this.checkTimer.Start();
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

        public override string DisplayName => $"{this.Type} - {this.Description}";

        public override string Type => TypeDescription;

        public override string Description => $"{this.RunProfileName} every {this.Interval}";

        public override string ToString()
        {
            return $"{this.DisplayName}";
        }

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
