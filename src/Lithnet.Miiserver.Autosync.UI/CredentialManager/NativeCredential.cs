using System;
using System.Runtime.InteropServices;
using Com=System.Runtime.InteropServices.ComTypes;

namespace Lithnet.Miiserver.AutoSync.UI
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeCredential
    {
        public uint Flags;
        public CredentialType Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public Com.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CredentialPersistence Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
