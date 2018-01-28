using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.ServiceModel;
using System.Windows;
using System.Windows.Input;
using Lithnet.Common.Presentation;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ConnectDialogViewModel : ViewModelBase
    {
        public ConnectDialogViewModel()
        {
        }

        public bool AutoConnect { get; set; }

        public string HostnameRaw { get; set; }

        public string Hostname
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.HostnameRaw))
                {
                    return "localhost";
                }

                string[] split = this.HostnameRaw.Split(':');

                return split[0];
            }
        }

        public int Port
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.HostnameRaw))
                {
                    return 54338;
                }

                string[] split = this.HostnameRaw.Split(':');

                if (split.Length == 2)
                {
                    if (int.TryParse(split[1], out int result))
                    {
                        return result;
                    }
                }

                return 54338;
            }
        }

        public Cursor Cursor { get; set; }

        public bool IsEnabled { get; set; } = true;

        internal bool ValidateConnection()
        {
            return this.TryConnect(this.Hostname, this.Port);
        }

        private bool TryConnect(string hostname, int port)
        {
            try
            {
                this.Cursor = Cursors.Wait;
                this.IsEnabled = false;
                return App.TryConnectionWithDialog(hostname, port);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                this.IsEnabled = true;
            }
        }
    }
}