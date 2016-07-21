using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class ExecutionParameters
    {
        public bool Exclusive { get; set; }

        public string RunProfileName { get; set; }

        public MARunProfileType RunProfileType { get; set; }

        public ExecutionParameters()
        {
        }

        public ExecutionParameters(string runProfileName)
        {
            this.RunProfileName = runProfileName;
        }

        public ExecutionParameters(MARunProfileType runProfileType)
        {
            this.RunProfileType = runProfileType;
        }

        public ExecutionParameters(string runProfileName, bool exclusive)
            : this(runProfileName)
        {
            this.Exclusive = exclusive;
        }

        public ExecutionParameters(MARunProfileType runProfileType, bool exclusive)
            : this(runProfileType)
        {
            this.Exclusive = exclusive;
        }

        public override bool Equals(object obj)
        {
            ExecutionParameters p2 = obj as ExecutionParameters;

            if (p2 == null)
            {
                return object.ReferenceEquals(this, obj);
            }

            if (!string.IsNullOrWhiteSpace(this.RunProfileName))
            {
                if (string.Equals(this.RunProfileName, p2.RunProfileName, StringComparison.OrdinalIgnoreCase) && this.Exclusive == p2.Exclusive)
                {
                    return true;
                }
            }

            if (this.RunProfileType == p2.RunProfileType && this.Exclusive == p2.Exclusive)
            {
                return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            string hashcode = $"{this.RunProfileName}{this.RunProfileType}{this.Exclusive}";
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

            return (string.Equals(a.RunProfileName, b.RunProfileName, StringComparison.OrdinalIgnoreCase) &&
                a.RunProfileType == b.RunProfileType &&
                a.Exclusive == b.Exclusive);
        }

        public static bool operator !=(ExecutionParameters a, ExecutionParameters b)
        {
            return !(a == b);
        }
    }
}
