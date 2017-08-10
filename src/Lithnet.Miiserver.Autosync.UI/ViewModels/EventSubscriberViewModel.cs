using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.AutoSync.UI
{
    public class EventSubscriberViewModel : ViewModelBase , IEventCallBack
    {
        internal ObservableCollection<string> Events = new ObservableCollection<string>();

        public void ExecutionQueueChanged(string executionQueue, string managementAgent)
        {
            this.Events.Add($"{managementAgent}: queue: {executionQueue}");
        }

        public void ExecutingRunProfileChanged(string executingRunProfile, string managementAgent)
        {
            this.Events.Add($"{managementAgent}: profile: {executingRunProfile}");
        }

        public void StatusChanged(string status, string managementAgent)
        {
            this.Events.Add($"{managementAgent}: status: {status}");
        }
    }
}
