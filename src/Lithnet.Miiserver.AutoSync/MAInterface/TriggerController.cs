using System;
using System.Collections.Generic;
using System.Threading;

namespace Lithnet.Miiserver.AutoSync
{
    internal class TriggerController
    {
        private List<IMAExecutionTrigger> triggers;

        private ControllerLogger logger;

        private MAControllerConfiguration config;

        private ExecutionQueue queue;

        public TriggerController(MAControllerConfiguration config, ControllerLogger logger, ExecutionQueue queue, CancellationToken controllerCancellationToken)
        {
            this.logger = logger;
            this.config = config;
            this.queue = queue;
            this.triggers = new List<IMAExecutionTrigger>();
        }
        
        public void Attach(IEnumerable<IMAExecutionTrigger> triggers)
        {
            if (triggers == null)
            {
                throw new ArgumentNullException(nameof(triggers));
            }

            foreach (IMAExecutionTrigger trigger in triggers)
            {
                if (trigger.Disabled)
                {
                    this.logger.LogInfo($"Skipping disabled trigger '{trigger.DisplayName}'");
                    continue;
                }

                this.triggers.Add(trigger);
            }
        }

        public void Start()
        {
            foreach (IMAExecutionTrigger t in this.triggers)
            {
                try
                {
                    this.logger.LogInfo($"Registering execution trigger '{t.DisplayName}'");
                    t.Message += this.NotifierTriggerMessage;
                    t.Error += this.NotifierTriggerError;
                    t.TriggerFired += this.NotifierTriggerFired;
                    t.Start(this.config.ManagementAgentName);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, $"Could not start execution trigger {t.DisplayName}");
                }
            }
        }

        public void Stop()
        {
            foreach (IMAExecutionTrigger t in this.triggers)
            {
                try
                {
                    this.logger.LogInfo($"Unregistering execution trigger '{t.DisplayName}'");
                    t.TriggerFired -= this.NotifierTriggerFired;
                    t.Message -= this.NotifierTriggerMessage;
                    t.Error -= this.NotifierTriggerError;
                    t.Stop();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, $"Could not stop execution trigger {t.DisplayName}");
                }
            }
        }

        private void NotifierTriggerMessage(object sender, TriggerMessageEventArgs e)
        {
            IMAExecutionTrigger t = (IMAExecutionTrigger)sender;
            if (e.Details == null)
            {
                this.logger.LogInfo($"{t.DisplayName}: {e.Message}");
            }
            else
            {
                this.logger.LogInfo($"{t.DisplayName}: {e.Message}\n{e.Details}");
            }
        }

        private void NotifierTriggerError(object sender, TriggerMessageEventArgs e)
        {
            IMAExecutionTrigger t = (IMAExecutionTrigger)sender;
            if (e.Details == null)
            {
                this.logger.LogError($"{t.DisplayName}: {e.Message}");
            }
            else
            {
                this.logger.LogError($"{t.DisplayName}: {e.Message}\n{e.Details}");
            }
        }

        private void NotifierTriggerFired(object sender, ExecutionTriggerEventArgs e)
        {
            IMAExecutionTrigger trigger = null;

            try
            {
                trigger = (IMAExecutionTrigger)sender;

                if (string.IsNullOrWhiteSpace(e.Parameters.RunProfileName))
                {
                    if (e.Parameters.RunProfileType == MARunProfileType.None)
                    {
                        this.logger.LogWarn($"Received empty run profile from trigger {trigger.DisplayName}");
                        return;
                    }
                }

                this.queue.Add(e.Parameters, trigger.DisplayName);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"The was an unexpected error processing an incoming trigger from {trigger?.DisplayName}");
            }
        }
    }
}
