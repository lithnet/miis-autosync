using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Common.Presentation;

namespace Lithnet.Miiserver.Autosync.UI
{
    internal class MainWindowViewModel : ViewModelBase
    {

        public ConfigFileViewModel ConfigFile { get; set; }
    }
}
