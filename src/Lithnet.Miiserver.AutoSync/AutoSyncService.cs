using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace Lithnet.Miiserver.AutoSync
{
    public partial class AutoSyncService : ServiceBase
    {
        internal static AutoSyncService ServiceInstance { get; private set;}

        public AutoSyncService()
        {
            InitializeComponent();
            AutoSyncService.ServiceInstance = this;
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }

        public static void StopInstance()
        {
            AutoSyncService.ServiceInstance.Stop();
        }
    }
}
