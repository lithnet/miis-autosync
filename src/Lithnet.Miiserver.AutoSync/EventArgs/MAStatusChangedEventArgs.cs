using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class MAStatusChangedEventArgs : EventArgs
    {
        internal MAStatusChangedEventArgs(MAStatus status, string maName)
        {
            this.Status = status;
            this.MAName = maName;
        }

        public MAStatus Status { get; private set; }

        public string MAName { get; private set; }
    }
}
