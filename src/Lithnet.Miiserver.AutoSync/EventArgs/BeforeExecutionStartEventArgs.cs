using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class BeforeExecutionStartEventArgs : EventArgs
    {
        public Guid PartitionID { get; set; }

        public string RunProfile { get; set; }

        public BeforeExecutionStartEventArgs(string runProfile, Guid partitionID)
        {
            this.RunProfile = runProfile;
            this.PartitionID = partitionID;
        }
    }
}
