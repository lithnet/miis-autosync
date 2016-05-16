using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lithnet.Miiserver.AutoSync
{
    [Flags]
    public enum PersistentSearchChangeType
    {
        None = 0,
        Add = 1,
        Delete = 2,
        Modify = 4,
        ModDN = 8,
        All = Add | Delete | Modify | ModDN
    }
}
