using System;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    [Serializable]
    public class ThresholdExceededException : Exception
    {
        public RunDetails RunDetails { get; }

        public ThresholdExceededException()
        {
        }

        public ThresholdExceededException(string message, RunDetails r)
            : base(message)
        {
            this.RunDetails = r;
        }

        public ThresholdExceededException(string message, RunDetails r, Exception innerException)
        : base(message, innerException)
        {
            this.RunDetails = r;
        }
    }
}
