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
        Processing = 3,

        [EnumMember]
        Stopped = 4,

        [EnumMember]
        Disabled = 5,

        [EnumMember]
        Stopping = 6,

        [EnumMember]
        Starting = 7,

        [EnumMember]
        Paused = 8,

        [EnumMember]
        Pausing = 9,

        [EnumMember]
        Resuming = 10,
    }
}
