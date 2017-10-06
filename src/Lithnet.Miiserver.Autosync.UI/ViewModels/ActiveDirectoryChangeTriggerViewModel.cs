using System;
using System.Collections.ObjectModel;
using Lithnet.Miiserver.AutoSync;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    public class ActiveDirectoryChangeTriggerViewModel : MAExecutionTriggerViewModel
    {
        private static char[] separators = new char[] { ',', ';' };

        private ActiveDirectoryChangeTrigger typedModel;

        private const string PlaceholderPassword = "{5A4A203E-EBB9-4D47-A3D4-CD6055C6B4FF}";

        public ActiveDirectoryChangeTriggerViewModel(ActiveDirectoryChangeTrigger model)
            : base(model)
        {
            this.typedModel = model;
            this.AddIsDirtyProperty(nameof(this.MinimumIntervalBetweenEvents));
            this.AddIsDirtyProperty(nameof(this.LastLogonTimestampOffset));
            this.AddIsDirtyProperty(nameof(this.BaseDN));
            this.AddIsDirtyProperty(nameof(this.UseServiceAccountCredentials));
            this.AddIsDirtyProperty(nameof(this.UseExplicitCredentials));
            this.AddIsDirtyProperty(nameof(this.Username));
            this.AddIsDirtyProperty(nameof(this.Password));
            this.AddIsDirtyProperty(nameof(this.Disabled));
            this.AddIsDirtyProperty(nameof(this.HostName));
            this.AddIsDirtyProperty(nameof(this.ObjectClasses));
        }

        public TimeSpan MinimumIntervalBetweenEvents
        {
            get => this.typedModel.MinimumIntervalBetweenEvents;
            set => this.typedModel.MinimumIntervalBetweenEvents = value;
        }

        public TimeSpan LastLogonTimestampOffset
        {
            get => this.typedModel.LastLogonTimestampOffset;
            set => this.typedModel.LastLogonTimestampOffset = value;
        }

        public string BaseDN
        {
            get => this.typedModel.BaseDN;
            set => this.typedModel.BaseDN = value;
        }

        public bool UseServiceAccountCredentials
        {
            get => !this.UseExplicitCredentials;
            set => this.UseExplicitCredentials = !value;
        }

        public bool UseExplicitCredentials
        {
            get => this.typedModel.UseExplicitCredentials;
            set => this.typedModel.UseExplicitCredentials = value;
        }

        public string Username
        {
            get => this.typedModel.Username;
            set => this.typedModel.Username = value;
        }

        public string Password
        {
            get
            {
                if (this.typedModel.Password == null || !this.typedModel.Password.HasValue)
                {
                    return null;
                }
                else
                {
                    return PlaceholderPassword;
                }
            }
            set
            {
                if (value == null)
                {
                    this.typedModel.Password = null;
                }

                if (value == PlaceholderPassword)
                {
                    return;
                }

                this.typedModel.Password = new ProtectedString(value);
            }
        }

        public string Type => this.Model.Type;

        public string Description => this.Model.Description;

        public bool Disabled
        {
            get => this.typedModel.Disabled;
            set => this.typedModel.Disabled = value;
        }

        [AlsoNotifyFor("Description")]
        public string HostName
        {
            get => this.typedModel.HostName;
            set => this.typedModel.HostName = value;
        }

        public string Name => this.Model.DisplayName;

        public string ObjectClasses
        {
            get
            {
                if (this.typedModel.ObjectClasses?.Length > 0)
                {
                    return string.Join(";", this.typedModel.ObjectClasses);
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.typedModel.ObjectClasses = null;
                }
                else
                {
                    this.typedModel.ObjectClasses = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }

        //internal override void DoAutoConfig(string madata, MAControllerConfigurationViewModel controller)
        //{
        //    XmlDocument d = new XmlDocument();
        //    d.LoadXml(madata);

        //   // XmlNode partitionNode = d.SelectSingleNode("/ma-data/ma-partition-data/partition[selected=1 and custom-data/adma-partition-data[is-domain=1]]");
        //    XmlNodeList allActivePartitions = d.SelectNodes("/ma-data/ma-partition-data/partition[selected=1]");

        //    if (allActivePartitions == null)
        //    {
        //        Trace.WriteLine("There were no active partitions");
        //        return;
        //    }
        
        //    List<XmlNode> unconfiguredPartitions = new List<XmlNode>();
        //    List<ActiveDirectoryChangeTriggerViewModel> otherVMs = controller.Triggers.OfType<ActiveDirectoryChangeTriggerViewModel>().ToList();

        //    foreach (XmlNode partition in allActivePartitions)
        //    {
        //        Guid partitionID;
        //        string partitionString = partition.SelectSingleNode("id")?.InnerText;
        //        if (!Guid.TryParse(partitionString, out partitionID))
        //        {
        //            Trace.WriteLine($"Management agent partition ID could not be parsed: {partitionString}");
        //            continue;
        //        }

        //        if (otherVMs.Any(t => t.PartitionID == partitionID))
        //        {
        //            Trace.WriteLine($"Management agent partition already has a trigger: {partitionString}");
        //            continue;
        //        }

        //        Trace.WriteLine($"Found unconfigured trigger: {partitionString}");
        //        unconfiguredPartitions.Add(partition);
        //    }

        //    if (unconfiguredPartitions.Count == 0)
        //    {
        //        Trace.WriteLine($"No unconfigured triggers found");
        //        return;
        //    }

        //    XmlNode primaryPartition = unconfiguredPartitions.FirstOrDefault(t => t.SelectSingleNode("custom-data/adma-partition-data/is-domain")?.InnerText == "1");

        //    if (primaryPartition != null)
        //    {
        //        this.DoAutoConfig(primaryPartition);
        //    }
        //    else
        //    {
        //        this.DoAutoConfig(unconfiguredPartitions.First());
        //    }

        //}

        //private void DoAutoConfig(XmlNode partition)
        //{
        //    this.HostName = partition.SelectSingleNode("custom-data/adma-partition-data/name")?.InnerText;
        //    this.BaseDN = partition.SelectSingleNode("custom-data/adma-partition-data/dn")?.InnerText;
        //    this.PartitionName = partition.SelectSingleNode("name")?.InnerText;

        //    Guid partitionID;
        //    string partitionString = partition.SelectSingleNode("id")?.InnerText;
        //    if (Guid.TryParse(partitionString, out partitionID))
        //    {
        //        this.PartitionID = partitionID;
        //    }

        //    string[] objectClasses = partition?.SelectNodes("filter/object-classes/object-class")?.OfType<XmlElement>().Where(t => t.InnerText != "container" && t.InnerText != "domainDNS" && t.InnerText != "organizationalUnit").Select(u => u.InnerText).ToArray();

        //    if (objectClasses != null)
        //    {
        //        this.ObjectClasses = string.Join(";", objectClasses);
        //    }

        //    this.LastLogonTimestampOffset = new TimeSpan(0, 5, 0);
        //    this.MinimumIntervalBetweenEvents = new TimeSpan(0, 1, 0);
        //    this.UseExplicitCredentials = false;
        //}
    }
}
