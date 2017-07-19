using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public enum RunMode
    {
        [EnumMember(Value = "supported")]
        Supported = 0,

        [EnumMember(Value = "unsupported")]
        Unsupported = 1,

        [EnumMember(Value = "exclusive")]
        Exclusive = 2
    }
}
