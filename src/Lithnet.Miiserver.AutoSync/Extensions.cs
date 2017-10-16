using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceModel;
using System.Threading;
using Lithnet.Miiserver.Client;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    public static class Extensions
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void ResetState(this PowerShell powershell)
        {
            powershell.Streams.ClearStreams();
            powershell.Commands.Clear();
        }

        public static void ThrowOnPipelineError(this PowerShell powershell)
        {
            if (!powershell.HadErrors)
            {
                return;
            }

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
                    b.AppendLine(error.Exception.ToString());
                }
            }

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
                        logger.Debug(ex, "Invocation communication exception");
                        client.Abort();
                    }
                    catch (TimeoutException ex)
                    {
                        logger.Debug(ex, "Invocation timeout");
                        client.Abort();
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "Invocation exception");
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
                        logger.Debug(ex, "Invocation communication exception");
                        client.Abort();
                    }
                    catch (TimeoutException ex)
                    {
                        logger.Debug(ex, "Invocation timeout");
                        client.Abort();
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "Invocation exception");
                        client.Abort();
                        throw;
                    }
                }
            }
        }
    }
}