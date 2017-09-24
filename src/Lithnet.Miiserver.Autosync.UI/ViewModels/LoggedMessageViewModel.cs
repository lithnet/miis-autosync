using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Lithnet.Common.Presentation;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class LoggedMessageViewModel : ViewModelBase
    {
        private MessageLoggedEventArgs e;

        public LoggedMessageViewModel(MessageLoggedEventArgs e)
        {
            this.e = e;
        }

        public string FormattedDate => this.Date.ToString("g");
        
        public DateTime Date => this.e.Date;

        public string Message => this.e.Message;
    }
}
