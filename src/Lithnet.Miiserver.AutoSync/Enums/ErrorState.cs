using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public enum ErrorState
    {
        [EnumMember]
        None = 0,

        [EnumMember]
        ThresholdExceeded = 1,

        [EnumMember]
        ControllerFaulted = 2,

        [EnumMember]
        UnexpectedChange = 3,
    }
}
