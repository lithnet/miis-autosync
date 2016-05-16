using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public class ExecutionTriggerEventArgs : EventArgs
    {
        public ExecutionParameters Parameters { get; set; }

        public ExecutionTriggerEventArgs()
        {
            this.Parameters = new ExecutionParameters();
        }

        public ExecutionTriggerEventArgs(ExecutionParameters p)
        {
            this.Parameters = p;
        }

        public ExecutionTriggerEventArgs(string runProfileName)
        {
            this.Parameters = new ExecutionParameters(runProfileName);
        }

        public ExecutionTriggerEventArgs(MARunProfileType runProfileType)
        {
            this.Parameters = new ExecutionParameters(runProfileType);
        }
    }
}
