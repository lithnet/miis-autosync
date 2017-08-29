using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Lithnet.Miiserver.Client;
using PreMailer.Net;

namespace Lithnet.Miiserver.AutoSync
{
    public static class MessageBuilder
    {
        private const string SimpleRow = "<tr><td>{0}</td><td>{1}</td></tr>";
        private const string FirstRow = "<tr>{0}<td>{1}</td><td>{2}</td></tr>";
        private const string TwoColumnHeader = "<td rowspan=\"{0}\" valign=\"top\">{1}</td><td rowspan=\"{0}\" colspan=\"2\" valign=\"top\">{2}</td>";
        private const string ThreeColumnHeader = "<td rowspan=\"{0}\" valign=\"top\">{1}</td><td rowspan=\"{0}\" valign=\"top\">{2}</td><td rowspan=\"{0}\" valign=\"top\">{3}</td>";

        private static string GetTemplate(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = $"Lithnet.Miiserver.AutoSync.Resources.{name}.html";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"The resource {resourceName} was missing from the assembly");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static string GetMessageBody(string maName, string triggerType, string triggerDetails, DateTime errorTime, bool hasTerminated, Exception ex)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat(MessageBuilder.GetTemplate("TriggerErrorFragment"), maName, triggerType, triggerDetails, errorTime, hasTerminated ? "Yes" : "No", ex);
            
            InlineResult result = PreMailer.Net.PreMailer.MoveCssInline(MessageBuilder.GetTemplate("EmailTemplate").Replace("%BODY%", builder.ToString()));

            return result.Html;
        }

        public static string GetMessageBody(RunDetails r)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendFormat(MessageBuilder.GetTemplate("RunSummaryFragment"), r.RunProfileName, r.MAName, r.StartTime, r.EndTime, r.SecurityID, r.LastStepStatus, r.RunNumber);

            builder.AppendFormat(MessageBuilder.GetTemplate("StepTableFragment"), MessageBuilder.BuildStepDetails(r.StepDetails));

            string stagingErrors = MessageBuilder.BuildStagingErrorDetails(r.StepDetails);

            if (stagingErrors != null)
            {
                builder.AppendLine(stagingErrors);
            }

            string importErrors = MessageBuilder.BuildImportErrorDetails(r.StepDetails);

            if (importErrors != null)
            {
                builder.AppendLine(importErrors);
            }

            string exportErrors = MessageBuilder.BuildExportErrorDetails(r.StepDetails);

            if (exportErrors != null)
            {
                builder.AppendLine(exportErrors);
            }

            string syncErrors = MessageBuilder.BuildSyncErrorDetails(r.StepDetails);

            if (syncErrors != null)
            {
                builder.AppendLine(syncErrors);
            }


            InlineResult result = PreMailer.Net.PreMailer.MoveCssInline(MessageBuilder.GetTemplate("EmailTemplate").Replace("%BODY%", builder.ToString()));

