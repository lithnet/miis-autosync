using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Lithnet.Miiserver.AutoSync.UI
{
    internal static class CredentialManager
    {
        public static bool TryDelete(string targetName, CredentialType type)
        {
            return CredentialManager.CredDelete(targetName, type, 0);
        }

        public static void Delete(string targetName, CredentialType type)
        {
            if (!CredentialManager.CredDelete(targetName, type, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public static ManagedCredential Read(string targetName, CredentialType type)
        {
            IntPtr pCreds = IntPtr.Zero;

            try
            {
                if (!CredRead(targetName, type, 0, out pCreds))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                NativeCredential creds = Marshal.PtrToStructure<NativeCredential>(pCreds);

                return ManagedCredential.FromNativeCredential(creds);
            }
            finally
            {
                if (pCreds != IntPtr.Zero)
                {
                    CredFree(pCreds);
                }
            }
        }

        public static void Write(string targetName, CredentialType type, CredentialPersistence persistence, string username, string password)
        {
            byte[] passwordBytes = null;

            if (password != null)
            {
                passwordBytes = Encoding.Unicode.GetBytes(password);
            }

            if (passwordBytes != null)
            {
                if (Environment.OSVersion.Version < new Version(6, 1))
                {
                    if (passwordBytes.Length > 512)
                    {
                        throw new ArgumentOutOfRangeException(nameof(password));
                    }
                }
                else
                {
                    if (passwordBytes.Length > (512 * 5))
                    {
                        throw new ArgumentOutOfRangeException(nameof(password));
                    }
                }
            }

            NativeCredential credential = new NativeCredential();

            try
            {
                credential.AttributeCount = 0;
                credential.Attributes = IntPtr.Zero;
                credential.Comment = IntPtr.Zero;
                credential.TargetAlias = IntPtr.Zero;
                credential.Type = type;
                credential.Persist = persistence;
                credential.CredentialBlobSize = (uint)(passwordBytes?.Length ?? 0);
                credential.TargetName = Marshal.StringToCoTaskMemUni(targetName);
                credential.CredentialBlob = Marshal.StringToCoTaskMemUni(password);
                credential.UserName = Marshal.StringToCoTaskMemUni(username ?? Environment.UserName);

                if (!CredWrite(ref credential, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                if (credential.TargetName != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(credential.TargetName);
                }

                if (credential.CredentialBlob != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(credential.CredentialBlob);
                }

                if (credential.UserName != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(credential.UserName);
                }
            }
        }

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, CredentialType type, int reservedFlag);

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref NativeCredential userCredential, [In] UInt32 flags);

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredEnumerate(string filter, int flag, out int count, out IntPtr pCredentials);

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredFree([In] IntPtr cred);
    }
}