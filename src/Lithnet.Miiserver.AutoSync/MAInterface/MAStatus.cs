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

        public string DisplayState
        {
            get
            {
                if (this.ControlState != ControlState.Running)
                {
                    return this.ControlState.ToString();
                }
                else
                {
                    return this.ExecutionState.ToString();
                }
            }
        }

        [DataMember]
        public ControlState ControlState { get; set; }

        [DataMember]
        public ExecutorState ExecutionState { get; set; }

        [DataMember]
        public int ActiveVersion { get; set; }

        [DataMember]
        public string Detail { get; set; }

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

        public void Clear()
        {
            this.ExecutingRunProfile = null;
            this.ExecutionQueue = null;
            this.Message = null;
            this.Detail = null;
        }
    }
}
