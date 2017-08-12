using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract]
    public class MAStatus
    {
        [DataMember]
        public string MAName { get; set; }

        [DataMember]
        public string ExecutingRunProfile { get; set; }

        [DataMember]
        public string ExecutionQueue { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string LastRunProfileResult { get; set; }

        [DataMember]
        public string LastRunProfileName { get; set; }

        [DataMember]
        public ExecutorState State { get; set; }
    }
}
