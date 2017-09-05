using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
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

        private static ConcurrentDictionary<string, MAStateChangedEventHandler> statusChangedEventHandlers;

        private static ConcurrentDictionary<string, RunProfileExecutionCompleteEventHandler> executionCompleteEventHandlers;

        internal delegate void MAStateChangedEventHandler(MAStatus status);

        internal delegate void RunProfileExecutionCompleteEventHandler(RunProfileExecutionCompleteEventArgs e);

        internal static void NotifySubscribersOnStatusChange(MAStatus status)
        {
            if (EventService.statusChangedEventHandlers.ContainsKey(status.MAName))
            {
                try
                {
                    EventService.statusChangedEventHandlers[status.MAName]?.Invoke(status);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Error notifying client");
                    Logger.WriteException(ex);
                }
            }
        }

        internal static void NotifySubscribersOnRunProfileExecutionComplete(string managementAgentName, RunProfileExecutionCompleteEventArgs e)
        {
            if (EventService.executionCompleteEventHandlers.ContainsKey(managementAgentName))
            {
                try
                {
                    EventService.executionCompleteEventHandlers[managementAgentName]?.Invoke(e);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Error notifying client");
                    Logger.WriteException(ex);
                }
            }
        }


        static EventService()
        {
            EventService.statusChangedEventHandlers = new ConcurrentDictionary<string, MAStateChangedEventHandler>(StringComparer.OrdinalIgnoreCase);
            EventService.executionCompleteEventHandlers = new ConcurrentDictionary<string, RunProfileExecutionCompleteEventHandler>(StringComparer.OrdinalIgnoreCase);
        }

        public void Register(string managementAgentName)
        {
            try
            {
                IEventCallBack subscriber = OperationContext.Current.GetCallbackChannel<IEventCallBack>();

                if (!EventService.statusChangedEventHandlers.ContainsKey(managementAgentName))
                {
                    EventService.statusChangedEventHandlers.TryAdd(managementAgentName, null);
                }

                EventService.statusChangedEventHandlers[managementAgentName] += subscriber.MAStatusChanged;


                if (!EventService.executionCompleteEventHandlers.ContainsKey(managementAgentName))
                {
                    EventService.executionCompleteEventHandlers.TryAdd(managementAgentName, null);
                }

                EventService.executionCompleteEventHandlers[managementAgentName] += subscriber.RunProfileExecutionComplete;


                // ReSharper disable once SuspiciousTypeConversion.Global
                ICommunicationObject commObj = subscriber as ICommunicationObject;
                if (commObj != null)
                {
                    commObj.Faulted += this.CommObj_Faulted;
                    commObj.Closed += this.CommObj_Closed;
                }

                Trace.WriteLine($"Registered callback channel for {managementAgentName}");

            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred with the client registration");
                Logger.WriteException(ex);
                throw;
            }
        }

        private void CommObj_Closed(object sender, EventArgs e)
        {
            this.DeregisterCallbackChannel(sender);
            Trace.WriteLine("Deregistered closed callback channel");
        }

        private void CommObj_Faulted(object sender, EventArgs e)
        {
            this.DeregisterCallbackChannel(sender);
            Trace.WriteLine("Deregistered faulted callback channel");
        }

        private void DeregisterCallbackChannel(object sender)
        {
            IEventCallBack subscriber = sender as IEventCallBack;

            if (subscriber != null)
            {
                foreach (string ma in EventService.statusChangedEventHandlers.Keys.ToArray())
                {
                    EventService.statusChangedEventHandlers[ma] -= subscriber.MAStatusChanged;
                }

                foreach (string ma in EventService.executionCompleteEventHandlers.Keys.ToArray())
                {
                    EventService.executionCompleteEventHandlers[ma] -= subscriber.RunProfileExecutionComplete;
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                ICommunicationObject commObj = subscriber as ICommunicationObject;
                if (commObj != null)
                {
                    commObj.Closed -= this.CommObj_Closed;
                }
            }
        }

        public MAStatus GetFullUpdate(string managementAgentName)
        {
            try
            {
                return Program.Engine?.GetMAState(managementAgentName);
            }
            catch (NoSuchManagementAgentException)
            {
                Logger.WriteLine($"The client requested a state update for a management agent that doesn't exist {managementAgentName}");
                return null;
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

