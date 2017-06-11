using System.ServiceProcess;

namespace Lithnet.Miiserver.AutoSync
{
    public partial class AutoSyncService : ServiceBase
    {
        internal static AutoSyncService ServiceInstance { get; private set; }

        public AutoSyncService()
        {
            this.InitializeComponent();
            AutoSyncService.ServiceInstance = this;
        }

        protected override void OnStart(string[] args)
        {
            Program.Start();
        }

        protected override void OnStop()
        {
            Program.Stop();
        }

        public static void StopInstance()
        {
            AutoSyncService.ServiceInstance.Stop();
        }
    }
}
