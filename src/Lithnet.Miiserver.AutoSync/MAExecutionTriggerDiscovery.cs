using System;
using System.Collections.Generic;
using System.Linq;
using Lithnet.Miiserver.Client;
using Lithnet.Logging;
using System.IO;
using System.Xml;

namespace Lithnet.Miiserver.AutoSync
{
    public static class MAExecutionTriggerDiscovery
    {
        private const int DefaultIntervalMinutes = 15;

        
        internal static int GetAverageImportInterval(ManagementAgent ma)
        {
            int deltaCount = 0;
            int deltaTotalTime = 0;

            int fullCount = 0;
            int fullTotalTime = 0;

            try
            {
                IList<RunDetails> rh = ma.GetRunHistory(200).ToList();

                foreach (RunDetails rd in rh)
                {
                    if (rd.EndTime == null || rd.StartTime == null || rd.StepDetails == null)
                    {
                        continue;
                    }

                    foreach (StepDetails sd in rd.StepDetails)
                    {
                        if (sd.StepDefinition == null || sd.EndDate == null || sd.StartDate == null)
                        {
                            continue;
                        }

                        if (sd.StepDefinition.Type == RunStepType.DeltaImport)
                        {
                            TimeSpan ts = sd.EndDate.Value - sd.StartDate.Value;

                            deltaCount++;
                            deltaTotalTime = deltaTotalTime + (int)ts.TotalSeconds;
                        }
                        else if (sd.StepDefinition.Type == RunStepType.FullImport)
                        {
                            TimeSpan ts = sd.EndDate.Value - sd.StartDate.Value;

                            fullCount++;
                            fullTotalTime = fullTotalTime + (int)ts.TotalSeconds;
                        }
                    }

                    if (deltaCount > 50)
                    {
                        break;
                    }
                }
            }
            catch (XmlException ex)
            {
                Logger.WriteLine("There was an error searching the run history. The run history may be corrupted. Using default intervals");
                Logger.WriteException(ex);
            }

            if (deltaCount == 0 && fullCount > 0)
            {
                return ((fullTotalTime / fullCount)) + (MAExecutionTriggerDiscovery.DefaultIntervalMinutes * 60);
            }
            else if (deltaCount == 0 && fullCount == 0)
            {
                return MAExecutionTriggerDiscovery.DefaultIntervalMinutes * 60;
            }

            return ((deltaTotalTime / deltaCount)) + (MAExecutionTriggerDiscovery.DefaultIntervalMinutes * 60);
        }


        private static bool IsSourceMA(ManagementAgent ma)
        {
            string madata = ma.ExportManagementAgent();

            XmlDocument d = new XmlDocument();
            d.LoadXml(madata);

            int eafCount = d.SelectNodes("/export-ma/ma-data/export-attribute-flow/export-flow-set/export-flow")?.Count ?? 0;
            int iafCount = d.SelectNodes($"/export-ma/mv-data/import-attribute-flow/import-flow-set/import-flows/import-flow[@src-ma='{ma.ID.ToString("B").ToUpper()}']")?.Count ?? 0;

            return iafCount > eafCount;
        }


     
    }
}
