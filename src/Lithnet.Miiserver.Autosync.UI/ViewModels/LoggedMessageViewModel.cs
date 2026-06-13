using System;
using Lithnet.Common.Presentation;

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
