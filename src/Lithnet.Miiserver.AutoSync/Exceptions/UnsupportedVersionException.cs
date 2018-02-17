using System;

namespace Lithnet.Miiserver.AutoSync
{
    [Serializable]
    public class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException()
        {
        }

        public UnsupportedVersionException(string message)
            : base(message)
        {
        }

        public UnsupportedVersionException(string message, Exception innerException)
        : base(message, innerException)
        {
        }
    }
}
