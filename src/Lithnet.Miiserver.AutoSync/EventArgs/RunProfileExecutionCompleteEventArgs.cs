using System;
using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class RunProfileExecutionCompleteEventArgs : EventArgs
    {
        public RunProfileExecutionCompleteEventArgs(string managementAgentName,
            Guid managementAgentID,
            string runProfileName,
            string result,
            int runNumber,
            DateTime? startDate,
            DateTime? endDate)
        {
            this.ManagementAgentName = managementAgentName;
            this.RunProfileName = runProfileName;
            this.Result = result;
            this.RunNumber = runNumber;
            this.StartDate = startDate;
            this.EndDate = endDate;
            this.ManagementAgentID = managementAgentID;
        }

        [DataMember]
        public string RunProfileName { get; set; }

        [DataMember]
        public string Result { get; set; }

        [DataMember]
        public int RunNumber { get; set; }

        [DataMember]
        public DateTime? EndDate { get; set; }

        [DataMember]
        public DateTime? StartDate { get; set; }

        [DataMember]
        public string ManagementAgentName { get; set; }

        [DataMember]
        public Guid ManagementAgentID { get; set; }
    }
}
