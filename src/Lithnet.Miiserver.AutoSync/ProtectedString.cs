using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Lithnet.Miiserver.AutoSync
{
    public class ProtectedString : IXmlSerializable
    {
        private byte[] Salt { get; set; }

        public SecureString Value { get; set; }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (reader.MoveToContent() == XmlNodeType.Element)
            {
                string e = reader["is-encrypted"];
                string s = reader["salt"];

                string data = reader.ReadElementContentAsString();
                
                if (string.IsNullOrEmpty(data))
                {
                    return;
                }

                bool encrypted;

                if (!bool.TryParse(e, out encrypted))
                {
                    encrypted = false;
                }

                if (encrypted)
                {
                    byte[] dataBytes = Convert.FromBase64String(data);

                    byte[] salt = null;
                    if (s != null)
                    {
                        salt = Convert.FromBase64String(s);
                    }

                    string d = this.UnprotectData(dataBytes, salt);
                    this.Value = d.ToSecureString();
                }
                else
                {
                    this.Value = data.ToSecureString();
                }
            }
        }
        
        private static byte[] GenerateSalt(int maximumSaltLength)
        {
            byte[] salt = new byte[maximumSaltLength];
            using (RNGCryptoServiceProvider random = new RNGCryptoServiceProvider())
            {
                random.GetNonZeroBytes(salt);
            }

            return salt;
        }

        private string ProtectData(byte[] salt)
        {
            if (this.Value == null)
            {
                return null;
            }

            byte[] p = ProtectedData.Protect(Encoding.Unicode.GetBytes(this.Value.ToUnsecureString()), salt, DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(p);
        }

        private string UnprotectData(byte[] data, byte[] salt)
        {
            byte[] p = ProtectedData.Unprotect(data, salt, DataProtectionScope.CurrentUser);

            return Encoding.Unicode.GetString(p);
        }

        public void WriteXml(XmlWriter writer)
        {
            if (this.Salt == null)
            {
                this.Salt = GenerateSalt(32);
            }

            writer.WriteAttributeString("salt", Convert.ToBase64String(this.Salt));
            writer.WriteAttributeString("is-encrypted", "true");
            writer.WriteString(this.ProtectData(this.Salt));
        }
    }
}
