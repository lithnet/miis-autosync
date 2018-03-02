using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class MAStatus : INotifyPropertyChanged
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        public delegate void StateChangedEventHandler(object sender, MAStatusChangedEventArgs e);

        public event StateChangedEventHandler StateChanged;

        [DataMember]
        public Guid ManagementAgentID { get; set; }

        [DataMember]
        public string ManagementAgentName { get; set; }

        [DataMember]
        public string ExecutingRunProfile { get; set; }

        [DataMember]
        public string ExecutionQueue { get; set; }

        [DataMember]
        public ExecutionParameters[] ExecutionQueueItems { get; set; }

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
        public bool HasError { get; internal set; }
        
        [DataMember]
        public bool HasExclusiveLock { get; internal set; }

        [DataMember]
        public bool HasForeignLock { get; internal set; }

        [DataMember]
        public bool ThresholdExceeded { get; internal set; }

        public MAStatus()
        {

        }

        internal MAStatus(string managementAgentName, Guid managementAgentID)
        {
            this.ManagementAgentName = managementAgentName;
            this.ManagementAgentID = managementAgentID;
        }

        private void RaiseStateChange()
        {
            Task.Run(() =>
            {
                try
                {
                    this.StateChanged?.Invoke(this, new MAStatusChangedEventArgs(this, this.ManagementAgentName));
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Unable to relay state change");
                }
            }); // Using the global cancellation token here prevents the final state messages being received (see issue #80)
        }
        
        internal void UpdateExecutionStatus(ControllerState state, string message)
        {
            this.ExecutionState = state;
            this.Message = message;
        }

        internal void UpdateExecutionStatus(ControllerState state, string message, string executingRunProfile)
        {
            this.ExecutionState = state;
            this.Message = message;
            this.ExecutingRunProfile = executingRunProfile;
        }

        internal void UpdateExecutionStatus(ControllerState state, string message, string executingRunProfile, string executionQueue)
        {
            this.ExecutionState = state;
            this.Message = message;
            this.ExecutingRunProfile = executingRunProfile;
            this.ExecutionQueue = executionQueue;
        }

        public void ClearExecutionStatus()
        {
            this.ExecutingRunProfile = null;
            this.ExecutionQueue = null;
            this.ExecutionState = ControllerState.Idle;
        }

        public void Reset()
        {
            this.ClearExecutionStatus();
            this.Message = null;
            this.ThresholdExceeded = false;
            this.HasError = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            this.RaiseStateChange();
        }
    }
}
