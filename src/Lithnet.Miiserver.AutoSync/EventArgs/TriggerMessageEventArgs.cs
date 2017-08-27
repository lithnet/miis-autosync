using System;

namespace Lithnet.Miiserver.AutoSync
{
    public class TriggerMessageEventArgs : EventArgs
    {
        public string Details { get; set; }

        public string Message { get; set; }

        public TriggerMessageEventArgs()
        {
        }

        public TriggerMessageEventArgs(string message)
        {
            this.Message = message;
        }

        public TriggerMessageEventArgs(string message, string details)
        {
            this.Details = details;
            this.Message = message;
        }
    }
}
