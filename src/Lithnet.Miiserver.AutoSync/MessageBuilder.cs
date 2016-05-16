using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Lithnet.Miiserver.Client;
using System.Xml;
using System.Reflection;
using System.IO;
using PreMailer.Net;

namespace Lithnet.Miiserver.AutoSync
{
    public static class MessageBuilder
    {
        private const string simpleRow = "<tr><td>{0}</td><td>{1}</td></tr>";
       // private const string cdataRow = "<tr><td>{0}</td><td><![CDATA[{1}]]></td></tr>";
        private const string firstRow = "<tr>{0}<td>{1}</td><td>{2}</td></tr>";
        private const string twoColumnHeader = "<td rowspan=\"{0}\" valign=\"top\">{1}</td><td rowspan=\"{0}\" colspan=\"2\" valign=\"top\">{2}</td>";
        private const string threeColumnHeader = "<td rowspan=\"{0}\" valign=\"top\">{1}</td><td rowspan=\"{0}\" valign=\"top\">{2}</td><td rowspan=\"{0}\" valign=\"top\">{3}</td>";

        private static string GetTemplate(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = string.Format("Lithnet.Miiserver.AutoSync.Resources.{0}.html", name);

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static string GetMessageBody(RunDetails r)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendFormat(MessageBuilder.GetTemplate("RunSummaryFragment"), r.RunProfileName, r.MAName, r.StartTime, r.EndTime, r.SecurityID, r.LastStepStatus);

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

            string stepDetails = string.Format(twoColumnHeader, 6, d.StepNumber, d.StepDefinition.StepTypeDescription);
            builder.AppendFormat(firstRow, stepDetails, "Export adds", d.ExportCounters.ExportAdd);
            builder.AppendFormat(simpleRow, "Export deletes", d.ExportCounters.ExportDelete);
            builder.AppendFormat(simpleRow, "Export delete-adds", d.ExportCounters.ExportDeleteAdd);
            builder.AppendFormat(simpleRow, "Export rename", d.ExportCounters.ExportRename);
            builder.AppendFormat(simpleRow, "Export update", d.ExportCounters.ExportUpdate);
            builder.AppendFormat(simpleRow, "Export failures", d.ExportCounters.ExportFailure);

            return builder.ToString();
        }

