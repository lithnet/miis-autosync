using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using Lithnet.Logging;
using Lithnet.Miiserver.Client;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Extensions
    {
        public static void ResetState(this PowerShell powershell)
        {
            powershell.Streams.Error.Clear();
            powershell.Streams.Warning.Clear();
            powershell.Streams.Verbose.Clear();
            powershell.Streams.Progress.Clear();
            powershell.Streams.Debug.Clear();
            powershell.Streams.Information.Clear();
            powershell.Commands.Clear();
        }

        public static void ThrowOnPipelineError(this PowerShell powershell)
        {
            if (!powershell.HadErrors)
            {
                return;
            }

            Logger.WriteLine("The PowerShell script encountered an error");

            StringBuilder b = new StringBuilder();

            foreach (ErrorRecord error in powershell.Streams.Error)
            {
                if (error.ErrorDetails != null)
                {
                    b.AppendLine(error.ErrorDetails.Message);
                    b.AppendLine(error.ErrorDetails.RecommendedAction);
                }

                b.AppendLine(error.ScriptStackTrace);

                if (error.Exception != null)
                {
                    Logger.WriteException(error.Exception);
                    b.AppendLine(error.Exception.ToString());
                }
            }

            Logger.WriteLine(b.ToString());

            throw new ApplicationException("The PowerShell script encountered an error\n" + b.ToString());
        }

        public static SecureString ToSecureString(this string s)
        {
            if (s == null)
            {
                return null;
            }

            SecureString sec = new SecureString();

            Array.ForEach(s.ToArray(), sec.AppendChar);

            return sec;
        }

        public static string ToUnsecureString(this SecureString s)
        {
            IntPtr valuePtr = IntPtr.Zero;

            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(s);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        public static string Pluralize(this int number)
        {
            return number == 1 ? string.Empty : "s";
        }

        public static bool HasUnconfirmedExports(this RunDetails d)
        {
            if (d?.StepDetails == null)
            {
                return false;
            }

            foreach (StepDetails s in d.StepDetails)
            {
                if (s.StepDefinition.IsImportStep)
                {
                    // If an import is present, before an export step, a confirming import is not required
                    return false;
                }

                if (s.StepDefinition.Type == RunStepType.Export)
                {
                    // If we get here, an export step has been found that it more recent than any import step
                    // that may be in the run profile
                    return s.ExportCounters?.HasChanges ?? false;
                }
            }

            return false;
        }


        public static bool HasStagedImports(this RunDetails d)
        {
            if (d?.StepDetails == null)
            {
                return false;
            }

            foreach (StepDetails s in d.StepDetails)
            {
                if (s.StepDefinition.IsSyncStep)
                {
                    // If a sync is present, before an import step, a sync is not required
                    return false;
                }

                if (s.StepDefinition.IsImportStep)
                {
                    // If we get here, an import step has been found that it more recent than any sync step
                    // that may be in the run profile
                    return s.StagingCounters?.HasChanges ?? false;
                }
            }

            return false;
        }

        public static bool HasUnconfirmedExports(this StepDetails s)
        {
            return s?.ExportCounters?.HasChanges ?? false;
        }

        public static TResult InvokeThenClose<TChannel, TResult>(this TChannel client, Func<TChannel, TResult> function) where TChannel : ICommunicationObject
        {
            try
            {
                return function(client);
            }
            finally
            {
                if (client != null)
                {
                    try
                    {
                        if (client.State != CommunicationState.Faulted)
                        {
                            client.Close();
                        }
                        else
                        {
                            client.Abort();
                        }
                    }
                    catch (CommunicationException ex)
                    {
                        Debug.WriteLine("Invocation communication exception");
                        Debug.WriteLine(ex);
                        client.Abort();
                    }
                    catch (TimeoutException ex)
                    {
                        Debug.WriteLine("Invocation timeout");
                        Debug.WriteLine(ex);
                        client.Abort();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Invocation exception");
                        Debug.WriteLine(ex);
                        client.Abort();
                        throw;
                    }
                }
            }
        }


        public static void SetThreadName(this Thread thread, string name)
        {
            if (thread.Name == null)
            {
                thread.Name = name;
            }
        }

        public static void InvokeThenClose<TChannel>(this TChannel client, Action<TChannel> function) where TChannel : ICommunicationObject
        {
            try
            {
                function(client);
            }
            finally
            {
                if (client != null)
                {
                    try
                    {
                        if (client.State != CommunicationState.Faulted)
                        {
                            client.Close();
                        }
                        else
                        {
                            client.Abort();
                        }
                    }
                    catch (CommunicationException ex)
                    {
                        Debug.WriteLine("Invocation communication exception");
                        Debug.WriteLine(ex);
                        client.Abort();
                    }
                    catch (TimeoutException ex)
                    {
                        Debug.WriteLine("Invocation timeout");
                        Debug.WriteLine(ex);
                        client.Abort();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Invocation exception");
                        Debug.WriteLine(ex);
                        client.Abort();
                        throw;
                    }
                }
            }
        }
    }
}