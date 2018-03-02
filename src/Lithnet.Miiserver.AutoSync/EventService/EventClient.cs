using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Lithnet.Miiserver.Client;

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

        public bool Ping(Guid managementAgentID)
        {
            return this.Channel.Ping(managementAgentID);
        }

        public string GetRunDetail(Guid managementAgentID, int runNumber)
        {
            return this.Channel.GetRunDetail(managementAgentID, runNumber);
        }

        public IEnumerable<CSObjectRef> GetStepDetail(Guid stepID, string statisticsType)
        {
            return this.Channel.GetStepDetail(stepID, statisticsType);
        }

        public static EventClient GetNamedPipesClient(InstanceContext ctx)
        {
            return new EventClient(ctx, EventServiceConfiguration.NetNamedPipeBinding, EventServiceConfiguration.NetNamedPipeEndpointAddress);
        }

        public static EventClient GetNetTcpClient(InstanceContext ctx, string hostname, int port, string expectedServerIdentityFormat)
        {
            return new EventClient(ctx, EventServiceConfiguration.NetTcpBinding, EventServiceConfiguration.CreateTcpEndPointAddress(hostname, port, expectedServerIdentityFormat));
        }
    }
}