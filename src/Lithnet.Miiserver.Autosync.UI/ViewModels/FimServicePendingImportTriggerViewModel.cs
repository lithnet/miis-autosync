using System;
using System.Diagnostics;
using System.Net;
using System.Windows;
using Lithnet.Miiserver.AutoSync;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using Microsoft.Win32;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class FimServicePendingImportTriggerViewModel : MAExecutionTriggerViewModel
    {
        private FimServicePendingImportTrigger typedModel;

        public FimServicePendingImportTriggerViewModel(FimServicePendingImportTrigger model)
            : base(model)
        {
            this.typedModel = model;
            this.AddIsDirtyProperty(nameof(this.HostName));
            this.AddIsDirtyProperty(nameof(this.Interval));
            this.Commands.AddItem("CreateMPR", x => this.CreateMpr());

        }

        public string Type => this.Model.Type;

        public string Description => this.Model.Description;

        [AlsoNotifyFor("Description")]
        public string HostName
        {
            get => this.typedModel.HostName;
            set => this.typedModel.HostName = value;
        }

        public TimeSpan Interval
        {
            get => this.typedModel.Interval;
            set => this.typedModel.Interval = value;
        }

        public string MimServiceHost { get; set; }

        public string MprName { get; set; }

        public string SetName { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }


        private void CreateMpr()
        {
            try
            {
                CreateMprWindow window = new CreateMprWindow();
                this.MimServiceHost = this.MimServiceHost ?? this.HostName;
                this.MprName = this.MprName ?? "_AutoSync service account can read msidmCompletedTime attribute on Request objects";
                this.SetName = this.SetName ?? "_AutoSync service account";

                window.DataContext = this;

                if (window.ShowDialog() == false)
                {
                    return;
                }

                NetworkCredential creds = null;

                if (this.Username != null)
                {
                    creds = new NetworkCredential(this.Username, this.Password);
                }

                ConfigClient c = App.GetDefaultConfigClient();
                string svcAccount = c.InvokeThenClose(t => t.GetAutoSyncServiceAccountName());

                FimServicePendingImportTrigger.CreateMpr(this.MimServiceHost, creds, svcAccount, this.SetName, this.MprName);
                MessageBox.Show("The set and MPR were created successfully", "Lithnet AutoSync", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"An error occurred while trying to grant the required permissions. If the problem persists, you can create the MPR manually by granting the AutoSync service account permission to read the msidmCompletedTime attribute on all request objects\n\nError message: {ex.Message}", "Unable to create the MPR");
            }
        }
    }
}
