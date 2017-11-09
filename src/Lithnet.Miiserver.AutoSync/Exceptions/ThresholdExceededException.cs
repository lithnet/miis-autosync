using System;

namespace Lithnet.Miiserver.AutoSync
{
    [Serializable]
    public class ThresholdExceededException : Exception
    {
        public ThresholdExceededException()
        {
        }
      
        public ThresholdExceededException(string message)
            : base(message)
        {
        }

        public ThresholdExceededException(string message, Exception innerException)
        : base(message, innerException)
        {
        }
    }
}
