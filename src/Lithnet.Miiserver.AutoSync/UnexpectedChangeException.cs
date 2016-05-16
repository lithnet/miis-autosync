using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    public class UnexpectedChangeException : Exception
    {
        public bool ShouldTerminateService { get; set; }

        public UnexpectedChangeException()
        : base()
        {
        }

        public UnexpectedChangeException(bool shouldTerminateService)
        : base()
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
