using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.DirectoryServices.Protocols;

namespace Lithnet.Miiserver.AutoSync
{
    public class PersistentSearchOptions
    {
        public PersistentSearchChangeType ChangeTypes { get; set; }

        public bool ChangesOnly { get; set; }

        public bool ReturnEntryChangeNotificationControls { get; set; }

        public PersistentSearchOptions()
        {
            this.ChangeTypes = PersistentSearchChangeType.Add | PersistentSearchChangeType.Delete | PersistentSearchChangeType.ModDN | PersistentSearchChangeType.Modify;
            this.ChangesOnly = true;
            this.ReturnEntryChangeNotificationControls = true;
        }

        public PersistentSearchOptions(PersistentSearchChangeType changeTypes)
        {
            this.ChangeTypes = changeTypes;
            this.ChangesOnly = true;
            this.ReturnEntryChangeNotificationControls = true;
        }

        public PersistentSearchOptions(PersistentSearchChangeType changeTypes, bool changesOnly, bool returnEntryChangeNotificationControls)
        {
            this.ChangeTypes = changeTypes;
            this.ChangesOnly = changesOnly;
            this.ReturnEntryChangeNotificationControls = returnEntryChangeNotificationControls;
        }

        internal byte[] GetValue()
        {
            return BerConverter.Encode("{ibb}", (int)this.ChangeTypes, this.ChangesOnly, this.ReturnEntryChangeNotificationControls);
        }
    }
}