using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using Lithnet.ResourceManagement.Client;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public class IntervalExecutionTrigger : IMAExecutionTrigger
    {
        public Timer checkTimer;

        public int TriggerInterval { get; set; }
        
        public MARunProfileType RunProfileTargetType { get; set; }

        public IntervalExecutionTrigger(MARunProfileType type, int intervalSeconds)
        {
            this.TriggerInterval = intervalSeconds;
            this.RunProfileTargetType = type;
        }

        private void checkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutionTriggerEventHandler registeredHandlers = this.TriggerExecution;

            if (registeredHandlers != null)
            {
                registeredHandlers(this, new ExecutionTriggerEventArgs(this.RunProfileTargetType));
            }
        }

        public void Start()
        {
            Logger.WriteLine("Starting interval timer for {0} at {1} seconds", LogLevel.Debug, this.RunProfileTargetType, this.TriggerInterval);

            this.checkTimer = new Timer(this.TriggerInterval * 1000);

            this.checkTimer.AutoReset = true;
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
                return string.Format("{0} second interval", this.TriggerInterval);
            }
        }

        public event ExecutionTriggerEventHandler TriggerExecution;
    }
}
