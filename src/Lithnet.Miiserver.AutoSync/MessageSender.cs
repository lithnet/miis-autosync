using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class MessageSender
    {
        public static void SendMessage(string subject, string body)
        {
            using (MailMessage m = new MailMessage())
            {
                foreach (string address in Settings.MailTo)
                {
                    m.To.Add(address);
                }

                if (!Settings.UseAppConfigMailSettings)
                {
                    m.From = new MailAddress(Settings.MailFrom);
                }

                m.Subject = subject;
                m.IsBodyHtml = true;
                m.Body = body;

                using (SmtpClient client = new SmtpClient())
                {
                    if (!Settings.UseAppConfigMailSettings)
                    {
                        client.Host = Settings.MailServer;
                        client.Port = Settings.MailServerPort;
                    }

                    client.Send(m);
                }
            }
        }

        internal static bool CanSendMail()
        {
            if (!Settings.MailEnabled)
            {
                return false;
            }

            if (!Settings.UseAppConfigMailSettings)
            {
                if (Settings.MailFrom == null || Settings.MailTo == null || Settings.MailServer == null)
                {
                    return false;
                }
            }
            else
            {
                if (Settings.MailTo == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
