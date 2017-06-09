using System;
using System.Linq;
using System.Text;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Extensions
    {
        public static void ThrowOnPipelineError(this PowerShell powershell)
        {
            if (!powershell.HadErrors)
            {
                return;
            }

            Logger.WriteLine("The PowerShell script encountered an error");

            StringBuilder b = new StringBuilder();

            foreach (ErrorRecord error in powershell.Streams.Error)
            {
                if (error.ErrorDetails != null)
                {
                    b.AppendLine(error.ErrorDetails.Message);
                    b.AppendLine(error.ErrorDetails.RecommendedAction);
                }

                b.AppendLine(error.ScriptStackTrace);

                if (error.Exception != null)
                {
                    Logger.WriteException(error.Exception);
                    b.AppendLine(error.Exception.ToString());
                }
            }

            Logger.WriteLine(b.ToString());

            throw new ApplicationException("The PowerShell script encountered an error\n" + b.ToString());
        }

        public static SecureString ToSecureString(this string s)
        {
            if (s == null)
            {
                return null;
            }

            SecureString sec = new SecureString();

            Array.ForEach(s.ToArray(), sec.AppendChar);

            return sec;
        }

        public static string ToUnsecureString(this SecureString s)
        {
            IntPtr valuePtr = IntPtr.Zero;

            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(s);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }
    }
}