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

        public ExecutorState DisplayState
        {
            get
            {
                if (this.ControlState != ExecutorState.Running)
                {
                    return this.ControlState;
                }
                else
                {
                    return this.ExecutionState;
                }
            }
        }

        [DataMember]
        public ExecutorState ControlState { get; set; }


        [DataMember]
        public ExecutorState ExecutionState { get; set; }

        [DataMember]
        public int ActiveVersion { get; set; }
        
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
