using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using Lithnet.Logging;
using System.Diagnostics;
using System.ServiceModel.Channels;

namespace Lithnet.Miiserver.AutoSync
{
    public class EventService : IEventService
    {
        public static ServiceHost CreateNetNamedPipeInstance()
        {
            return EventService.CreateInstance(EventServiceConfiguration.NetNamedPipeBinding, EventServiceConfiguration.NamedPipeUri);
        }

        public static ServiceHost CreateNetTcpInstance()
        {
            return EventService.CreateInstance(EventServiceConfiguration.NetTcpBinding, EventServiceConfiguration.CreateServerBindingUri());
        }

        private static ServiceHost CreateInstance(Binding binding, string uri)
        {
            try
            {
                ServiceHost s = new ServiceHost(typeof(EventService));
                s.AddServiceEndpoint(typeof(IEventService), binding, uri);
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

        private static ConcurrentDictionary<Guid, SynchronizedCollection<IEventCallBack>> subscribers;

        internal delegate void MAStateChangedEventHandler(MAStatus status);

        internal delegate void RunProfileExecutionCompleteEventHandler(RunProfileExecutionCompleteEventArgs e);

        internal static void NotifySubscribersOnStatusChange(MAStatus status)
        {
            if (EventService.subscribers.ContainsKey(status.ManagementAgentID))
            {
                foreach (IEventCallBack i in EventService.subscribers[status.ManagementAgentID].ToArray())
                {
                    try
                    {
                        i.MAStatusChanged(status);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine("Error notifying client. Client will be deregistered");
                        Logger.WriteException(ex);
                        EventService.DeregisterCallbackChannel(i);
                    }
                }
            }
        }

        internal static void NotifySubscribersOnRunProfileExecutionComplete(Guid managementAgentID, RunProfileExecutionCompleteEventArgs e)
        {
            if (EventService.subscribers.ContainsKey(managementAgentID))
            {
                foreach (IEventCallBack i in EventService.subscribers[managementAgentID].ToArray())
                {
                    try
                    {
                        i.RunProfileExecutionComplete(e);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine("Error notifying client. Client will be deregistered");
                        Logger.WriteException(ex);
                        EventService.DeregisterCallbackChannel(i);
                    }
                }
            }
        }

        static EventService()
        {
            EventService.subscribers = new ConcurrentDictionary<Guid, SynchronizedCollection<IEventCallBack>>();
        }

        public void Register(Guid managementAgentID)
        {
            try
            {
                string name = Global.GetManagementAgentName(managementAgentID);

                if (name == null)
                {
                    throw new NoSuchManagementAgentException();
                }

                IEventCallBack subscriber = OperationContext.Current.GetCallbackChannel<IEventCallBack>();

                if (!EventService.subscribers.ContainsKey(managementAgentID))
                {
                    EventService.subscribers.TryAdd(managementAgentID, new SynchronizedCollection<IEventCallBack>());
                }

                if (!EventService.subscribers[managementAgentID].Contains(subscriber))
                {
                    EventService.subscribers[managementAgentID].Add(subscriber);
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                ICommunicationObject commObj = subscriber as ICommunicationObject;
                if (commObj != null)
                {
                    commObj.Faulted += EventService.CommObj_Faulted;
                    commObj.Closed += EventService.CommObj_Closed;
                }

                Trace.WriteLine($"Registered callback channel for {name}/{managementAgentID}");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("An error occurred with the client registration");
                Logger.WriteException(ex);
                throw;
            }
        }

        private static void CommObj_Closed(object sender, EventArgs e)
        {
            EventService.DeregisterCallbackChannel(sender);
            Trace.WriteLine("Deregistered closed callback channel");
        }

        private static void CommObj_Faulted(object sender, EventArgs e)
        {
            EventService.DeregisterCallbackChannel(sender);
            Trace.WriteLine("Deregistered faulted callback channel");
        }

        private static void DeregisterCallbackChannel(object sender)
        {
            IEventCallBack subscriber = sender as IEventCallBack;

            if (subscriber != null)
            {
                foreach (SynchronizedCollection<IEventCallBack> callbacks in EventService.subscribers.Values.ToArray())
                {
                    callbacks.Remove(subscriber);
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                ICommunicationObject commObj = subscriber as ICommunicationObject;
                if (commObj != null)
                {
                    commObj.Faulted -= EventService.CommObj_Faulted;
                    commObj.Closed -= EventService.CommObj_Closed;
                }
            }
        }

        public MAStatus GetFullUpdate(Guid managementAgentID)
        {
            try
            {
                return Program.Engine?.GetMAState(managementAgentID);
            }
            catch (NoSuchManagementAgentException)
            {
                Logger.WriteLine($"The client requested a state update for a management agent that doesn't exist {managementAgentID}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"An error occurred while getting the client a full state update for {managementAgentID}");
                Logger.WriteException(ex);
                throw;
            }
        }
    }
}

