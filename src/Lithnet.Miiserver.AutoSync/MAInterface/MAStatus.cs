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
        public Guid ManagementAgentID { get; set; }

        [DataMember]
        public string ManagementAgentName { get; set; }

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
        public ControllerState ExecutionState { get; set; }

        [DataMember]
        public int ActiveVersion { get; set; }
        
        [DataMember]
        public bool HasSyncLock { get; internal set; }

        [DataMember]
        public bool HasExclusiveLock { get; internal set; }

        [DataMember]
        public bool HasForeignLock { get; internal set; }

        [DataMember]
        public ErrorState ErrorState { get; internal set; }
        
        public void Clear()
        {
            this.ExecutingRunProfile = null;
            this.ExecutionQueue = null;
            this.Message = null;
        }
    }
}
