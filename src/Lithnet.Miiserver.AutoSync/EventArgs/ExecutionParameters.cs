using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class ExecutionParameters
    {
        public bool Exclusive { get; set; }

        public string RunProfileName { get; set; }

        public MARunProfileType RunProfileType { get; set; }

        public Guid PartitionID { get; set; }

        public string PartitionName { get; set; }

        public ExecutionParameters()
        {
        }

        public ExecutionParameters(string runProfileName)
            : this(runProfileName, false)
        {
        }

        public ExecutionParameters(string runProfileName, bool exclusive)
        {
            this.RunProfileName = runProfileName;
            this.Exclusive = exclusive;
        }
        public ExecutionParameters(MARunProfileType runProfileType)
            : this(runProfileType, null, false)
        {
        }

        public ExecutionParameters(MARunProfileType runProfileType, Guid partitionID)
            : this(runProfileType, partitionID, false)
        {
        }

        public ExecutionParameters(MARunProfileType runProfileType, bool exclusive)
            : this(runProfileType, null, exclusive)
        {
        }

        public ExecutionParameters(MARunProfileType runProfileType, Guid partitionID, bool exclusive)
        {
            this.RunProfileType = runProfileType;
            this.PartitionID = partitionID;
            this.Exclusive = exclusive;
        }

        public ExecutionParameters(MARunProfileType runProfileType, string partitionName, bool exclusive)
        {
            this.RunProfileType = runProfileType;
            this.PartitionName = partitionName;
            this.Exclusive = exclusive;
        }

        public override bool Equals(object obj)
        {
            ExecutionParameters p2 = obj as ExecutionParameters;

            if (object.ReferenceEquals(this, p2))
            {
                return true;
            }

            if ((object)p2 == null)
            {
                return false;
            }
            
            return
                string.Equals(this.RunProfileName, p2.RunProfileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(this.PartitionName, p2.PartitionName, StringComparison.OrdinalIgnoreCase) &&
                this.PartitionID == p2.PartitionID &&
                this.RunProfileType == p2.RunProfileType &&
                this.Exclusive == p2.Exclusive;
        }

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            string hashcode = $"{this.RunProfileName}{this.RunProfileType}{this.Exclusive}{this.PartitionID}{this.PartitionName}";
            // ReSharper restore NonReadonlyMemberInGetHashCode
            return hashcode.GetHashCode();
        }

        public static bool operator ==(ExecutionParameters a, ExecutionParameters b)
        {
            if (object.ReferenceEquals(a, b))
            {
                return true;
            }

            if (((object)a == null) || ((object)b == null))
            {
                return false;
            }

            return
                string.Equals(a.RunProfileName, b.RunProfileName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.PartitionName, b.PartitionName, StringComparison.OrdinalIgnoreCase) &&
                a.PartitionID == b.PartitionID &&
                a.RunProfileType == b.RunProfileType &&
                a.Exclusive == b.Exclusive;
        }

        public static bool operator !=(ExecutionParameters a, ExecutionParameters b)
        {
            return !(a == b);
        }
    }
}
