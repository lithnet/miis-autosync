using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class MAStatus
    {
        private ExecutorState state;

        [DataMember]
        public string MAName { get; set; }

        [DataMember]
        public string ExecutingRunProfile { get; set; }

        [DataMember]
        public string ExecutionQueue { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string LastRunProfileResult { get; set; }

        [DataMember]
        public string LastRunProfileName { get; set; }

        [DataMember]
        public ExecutorState State
        {
            get
            {
                return this.state;
            }
            set
            {
                if (this.state == ExecutorState.Pausing || this.state == ExecutorState.Stopping)
                {
                    if (!MAStatus.IsControlState(value))
                    {
                        return;
                    }
                }

                this.state = value;
            }
        }

        public static bool IsControlState(ExecutorState state)
        {
            return state == ExecutorState.Disabled ||
                   state == ExecutorState.Paused ||
                   state == ExecutorState.Pausing ||
                   state == ExecutorState.Resuming ||
                   state == ExecutorState.Starting ||
                   state == ExecutorState.Stopped ||
                   state == ExecutorState.Stopping;
        }
    }
}
