using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Logging;
using System.Diagnostics;

namespace Lithnet.Miiserver.AutoSync
{
    public class EventService : IEventService
    {
        public static ServiceHost CreateInstance()
        {
            try
            {
                ServiceHost s = new ServiceHost(typeof(EventService));
                s.AddServiceEndpoint(typeof(IEventService), EventServiceConfiguration.NetNamedPipeBinding, EventServiceConfiguration.NamedPipeUri);
                if (s.Description.Behaviors.Find<ServiceMetadataBehavior>() == null)
                {
                    s.Description.Behaviors.Add(EventServiceConfiguration.ServiceMetadataDisabledBehavior);
                }

                var d = s.Description.Behaviors.Find<ServiceDebugBehavior>();

                if (d == null)
                {
                    s.Description.Behaviors.Add(EventServiceConfiguration.ServiceDebugBehavior);
                    Trace.WriteLine("Added service debug behavior");
                }
                else
                {
                    s.Description.Behaviors.Remove(d);
                    s.Description.Behaviors.Add(EventServiceConfiguration.ServiceDebugBehavior);
                    Trace.WriteLine("Replaced service debug behavior");
                }

                s.Authorization.ServiceAuthorizationManager = new EventServiceAuthorizationManager();
                s.Open();

                return s;
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw;
            }
        }
        
        internal static StatusChangedEventHandler StatusChanged;
        internal delegate void StatusChangedEventHandler(string status, string managementAgentName);

        internal static ExecutingRunProfileChangedEventHandler ExecutingRunProfileChanged;
        internal delegate void ExecutingRunProfileChangedEventHandler(string runProfileName, string managementAgentName);
        
        internal static ExecutionQueueChangedEventHandler ExecutionQueueChanged;
        internal delegate void ExecutionQueueChangedEventHandler(string executionQueue, string managementAgentName);

        public void Register()
        {
            IEventCallBack subscriber = OperationContext.Current.GetCallbackChannel<IEventCallBack>();
            EventService.StatusChanged += subscriber.StatusChanged;
            EventService.ExecutionQueueChanged += subscriber.ExecutionQueueChanged;
            EventService.ExecutingRunProfileChanged += subscriber.ExecutingRunProfileChanged;
        }
    }
}
