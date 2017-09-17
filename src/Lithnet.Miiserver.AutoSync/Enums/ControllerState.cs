using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public enum ControllerState
    {
        [EnumMember]
        Idle = 0,

        [EnumMember]
        Running = 1,

        [EnumMember]
        Waiting = 2,

        [EnumMember]
        Processing = 3,
    }
}
