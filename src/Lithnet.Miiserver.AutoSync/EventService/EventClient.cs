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

        public void Register(Guid managementAgentID)
        {
            this.Channel.Register(managementAgentID);
        }

        public MAStatus GetFullUpdate(Guid managementAgentID)
        {
            return this.Channel.GetFullUpdate(managementAgentID);
        }

        public static EventClient GetNamedPipesClient(InstanceContext ctx)
        {
            return new EventClient(ctx, EventServiceConfiguration.NetNamedPipeBinding, EventServiceConfiguration.NetNamedPipeEndpointAddress);
        }

        public static EventClient GetNetTcpClient(InstanceContext ctx, string hostname, string port, string expectedServerIdentityFormat)
        {
            return new EventClient(ctx, EventServiceConfiguration.NetTcpBinding, EventServiceConfiguration.CreateTcpEndPointAddress(hostname, port, expectedServerIdentityFormat));
        }
    }
}