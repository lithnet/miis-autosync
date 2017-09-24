using System;
using System.Runtime.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class MessageLoggedEventArgs : EventArgs
    {
        internal MessageLoggedEventArgs(DateTime date, string message)
        {
            this.Message = message;
            this.Date = date;
        }

        [DataMember]
        public string Message { get; private set; }

        [DataMember]
        public DateTime Date { get; private set; }
    }
}
