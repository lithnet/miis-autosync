using System.Net.Mail;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class MessageSender
    {
        public static void SendMessage(string subject, string body)
        {
            using (MailMessage m = new MailMessage())
            {
                foreach (string address in RegistrySettings.MailTo)
                {
                    m.To.Add(address);
                }

                if (!RegistrySettings.UseAppConfigMailSettings)
                {
                    m.From = new MailAddress(RegistrySettings.MailFrom);
                }
                m.Subject = subject;
                m.IsBodyHtml = true;
                m.Body = body;

                using (SmtpClient client = new SmtpClient())
                {
                    if (!RegistrySettings.UseAppConfigMailSettings)
                    {
                        client.Host = RegistrySettings.MailServer;
                        client.Port = RegistrySettings.MailServerPort;
                    }

                    client.Send(m);
                }
            }
        }

        internal static bool CanSendMail()
        {
            if (!RegistrySettings.MailEnabled)
            {
                return false;
            }

            if (!RegistrySettings.UseAppConfigMailSettings)
            {
                if (RegistrySettings.MailFrom == null || RegistrySettings.MailTo == null || RegistrySettings.MailServer == null)
                {
                    return false;
                }
            }
            else
            {
                if (RegistrySettings.MailTo == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