            return result.Html;
        }

        private static string BuildStepDetails(IReadOnlyList<StepDetails> details)
        {
            StringBuilder builder = new StringBuilder();

            foreach (StepDetails d in details)
            {

                if (d.StepDefinition.IsExportStep)
                {
                    builder.AppendLine(BuildExportStepDetails(d));
                }
                else if (d.StepDefinition.IsImportStep)
                {
                    builder.AppendLine(BuildImportStepDetails(d));
                }

                if (d.StepDefinition.IsSyncStep)
                {
                    builder.AppendLine(BuildSyncStepDetails(d));
                }
            }

            return builder.ToString();
        }

        private static string BuildExportStepDetails(StepDetails d)
        {
            StringBuilder builder = new StringBuilder();

            string stepDetails = string.Format(MessageBuilder.TwoColumnHeader, 6, d.StepNumber, d.StepDefinition.StepTypeDescription);
            builder.AppendFormat(MessageBuilder.FirstRow, stepDetails, "Export adds", d.ExportCounters.ExportAdd);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Export deletes", d.ExportCounters.ExportDelete);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Export delete-adds", d.ExportCounters.ExportDeleteAdd);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Export rename", d.ExportCounters.ExportRename);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Export update", d.ExportCounters.ExportUpdate);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Export failures", d.ExportCounters.ExportFailure);

            return builder.ToString();
        }

        private static string BuildImportStepDetails(StepDetails d)
        {
            StringBuilder builder = new StringBuilder();

            string stepDetails = string.Format(MessageBuilder.TwoColumnHeader, 7, d.StepNumber, d.StepDefinition.StepTypeDescription);
            builder.AppendFormat(MessageBuilder.FirstRow, stepDetails, "Import adds", d.StagingCounters.StageAdd);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Import deletes", d.StagingCounters.StageDelete);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Import delete-adds", d.StagingCounters.StageDeleteAdd);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Import rename", d.StagingCounters.StageRename);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Import update", d.StagingCounters.StageUpdate);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Import no change", d.StagingCounters.StageNoChange);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Import failures", d.StagingCounters.StageFailure);

            return builder.ToString();
        }

        private static string BuildStagingErrorDetails(IReadOnlyList<StepDetails> details)
        {
            StringBuilder b = new StringBuilder();

            foreach (StepDetails d in details)
            {
                string result = BuildStagingErrorDetails(d);

                if (result != null)
                {
                    b.AppendLine(result);
                }
            }

            if (b.Length == 0)
            {
                return null;
            }
            else
            {
                return b.ToString();
            }
        }

        private static string BuildImportErrorDetails(IReadOnlyList<StepDetails> details)
        {
            StringBuilder b = new StringBuilder();

            foreach (StepDetails d in details)
            {
                string result = BuildImportErrorDetails(d);

                if (result != null)
                {
                    b.AppendLine(result);
                }
            }

            if (b.Length == 0)
            {
                return null;
            }
            else
            {
                return b.ToString();
            }
        }

        private static string BuildExportErrorDetails(IReadOnlyList<StepDetails> details)
        {
            StringBuilder b = new StringBuilder();

            foreach (StepDetails d in details)
            {
                string result = BuildExportErrorDetails(d);

                if (result != null)
                {
                    b.AppendLine(result);
                }
            }

            if (b.Length == 0)
            {
                return null;
            }
            else
            {
                return b.ToString();
            }
        }

        private static string BuildSyncErrorDetails(IReadOnlyList<StepDetails> details)
        {
            StringBuilder b = new StringBuilder();

            foreach (StepDetails d in details)
            {
                string result = BuildSyncErrorDetails(d);

                if (result != null)
                {
                    b.AppendLine(result);
                }
            }

            if (b.Length == 0)
            {
                return null;
            }
            else
            {
                return b.ToString();
            }
        }

        private static string BuildStagingErrorDetails(StepDetails d)
        {
            if (d.MADiscoveryErrors.Count == 0)
            {
                return null;
            }

            IEnumerable<MAObjectError> errors;
            int remainingErrors = 0;

            if (Program.ActiveConfig.Settings.MailMaxErrors <= 0 || d.MADiscoveryErrors.Count <= Program.ActiveConfig.Settings.MailMaxErrors)
            {
                errors = d.MADiscoveryErrors;
            }
            else
            {
                errors = d.MADiscoveryErrors.Take(Program.ActiveConfig.Settings.MailMaxErrors);
                remainingErrors = d.MADiscoveryErrors.Count - Program.ActiveConfig.Settings.MailMaxErrors;
            }

            StringBuilder errorsBuilder = new StringBuilder();

            errorsBuilder.AppendLine("<h3>Staging errors</h3>");

            foreach (MAObjectError error in errors)
            {
                StringBuilder errorBuilder = new StringBuilder();

                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "DN", error.DN);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error type", error.ErrorType);

                if (error.CDError != null)
                {
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error code", error.CDError.ErrorCode);
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error literal", error.CDError.ErrorLiteral);
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Server error detail", error.CDError.ServerErrorDetail);

                    if (error.CDError.Value != null)
                    {
                        errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Value", string.Join(", ", error.CDError.Value));
                    }
                }

                if (error.LineNumber > 0)
                {
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Line number", error.LineNumber);
                }

                if (error.ColumnNumber > 0)
                {
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Column number", error.ColumnNumber);
                }

                if (error.AttributeName != null)
                {
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Attribute name", error.AttributeName);
                }

                errorsBuilder.AppendLine(string.Format(MessageBuilder.GetTemplate("ErrorTableFragment"), errorBuilder));
                errorsBuilder.AppendLine("<br/>");
            }

            if (remainingErrors > 0)
            {
                errorsBuilder.Append($"There are {remainingErrors} more errors that are not shown in this report<br/>");
            }

            return errorsBuilder.ToString();
        }

        private static string BuildImportErrorDetails(StepDetails d)
        {
            if (d.SynchronizationErrors.ImportErrors.Count == 0)
            {
                return null;
            }

            IEnumerable<ImportError> errors;
            int remainingErrors = 0;

            if (Program.ActiveConfig.Settings.MailMaxErrors <= 0 || d.SynchronizationErrors.ImportErrors.Count <= Program.ActiveConfig.Settings.MailMaxErrors)
            {
                errors = d.SynchronizationErrors.ImportErrors;
            }
            else
            {
                errors = d.SynchronizationErrors.ImportErrors.Take(Program.ActiveConfig.Settings.MailMaxErrors);
                remainingErrors = d.SynchronizationErrors.ImportErrors.Count - Program.ActiveConfig.Settings.MailMaxErrors;
            }

            StringBuilder errorsBuilder = new StringBuilder();

            errorsBuilder.AppendLine("<h3>Inbound synchronization errors</h3>");

            foreach (ImportError error in errors)
            {
                StringBuilder errorBuilder = new StringBuilder();

                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "DN", error.DN);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error type", error.ErrorType);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Date occurred", error.DateOccurred);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Retry count", error.RetryCount);

                MessageBuilder.BuildErrorExtensionInfo(error.ExtensionErrorInfo, errorBuilder);
                MessageBuilder.BuildRulesErrorInfo(error.RulesErrorInfo, errorBuilder);

                errorsBuilder.AppendLine(string.Format(MessageBuilder.GetTemplate("ErrorTableFragment"), errorBuilder));
                errorsBuilder.AppendLine("<br/>");
            }

            if (remainingErrors > 0)
            {
                errorsBuilder.Append($"There are {remainingErrors} more errors that are not shown in this report<br/>");
            }

            return errorsBuilder.ToString();
        }

        private static string BuildExportErrorDetails(StepDetails d)
        {
            if (d.SynchronizationErrors.ExportErrors.Count == 0)
            {
                return null;
            }

            IEnumerable<ExportError> errors;
            int remainingErrors = 0;

            if (Program.ActiveConfig.Settings.MailMaxErrors <= 0 || d.SynchronizationErrors.ExportErrors.Count <= Program.ActiveConfig.Settings.MailMaxErrors)
            {
                errors = d.SynchronizationErrors.ExportErrors;
            }
            else
            {
                errors = d.SynchronizationErrors.ExportErrors.Take(Program.ActiveConfig.Settings.MailMaxErrors);
                remainingErrors = d.SynchronizationErrors.ExportErrors.Count - Program.ActiveConfig.Settings.MailMaxErrors;
            }

            StringBuilder errorsBuilder = new StringBuilder();

            errorsBuilder.AppendLine("<h3>Export errors</h3>");

            foreach (ExportError error in errors)
            {
                StringBuilder errorBuilder = new StringBuilder();

                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "DN", error.DN);
              
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error type", error.ErrorType);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Date occurred", error.DateOccurred);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "First occurred", error.FirstOccurred);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Retry count", error.RetryCount);

                if (error.CDError != null)
                {
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error code", error.CDError.ErrorCode);
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error literal", error.CDError.ErrorLiteral);
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Server error detail", error.CDError.ServerErrorDetail);
                }

                errorsBuilder.AppendLine(string.Format(MessageBuilder.GetTemplate("ErrorTableFragment"), errorBuilder));
                errorsBuilder.AppendLine("<br/>");
            }

            if (remainingErrors > 0)
            {
                errorsBuilder.Append($"There are {remainingErrors} more errors that are not shown in this report<br/>");
            }

            return errorsBuilder.ToString();
        }

        private static string BuildSyncStepDetails(StepDetails d)
        {
            StringBuilder builder = new StringBuilder();

            string stepDetails = string.Format(MessageBuilder.ThreeColumnHeader, 10, d.StepNumber, d.StepDefinition.StepTypeDescription, "Inbound");
            builder.AppendFormat(MessageBuilder.FirstRow, stepDetails, "Projections", d.InboundFlowCounters.TotalProjections);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Joins", d.InboundFlowCounters.TotalJoins);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Filtered disconnectors", d.InboundFlowCounters.DisconnectorFiltered);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Disconnectors", d.InboundFlowCounters.DisconnectedRemains);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Connectors with flow updates", d.InboundFlowCounters.ConnectorFlow);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Connectors without flow updates", d.InboundFlowCounters.ConnectorNoFlow);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Filtered connectors", d.InboundFlowCounters.TotalFilteredConnectors);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Deleted connectors", d.InboundFlowCounters.TotalDeletedConnectors);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Metaverse object deletes", d.InboundFlowCounters.TotalMVObjectDeletes);
            builder.AppendFormat(MessageBuilder.SimpleRow, "Flow errors", d.InboundFlowCounters.FlowFailure);

            foreach (OutboundFlowCounters item in d.OutboundFlowCounters)
            {
                string f = string.Format(MessageBuilder.ThreeColumnHeader, 5, string.Empty, string.Empty, "Outbound: " + item.ManagementAgent);
                builder.AppendFormat(MessageBuilder.FirstRow, f, "Export attribute flow", item.ConnectorFlow);
                builder.AppendFormat(MessageBuilder.SimpleRow, "Provisioning adds", (item.ProvisionedAddFlow + item.ProvisionedAddNoFlow));
                builder.AppendFormat(MessageBuilder.SimpleRow, "Provisioning renames", (item.ProvisionRenameFlow + item.ProvisionedRenameNoFlow));
                builder.AppendFormat(MessageBuilder.SimpleRow, "Provisioning disconnects", item.ProvisionedDisconnect);
                builder.AppendFormat(MessageBuilder.SimpleRow, "Provisioning delete-adds", (item.ProvisionedDeleteAddFlow + item.ProvisionedDeleteAddNoFlow));

            }

            return builder.ToString();
        }

        private static string BuildSyncErrorDetails(StepDetails d)
        {
            if (d.MVRetryErrors == null || d.MVRetryErrors.Count == 0)
            {
                return null;
            }

            IEnumerable<MVRetryError> errors;
            int remainingErrors = 0;

            if (Program.ActiveConfig.Settings.MailMaxErrors <= 0 || d.MVRetryErrors.Count <= Program.ActiveConfig.Settings.MailMaxErrors)
            {
                errors = d.MVRetryErrors;
            }
            else
            {
                errors = d.MVRetryErrors.Take(Program.ActiveConfig.Settings.MailMaxErrors);
                remainingErrors = d.MVRetryErrors.Count - Program.ActiveConfig.Settings.MailMaxErrors;
            }

            StringBuilder errorsBuilder = new StringBuilder();

            errorsBuilder.AppendLine("<h3>Outbound synchronization errors</h3>");

            foreach (MVRetryError error in errors)
            {
                StringBuilder errorBuilder = new StringBuilder();

                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Object", error.DisplayName ?? error.MVID);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Step", error.AlgorithmStep?.Value);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Error type", error.ErrorType);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Date occurred", error.DateOccurred);

                MessageBuilder.BuildErrorExtensionInfo(error.ExtensionErrorInfo, errorBuilder);
                MessageBuilder.BuildRulesErrorInfo(error.RulesErrorInfo, errorBuilder);

                errorsBuilder.AppendLine(string.Format(MessageBuilder.GetTemplate("ErrorTableFragment"), errorBuilder));
                errorsBuilder.AppendLine("<br/>");
            }

            if (remainingErrors > 0)
            {
                errorsBuilder.Append($"There are {remainingErrors} more errors that are not shown in this report<br/>");
            }

            return errorsBuilder.ToString();
        }

        private static void BuildRulesErrorInfo(RulesErrorInfoContext c, StringBuilder errorBuilder)
        {
            if (c != null)
            {
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "CS Object ID", c.CSObjectID);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "DN", c.DN);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "MA Name", c.MAName);

                if (c.AttributeFlow != null)
                {
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Context", c.AttributeFlow.ContextID);
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Destination attribute", c.AttributeFlow.DestinationAttribute);
                    errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Flow rule", c.AttributeFlow.FlowRule);
                }
            }
        }

        private static void BuildErrorExtensionInfo(ExtensionErrorInfo error, StringBuilder errorBuilder)
        {
            if (error != null)
            {
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Extension name", error.ExtensionName);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Context", error.ExtensionContext);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Call site", error.ExtensionCallSite);
                errorBuilder.AppendFormat(MessageBuilder.SimpleRow, "Call stack", error.CallStack);
            }
        }
    }
}
