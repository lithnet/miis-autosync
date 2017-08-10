using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class ExecutionQueueChangedEventArgs : EventArgs
    {
        internal ExecutionQueueChangedEventArgs(string executionQueue, string maName)
        {
            this.ExecutionQueue = executionQueue;
            this.MAName = maName;
        }

        public string ExecutionQueue { get; private set; }

        public string MAName { get; private set; }
    }
}
