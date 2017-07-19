using System.Net.Mail;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class MessageSender
    {
        private static Settings MailSettings => Program.ActiveConfig.Settings;

        public static void SendMessage(string subject, string body)
        {
            using (MailMessage m = new MailMessage())
            {
                foreach (string address in MailSettings.MailTo)
                {
                    m.To.Add(address);
                }

                if (!MailSettings.MailUseAppConfig)
                {
                    m.From = new MailAddress(MailSettings.MailFrom);
                }

                m.Subject = subject;
                m.IsBodyHtml = true;
                m.Body = body;

                using (SmtpClient client = new SmtpClient())
                {
                    if (!MailSettings.MailUseAppConfig)
                    {
                        client.Host = MailSettings.MailHost;
                        client.Port = MailSettings.MailPort;
                        client.UseDefaultCredentials = MailSettings.MailUseDefaultCredentials;
                        client.EnableSsl = MailSettings.MailUseSsl;
                       
                        if (!string.IsNullOrEmpty(MailSettings.MailUsername))
                        {
                            client.Credentials = new System.Net.NetworkCredential(MailSettings.MailUsername, MailSettings.MailPassword?.Value);
                        }
                    }

                    client.Send(m);
                }
            }
        }

        internal static bool CanSendMail()
        {
            if (!MailSettings.MailEnabled)
            {
                return false;
            }

            if (!MailSettings.MailUseAppConfig)
            {
                if (MailSettings.MailFrom == null || MailSettings.MailTo == null || MailSettings.MailHost == null)
                {
                    return false;
                }
            }
            else
            {
                if (MailSettings.MailTo == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
