using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Lithnet.Logging;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class Extensions
    {
        public static void ThrowOnPipelineError(this PowerShell powershell)
        {
            if (!powershell.HadErrors)
            {
                return;
            }

            Logger.WriteLine("The PowerShell script encountered an error");

            foreach (ErrorRecord error in powershell.Streams.Error)
            {
                if (error.ErrorDetails != null)
                {
                    Logger.WriteLine(error.ErrorDetails.Message);
                    Logger.WriteLine(error.ErrorDetails.RecommendedAction);
                }

                Logger.WriteLine(error.ScriptStackTrace);

                if (error.Exception != null)
                {
                    Logger.WriteException(error.Exception);
                }
            }

            throw new ApplicationException("The PowerShell script encountered an error");
        }
    }
}