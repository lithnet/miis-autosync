using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace Lithnet.Miiserver.AutoSync
{
    public class EventClient : DuplexClientBase<IEventService>, IEventService
    {
        protected EventClient(InstanceContext ctx, Binding binding, EndpointAddress endpoint)
            : base(ctx, binding, endpoint)
        {
        }

        public void Register(string managementAgentName)
        {
            this.Channel.Register(managementAgentName);
        }

        public MAStatus GetFullUpdate(string managementAgentName)
        {
            return this.Channel.GetFullUpdate(managementAgentName);
        }

        public static EventClient GetDefaultClient(InstanceContext ctx)
        {
            if (string.Equals(RegistrySettings.AutoSyncServerHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return EventClient.GetNamedPipesClient(ctx);
            }
            else
            {
                return EventClient.GetNetTcpClient(ctx);
            }
        }

        public static EventClient GetNamedPipesClient(InstanceContext ctx)
        {
            return new EventClient(ctx, EventServiceConfiguration.NetNamedPipeBinding, EventServiceConfiguration.NetNamedPipeEndpointAddress);
        }

        public static EventClient GetNetTcpClient(InstanceContext ctx)
        {
            return new EventClient(ctx, EventServiceConfiguration.NetTcpBinding, EventServiceConfiguration.CreateClientTcpEndPointAddress());
        }
    }
}