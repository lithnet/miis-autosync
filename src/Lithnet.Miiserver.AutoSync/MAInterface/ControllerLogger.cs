using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ControllerLogger
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private string managementAgentName;

        private Guid managementAgentID;


        public event EventHandler<MessageLoggedEventArgs> MessageLogged;

        private CancellationToken token;

        public ControllerLogger(string managementAgentName, Guid managementAgentID, CancellationToken token)
        {
            this.token = token;
            this.managementAgentName = managementAgentName;
            this.managementAgentID = managementAgentID;
        }

        public void LogInfo(string message)
        {
            logger.Info($"{this.managementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        public void LogWarn(string message)
        {
            logger.Warn($"{this.managementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        public void LogWarn(Exception ex, string message)
        {
            logger.Warn(ex, $"{this.managementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        public void LogError(string message)
        {
            logger.Error($"{this.managementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        public void LogError(Exception ex, string message)
        {
            logger.Error(ex, $"{this.managementAgentName}: {message}");
            this.RaiseMessageLogged(message);
        }

        public void Trace(string message)
        {
            logger.Trace($"{this.managementAgentName}: {message}");
        }

        public void RaiseMessageLogged(string message)
        {
            Task.Run(() =>
            {
                try
                {
                    this.MessageLogged?.Invoke(this, new MessageLoggedEventArgs(DateTime.Now, this.managementAgentID, message));
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Unable to relay state change");
                }
            }, this.token);
        }
    }
}
