using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lithnet.Miiserver.AutoSync
{
    public enum MARunProfileType
    {
        None,
        DeltaImport,
        FullImport,
        Export,
        DeltaSync,
        FullSync
    }
}
