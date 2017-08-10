using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class StatusChangedEventArgs : EventArgs
    {
        internal StatusChangedEventArgs(string status, string maName)
        {
            this.Status = status;
            this.MAName = maName;
        }

        public string Status { get; private set; }

        public string MAName { get; private set; }
    }
}
