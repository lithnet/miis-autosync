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

        private static Dictionary<string, MAStateChangedEventHandler> eventHandlers;

        internal delegate void MAStateChangedEventHandler(MAStatus status);

        internal static void NotifySubscribers(MAStatus status)
        {
            if (EventService.eventHandlers.ContainsKey(status.MAName))
            {
                EventService.eventHandlers[status.MAName]?.Invoke(status);
            }
        }

        static EventService()
        {
            EventService.eventHandlers = new Dictionary<string, MAStateChangedEventHandler>(StringComparer.OrdinalIgnoreCase);
        }

        public void Register(string managementAgentName)
        {
            try
            {
                IEventCallBack subscriber = OperationContext.Current.GetCallbackChannel<IEventCallBack>();

                if (!EventService.eventHandlers.ContainsKey(managementAgentName))
                {
                    EventService.eventHandlers.Add(managementAgentName, null);
                }

                EventService.eventHandlers[managementAgentName] += subscriber.MAStatusChanged;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred with the client registration");
                Logger.WriteException(ex);
                throw;
            }
        }

        public MAStatus GetFullUpdate(string managementAgentName)
        {
            try
            {
                return Program.GetMAState(managementAgentName);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"An error occurred while getting the client a full state update for {managementAgentName}");
                Logger.WriteException(ex);
                throw;
            }
        }
    }
}
