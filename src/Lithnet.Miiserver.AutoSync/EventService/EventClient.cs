using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.ServiceModel.Description;

namespace Lithnet.Miiserver.AutoSync
{
    public class EventClient : DuplexClientBase<IEventService>, IEventService
    {
        public EventClient(InstanceContext ctx)
            : base(ctx, EventServiceConfiguration.NetNamedPipeBinding, EventServiceConfiguration.NetNamedPipeEndpointAddress)
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
    }
}