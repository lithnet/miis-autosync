using System;
using System.Collections;

namespace Lithnet.Miiserver.AutoSync
{
    using System.Text;

    public class MAConfigParameters
    {
        public string ConfirmingImportRunProfileName { get; set; }

        public string DeltaSyncRunProfileName { get; set; }

        public string FullSyncRunProfileName { get; set; }

        public string FullImportRunProfileName { get; set; }

        public string ScheduledImportRunProfileName { get; set; }

        public string DeltaImportRunProfileName { get; set; }

        public string ExportRunProfileName { get; set; }

        public bool Disabled { get; set; }

        public AutoImportScheduling AutoImportScheduling { get; set; }

        public bool DisableDefaultTriggers { get; set; }

        public int AutoImportIntervalMinutes { get; set; }

        public MAConfigParameters()
        {
        }

        public MAConfigParameters(Hashtable config)
        {
            this.ConfirmingImportRunProfileName = config["ConfirmingImportRunProfileName"] as string;
            this.DeltaSyncRunProfileName = config["DeltaSyncRunProfileName"] as string;
            this.FullSyncRunProfileName = config["FullSyncRunProfileName"] as string;
            this.FullImportRunProfileName = config["FullImportRunProfileName"] as string;
            this.ScheduledImportRunProfileName = config["ScheduledImportRunProfileName"] as string;
            this.DeltaImportRunProfileName = config["DeltaImportRunProfileName"] as string;
            this.ExportRunProfileName = config["ExportRunProfileName"] as string;
            this.Disabled = config["Disabled"] != null && Convert.ToBoolean(config["Disabled"]);
            this.AutoImportScheduling = config["AutoImportScheduling"] == null ? AutoImportScheduling.Default : (AutoImportScheduling)Enum.Parse(typeof(AutoImportScheduling), config["AutoImportScheduling"].ToString(), true);
            this.DisableDefaultTriggers = config["DisableDefaultTriggers"] != null && Convert.ToBoolean(config["DisableDefaultTriggers"]);
            this.AutoImportIntervalMinutes = config["AutoImportIntervalMinutes"] == null ? 0 : Convert.ToInt32(config["AutoImportIntervalMinutes"]);
        }

        internal bool CanExport => this.ExportRunProfileName != null;

        internal bool CanImport => this.ScheduledImportRunProfileName != null || this.FullImportRunProfileName != null;

        internal bool CanAutoRun => this.DeltaSyncRunProfileName != null ||
                                    this.ConfirmingImportRunProfileName != null ||
                                    this.FullSyncRunProfileName != null ||
                                    this.FullImportRunProfileName != null ||
                                    this.ScheduledImportRunProfileName != null ||
                                    this.ExportRunProfileName != null;

        internal string GetRunProfileName(MARunProfileType type)
        {
            switch (type)
            {
                case MARunProfileType.DeltaImport:
                    return this.ScheduledImportRunProfileName;

                case MARunProfileType.FullImport:
                    return this.FullImportRunProfileName;

                case MARunProfileType.Export:
                    return this.ExportRunProfileName;

                case MARunProfileType.DeltaSync:
                    return this.DeltaSyncRunProfileName;

                case MARunProfileType.FullSync:
                    return this.FullSyncRunProfileName;

                default:
                case MARunProfileType.None:
                    throw new ArgumentException("Unknown run profile type");
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            if (this.Disabled)
            {
                builder.AppendLine($"Disabled: {this.Disabled}");
                return builder.ToString();
            }

            builder.AppendLine($"{nameof(this.ConfirmingImportRunProfileName)}: {this.ConfirmingImportRunProfileName}");
            builder.AppendLine($"{nameof(this.DeltaImportRunProfileName)}: {this.DeltaImportRunProfileName}");
            builder.AppendLine($"{nameof(this.FullImportRunProfileName)}: {this.FullImportRunProfileName}");
            builder.AppendLine($"{nameof(this.DeltaSyncRunProfileName)}: {this.DeltaSyncRunProfileName}");
            builder.AppendLine($"{nameof(this.FullSyncRunProfileName)}: {this.FullSyncRunProfileName}");
            builder.AppendLine($"{nameof(this.ExportRunProfileName)}: {this.ExportRunProfileName}");
            builder.AppendLine($"{nameof(this.ScheduledImportRunProfileName)}: {this.ScheduledImportRunProfileName}");
            builder.AppendLine($"{nameof(this.AutoImportScheduling)}: {this.AutoImportScheduling}");
            builder.AppendLine($"{nameof(this.DisableDefaultTriggers)}: {this.DisableDefaultTriggers}");
            builder.AppendLine($"{nameof(this.AutoImportIntervalMinutes)}: {this.AutoImportIntervalMinutes}");
            builder.AppendLine("--- Capabilities ---");
            builder.AppendLine($"{nameof(this.CanExport)}: {this.CanExport}");
            builder.AppendLine($"{nameof(this.CanImport)}: {this.CanImport}");
            builder.AppendLine($"{nameof(this.CanAutoRun)}: {this.CanAutoRun}");

            return builder.ToString();
        }
    }
}
