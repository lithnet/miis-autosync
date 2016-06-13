using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    public static class MessageSender
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
    }
}
