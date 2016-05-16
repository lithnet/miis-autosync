using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            :this(runProfileName)
        {
            this.Exclusive = exclusive;
        }

        public ExecutionParameters(MARunProfileType runProfileType, bool exclusive)
            :this(runProfileType)
        {
            this.Exclusive = exclusive;
        }

        public override bool Equals(object obj)
        {
            ExecutionParameters p2 = obj as ExecutionParameters;

            if (p2 == null)
            {
                return base.Equals(obj);
            }

            return (string.Equals(this.RunProfileName, p2.RunProfileName, StringComparison.OrdinalIgnoreCase) &&
                this.RunProfileType == p2.RunProfileType &&
                this.Exclusive == p2.Exclusive);
        }

        public override int GetHashCode()
        {
            string hashcode = string.Format("{0}{1}{2}", this.RunProfileName, this.RunProfileType.ToString(), this.Exclusive.ToString());
            return hashcode.GetHashCode();
        }

        public static bool operator ==(ExecutionParameters a, ExecutionParameters b)
        {
            if (System.Object.ReferenceEquals(a, b))
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
