using System;
using System.Net;

namespace Lithnet.Miiserver.AutoSync
{
    public class ADListenerConfiguration
    {
        public string BaseDN { get; set; }

        public string[] ObjectClasses { get; set; }

        public NetworkCredential Credentials { get; set; }

        public string HostName { get; set; }

        public int LastLogonTimestampOffsetSeconds { get; set; }

        public bool Disabled { get; set; }

        public ADListenerConfiguration()
        {
            this.LastLogonTimestampOffsetSeconds = 60;
            this.MinimumTriggerIntervalSeconds = 60;
        }

        public int MinimumTriggerIntervalSeconds { get; set; }

        internal void Validate()
        {
            if (this.Disabled)
            {
                return;
            }

            if (this.MinimumTriggerIntervalSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(this.MinimumTriggerIntervalSeconds), "The MinimumTriggerIntervalSeconds parameter must be greater than 0");
            }

            if (string.IsNullOrWhiteSpace(this.BaseDN))
            {
                throw new ArgumentNullException(nameof(this.BaseDN), "A BaseDN must be specified");
            }

            if (string.IsNullOrWhiteSpace(this.HostName))
            {
                throw new ArgumentNullException(nameof(this.HostName), "A host name must be specified");
            }

            if (this.ObjectClasses == null || this.ObjectClasses.Length == 0)
            {
                throw new ArgumentNullException(nameof(this.ObjectClasses), "One or more object classes must be specified");
            }
        }
    }
}
