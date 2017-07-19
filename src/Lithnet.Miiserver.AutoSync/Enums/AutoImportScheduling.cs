using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public enum AutoImportScheduling
    {
        [EnumMember(Value="enabled")]
        Enabled = 0,

        [EnumMember(Value="disabled")]
        Disabled = 1,
    }
}
