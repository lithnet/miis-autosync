using System;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigServiceConfiguration
    {
        public const string NamedPipeUri = "net.pipe://localhost/lithnet/autosync/config";

        public static ServiceMetadataBehavior ServiceMetadataDisabledBehavior
        {
            get
            {
                return new ServiceMetadataBehavior
                {
                    HttpGetEnabled = false, 
                    HttpsGetEnabled = false
                };
            }
        }

        public static ServiceDebugBehavior ServiceDebugBehavior
        {
            get
            {
                return new ServiceDebugBehavior
                { 
                    IncludeExceptionDetailInFaults = true,
                    HttpHelpPageEnabled = false,
                    HttpsHelpPageEnabled = false
                };
            }
        }

        public static Binding NetNamedPipeBinding
        {
            get
            {
                int timeout = System.Diagnostics.Debugger.IsAttached ? 900 : 10;

                NetNamedPipeBinding binding = new NetNamedPipeBinding();
                binding.MaxReceivedMessageSize = int.MaxValue;
                binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
                binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
                binding.CloseTimeout = TimeSpan.FromSeconds(timeout);
                binding.OpenTimeout = TimeSpan.FromSeconds(timeout);
                binding.ReceiveTimeout = TimeSpan.FromSeconds(timeout);
                binding.SendTimeout = TimeSpan.FromSeconds(timeout);
                binding.TransactionFlow = false;
                binding.Security.Mode = NetNamedPipeSecurityMode.Transport;
                binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.None;
                return binding;
            }
        }

        public static EndpointAddress NetNamedPipeEndpointAddress
        {
            get
            {
                return new EndpointAddress(ConfigServiceConfiguration.NamedPipeUri);
            }
        }
    }
}