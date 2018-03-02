using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using Lithnet.Miiserver.Client;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    public class EventService : IEventService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
                    logger.Trace("Added service debug behavior");
                }
                else
                {
                    s.Description.Behaviors.Remove(d);
                    s.Description.Behaviors.Add(EventServiceConfiguration.ServiceDebugBehavior);
                    logger.Trace("Replaced service debug behavior");
                }

                s.Authorization.ServiceAuthorizationManager = new EventServiceAuthorizationManager();
                s.Open();

                return s;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not create service host");
                throw;
            }
        }

        private static ConcurrentDictionary<Guid, SynchronizedCollection<IEventCallBack>> subscribers;

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
                        logger.Warn(ex, "Error notifying client. Client will be deregistered");
                        EventService.DeregisterCallbackChannel(i);
                    }
                }
            }
        }

        internal static void NotifySubscribersOnRunProfileExecutionComplete(RunProfileExecutionCompleteEventArgs e)
        {
            if (EventService.subscribers.ContainsKey(e.ManagementAgentID))
            {
                foreach (IEventCallBack i in EventService.subscribers[e.ManagementAgentID].ToArray())
                {
                    try
                    {
                        i.RunProfileExecutionComplete(e);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex, "Error notifying client. Client will be deregistered");
                        EventService.DeregisterCallbackChannel(i);
                    }
                }
            }
        }

        internal static void NotifySubscribersOnMessageLogged(MessageLoggedEventArgs e)
        {
            if (EventService.subscribers.ContainsKey(e.ManagementAgentID))
            {
                foreach (IEventCallBack i in EventService.subscribers[e.ManagementAgentID].ToArray())
                {
                    try
                    {
                        i.MessageLogged(e);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex, "Error notifying client. Client will be deregistered");
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
                if (subscriber is ICommunicationObject commObj)
                {
                    commObj.Faulted += EventService.CommObj_Faulted;
                    commObj.Closed += EventService.CommObj_Closed;
                }

                logger.Trace($"Registered callback channel for {name}/{managementAgentID} on client {EventService.RemoteHost}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred with the client registration");
                throw;
            }
        }

        public bool Ping(Guid managementAgentID)
        {
            IEventCallBack subscriber = OperationContext.Current.GetCallbackChannel<IEventCallBack>();

            if (EventService.subscribers.ContainsKey(managementAgentID))
            {
                if (EventService.subscribers[managementAgentID].Contains(subscriber))
                {
                    // Still registered
                    logger.Trace($"Client ping succeeded for {managementAgentID} at {EventService.RemoteHost}");
                    return true;
                }
            }

            logger.Warn($"Client ping failed for {managementAgentID} at {EventService.RemoteHost}. Client is no longer registered.");

            // Not registered
            return false;
        }

        public string GetRunDetail(Guid managementAgentID, int runNumber)
        {
            return SyncServer.GetRunDetail(managementAgentID, runNumber).GetOuterXml();
        }

        public IEnumerable<CSObjectRef> GetStepDetail(Guid stepID, string statisticsType)
        {
            return SyncServer.GetStepDetailCSObjectRefs(stepID, statisticsType);
        }

        private static string RemoteHost
        {
            get
            {
                if (!OperationContext.Current?.IncomingMessageProperties.ContainsKey(RemoteEndpointMessageProperty.Name) ?? false)
                {
                    return "localhost";
                }

                RemoteEndpointMessageProperty remoteEndpoint = OperationContext.Current?.IncomingMessageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;

                if (remoteEndpoint == null)
                {
                    return null;
                }

                return $"{remoteEndpoint?.Address}:{remoteEndpoint?.Port}";
            }
        }

        private static void CommObj_Closed(object sender, EventArgs e)
        {
            EventService.DeregisterCallbackChannel(sender);
            logger.Trace("Deregistered closed callback channel");
        }

        private static void CommObj_Faulted(object sender, EventArgs e)
        {
            EventService.DeregisterCallbackChannel(sender);
            logger.Trace("Deregistered faulted callback channel");
        }

        private static void DeregisterCallbackChannel(object sender)
        {
            if (sender is IEventCallBack subscriber)
            {
                foreach (SynchronizedCollection<IEventCallBack> callbacks in EventService.subscribers.Values.ToArray())
                {
                    callbacks.Remove(subscriber);
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                if (subscriber is ICommunicationObject commObj)
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
                logger.Warn($"The client requested a state update for a management agent that doesn't exist {managementAgentID}");
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"An error occurred while getting the client a full state update for {managementAgentID}");
                throw;
            }
        }
    }
}

