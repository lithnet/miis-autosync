using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public enum ControlState
    {
        [EnumMember]
        Disabled = 0,

        [EnumMember]
        Running = 1,
       
        [EnumMember]
        Stopped = 2,
      
        [EnumMember]
        Stopping = 3,

        [EnumMember]
        Starting = 4,
    }
}
