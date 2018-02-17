using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync.Setup.CustomActions
{
    internal enum ComputerNameFormat
    {
        ComputerNameNetBios,
        ComputerNameDnsHostname,
        ComputerNameDnsDomain,
        ComputerNameDnsFullyQualified,
        ComputerNamePhysicalNetBios,
        ComputerNamePhysicalDnsHostname,
        ComputerNamePhysicalDnsDomain,
        ComputerNamePhysicalDnsFullyQualified
    }
}
