using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lithnet.Miiserver.AutoSync
{
    public class SyncCompleteEventArgs : EventArgs
    {
        public Guid TargetMA { get; set; }

        public string SendingMAName { get; set; }
    }
}
