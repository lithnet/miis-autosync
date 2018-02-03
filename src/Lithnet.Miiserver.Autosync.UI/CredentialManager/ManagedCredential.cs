using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync.UI
{
    internal class ManagedCredential
    {
        public CredentialType CredentialType { get; }

        public string TargetName { get; }

        public string UserName { get; }

        public string Password { get; }

        public ManagedCredential(CredentialType credentialType, string targetName, string username, string password)
        {
            this.TargetName = targetName;
            this.UserName = username;
            this.Password = password;
            this.CredentialType = credentialType;
        }

        public static ManagedCredential FromNativeCredential(NativeCredential credential)
        {
            string applicationName = Marshal.PtrToStringUni(credential.TargetName);
            string username = Marshal.PtrToStringUni(credential.UserName);
            string password = null;

            if (credential.CredentialBlob != IntPtr.Zero)
            {
                password = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
            }

            return new ManagedCredential(credential.Type, applicationName, username, password);
        }
    }
}
