using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    [DataContract(Name = "settings")]
    public class Settings
    {
        [DataMember(Name = "run-history-max-age")]
        public TimeSpan RunHistoryAge { get; set; }

        [DataMember(Name = "run-history-save-path")]
        public string RunHistoryPath { get; set; }

        [DataMember(Name = "run-history-save")]
        public bool RunHistorySave { get; set; }

        [DataMember(Name = "run-history-clear")]
        public bool RunHistoryClear { get; set; }

        [DataMember(Name = "mail-enabled")]
        public bool MailEnabled { get; set; }

        [DataMember(Name = "mail-use-app-config")]
        public bool MailUseAppConfig { get; set; }

        [DataMember(Name = "mail-host")]
        public string MailHost { get; set; }

        [DataMember(Name = "mail-port")]
        public int MailPort { get; set; }

        [DataMember(Name = "mail-use-ssl")]
        public bool MailUseSsl { get; set; }

        [DataMember(Name = "mail-use-default-credentials")]
        public bool MailUseDefaultCredentials { get; set; }

        [DataMember(Name = "mail-username")]
        public string MailUsername { get; set; }

        [DataMember(Name = "mail-password")]
        public ProtectedString MailPassword { get; set; }

        [DataMember(Name = "mail-to")]
        public HashSet<string> MailTo { get; set; }

        [DataMember(Name = "mail-from")]
        public string MailFrom { get; set; }

        [DataMember(Name = "mail-max-errors")]
        public int MailMaxErrors { get; set; }

        [DataMember(Name = "mail-send-all-error-instances")]
        public bool MailSendAllErrorInstances { get; set; }

        [DataMember(Name = "mail-ignore-return-codes")]
        public HashSet<string> MailIgnoreReturnCodes { get; set; }
    }
}
