using System;
using System.ComponentModel;
using System.Text;
using System.Runtime.InteropServices;

namespace Lithnet.Miiserver.AutoSync.Setup.CustomActions
{
    internal static class SpnInterop
    {
        #region Native functions

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetComputerNameEx(ComputerNameFormat nameType, [Out] StringBuilder lpBuffer, ref int lpnSize);

        [DllImport("ntdsapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DsGetSpn
        (
            SpnNameType serviceType,
            string serviceClass,
            string serviceName,
            ushort instancePort,
            ushort cInstanceNames,
            string[] pInstanceNames,
            ushort[] pInstancePorts,
            ref uint spnCount,
            ref IntPtr spnArrayPointer
        );

        
        [DllImport("ntdsapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DsWriteAccountSpn
        (
            IntPtr hds,
            SpnWriteOperationType operation,
            string pszAccount,
            uint cSpn,
            IntPtr pSpn
        );

        [DllImport("ntdsapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DsBind
        (
            string domainControllerName,
            string dnsDomainName,
            out IntPtr hds
        );

        [DllImport("ntdsapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DsUnBind
        (
            IntPtr hds
        );

        [DllImport("ntdsapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DsFreeSpnArray
        (
            uint count,
            IntPtr pSpnArray
        );

        #endregion

        public static string GetComputerDnsName(ComputerNameFormat format)
        {
            int size = 0;

            if (GetComputerNameEx(format, null, ref size))
            {
                throw new InvalidOperationException("GetComputerNameEx should have failed");
            }

            int result = Marshal.GetLastWin32Error();

            if (result != 234) // ERROR_MORE_DATA
            {
                throw new Win32Exception(result);
            }

            StringBuilder name = new StringBuilder(size);

            if (!GetComputerNameEx(format, name, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return name.ToString();
        }

        public static void SetSpn(string serviceClassName, string[] hostnames, string principalDN)
        {
            IntPtr hdirectoryService = IntPtr.Zero;
            IntPtr pSpnArray = IntPtr.Zero;

            uint spnArrayCount = 0;

            try
            {
                // Get a handle to the GC
                int result = SpnInterop.DsBind(null, null, out hdirectoryService);

                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                // Create the SPN object and get a pointer to its location
                result = DsGetSpn(SpnNameType.DnsHost, serviceClassName, null, 0, (ushort)hostnames.Length, hostnames, null, ref spnArrayCount, ref pSpnArray);

                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                // Write the SPN to the object by passing in its pointer
                result = DsWriteAccountSpn(hdirectoryService, SpnWriteOperationType.AddSpn, principalDN, spnArrayCount, pSpnArray);

                if (result != 0)
                {
                    throw new Win32Exception(result);
                }
            }
            finally
            {
                if (hdirectoryService != IntPtr.Zero)
                {
                    DsUnBind(hdirectoryService);
                }

                if (pSpnArray != IntPtr.Zero)
                {
                    DsFreeSpnArray(spnArrayCount, pSpnArray);
                }
            }
        }
    }
}