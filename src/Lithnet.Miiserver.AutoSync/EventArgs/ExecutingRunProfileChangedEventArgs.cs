using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class ExecutingRunProfileChangedEventArgs : EventArgs
    {
        internal ExecutingRunProfileChangedEventArgs(string executingRunProfile, string maName)
        {
            this.ExecutingRunProfile = executingRunProfile;
            this.MAName = maName;
        }

        public string ExecutingRunProfile { get; private set; }

        public string MAName { get; private set; }
    }
}
