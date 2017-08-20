using System;

namespace Lithnet.Miiserver.AutoSync
{
    [Serializable]
    public class SyncEngineStoppedException : Exception
    {
        public SyncEngineStoppedException()
        {
        }

        public SyncEngineStoppedException(string message)
            : base(message)
        {
        }

        public SyncEngineStoppedException(string message, Exception innerException)
        : base(message, innerException)
        {
        }
    }
}
