using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public enum ExecutorState
    {
        [EnumMember]
        Idle = 0,

        [EnumMember]
        Running = 1,

        [EnumMember]
        Waiting = 2,

        [EnumMember]
        Processing = 3
    }
}