        private static string BuildImportStepDetails(StepDetails d)
        {
            StringBuilder builder = new StringBuilder();

            string stepDetails = string.Format(twoColumnHeader, 7, d.StepNumber, d.StepDefinition.StepTypeDescription);
            builder.AppendFormat(firstRow, stepDetails, "Import adds", d.StagingCounters.StageAdd);
            builder.AppendFormat(simpleRow, "Import deletes", d.StagingCounters.StageDelete);
            builder.AppendFormat(simpleRow, "Import delete-adds", d.StagingCounters.StageDeleteAdd);
            builder.AppendFormat(simpleRow, "Import rename", d.StagingCounters.StageRename);
            builder.AppendFormat(simpleRow, "Import update", d.StagingCounters.StageUpdate);
            builder.AppendFormat(simpleRow, "Import no change", d.StagingCounters.StageNoChange);
            builder.AppendFormat(simpleRow, "Import failures", d.StagingCounters.StageFailure);

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

        private static string BuildStagingErrorDetails(StepDetails d)
        {
            if (d.MADiscoveryErrors.Count == 0)
            {
                return null;
            }

            IEnumerable<MAObjectError> errors;
            int remainingErrors = 0;

            if (Settings.MailMaxErrorItems <= 0 || d.MADiscoveryErrors.Count <= Settings.MailMaxErrorItems)
            {
                errors = d.MADiscoveryErrors;
            }
            else
            {
                errors = d.MADiscoveryErrors.Take(Settings.MailMaxErrorItems);
                remainingErrors = d.MADiscoveryErrors.Count - Settings.MailMaxErrorItems;
            }

            StringBuilder errorsBuilder = new StringBuilder();

            errorsBuilder.AppendLine("<h3>Staging errors</h3>");

            foreach (MAObjectError error in errors)
            {
                StringBuilder errorBuilder = new StringBuilder();

                errorBuilder.AppendFormat(simpleRow, "DN", error.DN);
                errorBuilder.AppendFormat(simpleRow, "Error type", error.ErrorType);

                if (error.CDError != null)
                {
                    errorBuilder.AppendFormat(simpleRow, "Error code", error.CDError.ErrorCode);
                    errorBuilder.AppendFormat(simpleRow, "Error literal", error.CDError.ErrorLiteral);
                    errorBuilder.AppendFormat(simpleRow, "Server error detail", error.CDError.ServerErrorDetail);

                    if (error.CDError.Value != null)
                    {
                        errorBuilder.AppendFormat(simpleRow, "Value", error.CDError.Value.ToCommaSeparatedString());
                    }
                }

                if (error.LineNumber > 0)
                {
                    errorBuilder.AppendFormat(simpleRow, "Line number", error.LineNumber);
                }

                if (error.ColumnNumber > 0)
                {
                    errorBuilder.AppendFormat(simpleRow, "Column number", error.ColumnNumber);
                }

                if (error.AttributeName != null)
                {
                    errorBuilder.AppendFormat(simpleRow, "Attribute name", error.AttributeName);
                }

                errorsBuilder.AppendLine(string.Format(MessageBuilder.GetTemplate("ErrorTableFragment"), errorBuilder.ToString()));
                errorsBuilder.AppendLine("<br/>");
            }

            if (remainingErrors > 0)
            {
                errorsBuilder.AppendFormat("There are {0} more errors that are not shown in this report<br/>", remainingErrors);
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

            if (Settings.MailMaxErrorItems <= 0 || d.SynchronizationErrors.ImportErrors.Count <= Settings.MailMaxErrorItems)
            {
                errors = d.SynchronizationErrors.ImportErrors;
            }
            else
            {
                errors = d.SynchronizationErrors.ImportErrors.Take(Settings.MailMaxErrorItems);
                remainingErrors = d.SynchronizationErrors.ImportErrors.Count - Settings.MailMaxErrorItems;
            }

            StringBuilder errorsBuilder = new StringBuilder();

            errorsBuilder.AppendLine("<h3>Synchronization errors</h3>");

            foreach (ImportError error in errors)
            {
                StringBuilder errorBuilder = new StringBuilder();

                errorBuilder.AppendFormat(simpleRow, "DN", error.DN);
                errorBuilder.AppendFormat(simpleRow, "Error type", error.ErrorType);
                errorBuilder.AppendFormat(simpleRow, "Date occurred", error.DateOccurred.ToString());
                errorBuilder.AppendFormat(simpleRow, "Retry count", error.RetryCount);

                if (error.ExtensionErrorInfo != null)
                {
                    errorBuilder.AppendFormat(simpleRow, "Extension name", error.ExtensionErrorInfo.ExtensionName);
                    errorBuilder.AppendFormat(simpleRow, "Context", error.ExtensionErrorInfo.ExtensionContext);
                    errorBuilder.AppendFormat(simpleRow, "Call site", error.ExtensionErrorInfo.ExtensionCallSite.ToString());
                    errorBuilder.AppendFormat(simpleRow, "Stack trace", error.ExtensionErrorInfo.CallStack);
                }

                errorsBuilder.AppendLine(string.Format(MessageBuilder.GetTemplate("ErrorTableFragment"), errorBuilder.ToString()));
                errorsBuilder.AppendLine("<br/>");
            }

            if (remainingErrors > 0)
            {
                errorsBuilder.AppendFormat("There are {0} more errors that are not shown in this report<br/>", remainingErrors);
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

            if (Settings.MailMaxErrorItems <= 0 || d.SynchronizationErrors.ExportErrors.Count <= Settings.MailMaxErrorItems)
            {
                errors = d.SynchronizationErrors.ExportErrors;
            }
            else
            {
                errors = d.SynchronizationErrors.ExportErrors.Take(Settings.MailMaxErrorItems);
                remainingErrors = d.SynchronizationErrors.ExportErrors.Count - Settings.MailMaxErrorItems;
            }

            StringBuilder errorsBuilder = new StringBuilder();

            errorsBuilder.AppendLine("<h3>Export errors</h3>");

            foreach (ExportError error in errors)
            {
                StringBuilder errorBuilder = new StringBuilder();

                errorBuilder.AppendFormat(simpleRow, "DN", error.DN);
              
                errorBuilder.AppendFormat(simpleRow, "Error type", error.ErrorType);
                errorBuilder.AppendFormat(simpleRow, "Date occurred", error.DateOccurred.ToString());
                errorBuilder.AppendFormat(simpleRow, "First occurred", error.FirstOccurred.ToString());
                errorBuilder.AppendFormat(simpleRow, "Retry count", error.RetryCount);

                if (error.CDError != null)
                {
                    errorBuilder.AppendFormat(simpleRow, "Error code", error.CDError.ErrorCode);
                    errorBuilder.AppendFormat(simpleRow, "Error literal", error.CDError.ErrorLiteral);
                    errorBuilder.AppendFormat(simpleRow, "Server error detail", error.CDError.ServerErrorDetail);
                }

                errorsBuilder.AppendLine(string.Format(MessageBuilder.GetTemplate("ErrorTableFragment"), errorBuilder.ToString()));
                errorsBuilder.AppendLine("<br/>");
            }

            if (remainingErrors > 0)
            {
                errorsBuilder.AppendFormat("There are {0} more errors that are not shown in this report<br/>", remainingErrors);
            }

            return errorsBuilder.ToString();
        }

        private static string BuildSyncStepDetails(StepDetails d)
        {
            StringBuilder builder = new StringBuilder();

            string stepDetails = string.Format(threeColumnHeader, 10, d.StepNumber, d.StepDefinition.StepTypeDescription, "Inbound");
            builder.AppendFormat(firstRow, stepDetails, "Projections", d.InboundFlowCounters.TotalProjections);
            builder.AppendFormat(simpleRow, "Joins", d.InboundFlowCounters.TotalJoins);
            builder.AppendFormat(simpleRow, "Filtered disconnectors", d.InboundFlowCounters.DisconnectorFiltered);
            builder.AppendFormat(simpleRow, "Disconnectors", d.InboundFlowCounters.DisconnectedRemains);
            builder.AppendFormat(simpleRow, "Connectors with flow updates", d.InboundFlowCounters.ConnectorFlow);
            builder.AppendFormat(simpleRow, "Connectors without flow updates", d.InboundFlowCounters.ConnectorNoFlow);
            builder.AppendFormat(simpleRow, "Filtered connectors", d.InboundFlowCounters.TotalFilteredConnectors);
            builder.AppendFormat(simpleRow, "Deleted connectors", d.InboundFlowCounters.TotalDeletedConnectors);
            builder.AppendFormat(simpleRow, "Metaverse object deletes", d.InboundFlowCounters.TotalMVObjectDeletes);
            builder.AppendFormat(simpleRow, "Flow errors", d.InboundFlowCounters.FlowFailure);

            foreach (OutboundFlowCounters item in d.OutboundFlowCounters)
            {
                string f = string.Format(threeColumnHeader, 5, string.Empty, string.Empty, "Outbound: " + item.ManagementAgent);
                builder.AppendFormat(firstRow, f, "Export attribute flow", item.ConnectorFlow);
                builder.AppendFormat(simpleRow, "Provisioning adds", (item.ProvisionedAddFlow + item.ProvisionedAddNoFlow));
                builder.AppendFormat(simpleRow, "Provisioning renames", (item.ProvisionRenameFlow + item.ProvisionedRenameNoFlow));
                builder.AppendFormat(simpleRow, "Provisioning disconnects", item.ProvisionedDisconnect);
                builder.AppendFormat(simpleRow, "Provisioning delete-adds", (item.ProvisionedDeleteAddFlow + item.ProvisionedDeleteAddNoFlow));

            }

            return builder.ToString();
        }
    }
}
