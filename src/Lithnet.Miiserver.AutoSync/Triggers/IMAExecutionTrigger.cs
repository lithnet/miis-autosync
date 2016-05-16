using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lithnet.Miiserver.AutoSync
{
    public delegate void ExecutionTriggerEventHandler(object sender, ExecutionTriggerEventArgs e);

    public interface IMAExecutionTrigger
    {
        string Name { get; }

        event ExecutionTriggerEventHandler TriggerExecution;

        void Start();

        void Stop();
    }
}
