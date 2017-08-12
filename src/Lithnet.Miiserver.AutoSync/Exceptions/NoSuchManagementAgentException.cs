using System;

namespace Lithnet.Miiserver.AutoSync
{
    [Serializable]
    public class NoSuchManagementAgentException : Exception
    {
        public NoSuchManagementAgentException()
        {
        }

        public NoSuchManagementAgentException(string message)
            : base(message)
        {
        }

        public NoSuchManagementAgentException(string message, Exception innerException)
        : base(message, innerException)
        {
        }
    }
}
