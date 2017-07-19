using System.ComponentModel;
using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public enum ExecutionErrorBehaviour
    {
        [EnumMember(Value = "terminate")]
        [Description("Terminate the script")]
        Terminate = 0,

        [Description("Reset the script and resume execution")]
        [EnumMember(Value = "reset")]
        ResetAndResume = 1,
    }
}
