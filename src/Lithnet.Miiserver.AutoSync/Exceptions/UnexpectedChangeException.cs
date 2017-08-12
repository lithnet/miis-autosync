using System;

namespace Lithnet.Miiserver.AutoSync
{
    [Serializable]
    public class UnexpectedChangeException : Exception
    {
        public bool ShouldTerminateService { get; set; }

        public UnexpectedChangeException()
        {
        }

        public UnexpectedChangeException(bool shouldTerminateService)
        {
            this.ShouldTerminateService = shouldTerminateService;
        }

        public UnexpectedChangeException(bool shouldTerminateService, string message)
            :base(message)
        {
            this.ShouldTerminateService = shouldTerminateService;
        }

        public UnexpectedChangeException(string message)
            : base(message)
        {
        }

        public UnexpectedChangeException(string message, Exception innerException)
        : base(message, innerException)
        {
        }
    }
}
