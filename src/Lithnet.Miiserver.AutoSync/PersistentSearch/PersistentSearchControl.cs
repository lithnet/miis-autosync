using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.DirectoryServices.Protocols;

namespace Lithnet.Miiserver.AutoSync
{
    public class PersistentSearchControl : DirectoryControl
    {
        public PersistentSearchControl()
            : this(new PersistentSearchOptions())
        {
        }

        public PersistentSearchControl(PersistentSearchChangeType changeTypes)
            : this(new PersistentSearchOptions(changeTypes))
        {
        }

        public PersistentSearchControl(PersistentSearchOptions options)
            : base("2.16.840.1.113730.3.4.3", options.GetValue(), true, true)
        {
        }
    }
}