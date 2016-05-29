using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class SyncCompleteEventArgs : EventArgs
    {
        public Guid TargetMA { get; set; }

        public string SendingMAName { get; set; }
    }
}
