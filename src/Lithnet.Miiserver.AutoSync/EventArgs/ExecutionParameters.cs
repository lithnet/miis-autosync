using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class ExecutionParameters
    {
        public bool Exclusive { get; set; }

        public bool RunImmediate { get; set; }

        public string RunProfileName { get; set; }

        public MARunProfileType RunProfileType { get; set; }

        public Guid PartitionID { get; set; }

        public long QueueID { get; set; }

        public string PartitionName { get; set; }

        public ExecutionParameters()
        {
        }

        public ExecutionParameters(string runProfileName)
            : this(runProfileName, false, false)
        {
        }

        public ExecutionParameters(string runProfileName, bool exclusive)
            : this(runProfileName, exclusive, false)
        {
        }

        public ExecutionParameters(string runProfileName, bool exclusive, bool runImmediate)
        {
            this.RunProfileName = runProfileName;
            this.Exclusive = exclusive;
            this.RunImmediate = runImmediate;
        }

        public ExecutionParameters(MARunProfileType runProfileType)
            : this(runProfileType, null, false, false)
        {
        }

        public ExecutionParameters(MARunProfileType runProfileType, Guid partitionID)
            : this(runProfileType, partitionID, false, false)
        {
        }

        public ExecutionParameters(MARunProfileType runProfileType, bool exclusive, bool runImmediate)
            : this(runProfileType, null, exclusive, runImmediate)
        {
        }

        public ExecutionParameters(MARunProfileType runProfileType, Guid partitionID, bool exclusive, bool runImmediate)
        {
            this.RunProfileType = runProfileType;
            this.PartitionID = partitionID;
            this.Exclusive = exclusive;
            this.RunImmediate = runImmediate;
        }

        public ExecutionParameters(MARunProfileType runProfileType, string partitionName, bool exclusive, bool runImmediate)
        {
            this.RunProfileType = runProfileType;
            this.PartitionName = partitionName;
            this.Exclusive = exclusive;
            this.RunImmediate = runImmediate;
        }

        public override bool Equals(object obj)
        {
            ExecutionParameters p2 = obj as ExecutionParameters;

            return ExecutionParameters.Compare(this, p2);
        }

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            if (this.RunProfileName != null)
            {
                return this.RunProfileName.GetHashCode();
            }

            string hashcode = $"{this.RunProfileType}{this.PartitionID}{this.PartitionName}";
            // ReSharper restore NonReadonlyMemberInGetHashCode
            return hashcode.GetHashCode();
        }

        public static bool operator ==(ExecutionParameters a, ExecutionParameters b)
        {
            return ExecutionParameters.Compare(a, b);
        }

        public static bool operator !=(ExecutionParameters a, ExecutionParameters b)
        {
            return !ExecutionParameters.Compare(a, b);
        }

        private static bool Compare(ExecutionParameters a, ExecutionParameters b)
        {
            if (object.ReferenceEquals(a, b))
            {
                return true;
            }

            if ((object)a == null || (object)b == null)
            {
                return false;
            }

            if (a.Exclusive != b.Exclusive)
            {
                return false;
            }

            if (a.RunProfileName != null)
            {
                return string.Equals(a.RunProfileName, b.RunProfileName, StringComparison.InvariantCultureIgnoreCase);
            }

            if (a.RunProfileType != b.RunProfileType)
            {
                return false;
            }

            if (a.PartitionName != null)
            {
                return string.Equals(a.PartitionName, b.PartitionName, StringComparison.InvariantCultureIgnoreCase);
            }

            return a.PartitionID == b.PartitionID;
        }
    }
}