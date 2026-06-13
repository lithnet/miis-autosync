using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    internal class MAControllerPerfCounters
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The performance counter category. This category is created by the installer
        /// (see Setup.RegisterPerformanceCounters); this class only consumes it.
        /// </summary>
        internal const string CategoryName = "Lithnet AutoSync";

        // True only when all counters were opened successfully. When the category is missing
        // (e.g. a manual/dev run without the installer), counter reporting is disabled rather
        // than allowed to throw out of the constructor and take the controller down with it.
        private readonly bool enabled;

        private Stopwatch activeTimer;

        private Timer timer;

        public PerformanceCounter RunCount { get; }

        public PerformanceCounter WaitTime { get; }

        public PerformanceCounter WaitTimeSyncLock { get; }

        public PerformanceCounter WaitTimeExclusiveLock { get; }


        public PerformanceCounter WaitTimeAverage { get; }

        public PerformanceCounter WaitTimeAverageSyncLock { get; }

        public PerformanceCounter WaitTimeAverageExclusiveLock { get; }

        private PerformanceCounter ExecutionTimeAverage { get; }

        public PerformanceCounter ExecutionTimeTotal { get; }

        public PerformanceCounter IdleTimePercent { get; }


        public PerformanceCounter CurrentQueueLength { get; }


        public MAControllerPerfCounters(string maName)
        {
            this.activeTimer = new Stopwatch();
            this.timer = new Timer();
            this.timer.Interval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            this.timer.Elapsed += this.Timer_Elapsed;
            this.executionHistory = new List<DateTime>();

            try
            {
                this.RunCount = MAControllerPerfCounters.CreateCounter("Runs/10 min", maName);
                this.WaitTimeAverageSyncLock = MAControllerPerfCounters.CreateCounter("Wait time average - sync lock", maName);
                this.WaitTimeAverageExclusiveLock = MAControllerPerfCounters.CreateCounter("Wait time average - exclusive lock", maName);
                this.WaitTimeAverage = MAControllerPerfCounters.CreateCounter("Wait time average", maName);
                this.WaitTimeSyncLock = MAControllerPerfCounters.CreateCounter("Wait time % - sync lock", maName);
                this.WaitTimeExclusiveLock = MAControllerPerfCounters.CreateCounter("Wait time % - exclusive lock", maName);
                this.WaitTime = MAControllerPerfCounters.CreateCounter("Wait time %", maName);
                this.ExecutionTimeAverage = MAControllerPerfCounters.CreateCounter("Execution time average", maName);
                this.ExecutionTimeTotal = MAControllerPerfCounters.CreateCounter("Execution time %", maName);
                this.CurrentQueueLength = MAControllerPerfCounters.CreateCounter("Queue length", maName);
                this.IdleTimePercent = MAControllerPerfCounters.CreateCounter("Idle time %", maName);

                this.enabled = true;
            }
            catch (Exception ex)
            {
                // The category is created by the installer. If it is missing (or the counters
                // cannot be opened), disable reporting instead of failing the controller --
                // performance counters are non-critical telemetry.
                logger.Warn(ex, $"Performance counters are unavailable; performance reporting for '{maName}' is disabled. Ensure the '{MAControllerPerfCounters.CategoryName}' category is registered (this is done by the installer).");
                this.enabled = false;
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.UpdateCounters();
        }

        public void Stop()
        {
            if (!this.enabled)
            {
                return;
            }

            this.timer?.Stop();
            this.activeTimer?.Stop();
            this.ResetValues();
        }

        public void Start()
        {
            if (!this.enabled)
            {
                return;
            }

            this.activeTimer.Restart();
            this.ResetValues();
            this.timer.Start();
        }

        /// <summary>Increments the run counter, if performance counters are available.</summary>
        public void IncrementRunCount()
        {
            if (!this.enabled)
            {
                return;
            }

            this.RunCount.Increment();
        }

        /// <summary>Increments the queue-length counter, if performance counters are available.</summary>
        public void IncrementQueueLength()
        {
            if (!this.enabled)
            {
                return;
            }

            this.CurrentQueueLength.Increment();
        }

        /// <summary>Decrements the queue-length counter, if performance counters are available.</summary>
        public void DecrementQueueLength()
        {
            if (!this.enabled)
            {
                return;
            }

            this.CurrentQueueLength.Decrement();
        }

        private void ResetValues()
        {
            this.CurrentQueueLength.RawValue = 0;
            this.ExecutionTimeTotal.RawValue = 0;
            this.ExecutionTimeAverage.RawValue = 0;
            this.IdleTimePercent.RawValue = 0;
            this.WaitTime.RawValue = 0;
            this.WaitTimeExclusiveLock.RawValue = 0;
            this.WaitTimeSyncLock.RawValue = 0;
            this.WaitTimeAverage.RawValue = 0;
            this.WaitTimeAverageExclusiveLock.RawValue = 0;
            this.WaitTimeAverageSyncLock.RawValue = 0;
            this.RunCount.RawValue = 0;

            this.executionTimeCounts = 0;
            this.executionTimeTotal = new TimeSpan();

            this.waitTimeCounts = 0;
            this.waitTimeTotal = new TimeSpan();

            this.waitTimeSyncCounts = 0;
            this.waitTimeSync = new TimeSpan();

            this.waitTimeExclusiveCounts = 0;
            this.waitTimeExclusive = new TimeSpan();

            this.executionHistory.Clear();

        }

        private static PerformanceCounter CreateCounter(string name, string maName)
        {
            return new PerformanceCounter()
            {
                CategoryName = MAControllerPerfCounters.CategoryName,
                CounterName = name,
                MachineName = ".",
                InstanceLifetime = PerformanceCounterInstanceLifetime.Global,
                InstanceName = maName,
                ReadOnly = false,
            };
        }

        private int executionTimeCounts;
        private TimeSpan executionTimeTotal;

        private List<DateTime> executionHistory;

        public void AddExecutionTime(TimeSpan value)
        {
            if (!this.enabled)
            {
                return;
            }

            this.executionTimeTotal = this.executionTimeTotal.Add(value);
            this.executionTimeCounts++;
            this.executionHistory.Add(DateTime.Now);

            this.UpdateCounters();
        }

        private int waitTimeCounts;
        private TimeSpan waitTimeTotal;

        public void AddWaitTime(TimeSpan value)
        {
            if (!this.enabled)
            {
                return;
            }

            this.waitTimeTotal = this.waitTimeTotal.Add(value);
            this.waitTimeCounts++;
            this.UpdateCounters();
        }

        private int waitTimeSyncCounts;
        private TimeSpan waitTimeSync;

        public void AddWaitTimeSync(TimeSpan value)
        {
            if (!this.enabled)
            {
                return;
            }

            this.waitTimeSync = this.waitTimeSync.Add(value);
            this.waitTimeSyncCounts++;
            this.UpdateCounters();
        }

        private int waitTimeExclusiveCounts;
        private TimeSpan waitTimeExclusive;

        public void AddWaitTimeExclusive(TimeSpan value)
        {
            if (!this.enabled)
            {
                return;
            }

            this.waitTimeExclusive = this.waitTimeExclusive.Add(value);
            this.waitTimeExclusiveCounts++;
            this.UpdateCounters();
        }

        private void UpdateCounters()
        {
            if (!this.enabled)
            {
                return;
            }

            try
            {
                double elapsed = this.activeTimer.Elapsed.TotalSeconds;

                if (elapsed <= 0)
                {
                    return;
                }

                if (this.waitTimeExclusiveCounts > 0)
                {
                    this.WaitTimeAverageExclusiveLock.RawValue = Convert.ToInt64(this.waitTimeExclusive.TotalSeconds / this.waitTimeExclusiveCounts);
                    this.WaitTimeExclusiveLock.RawValue = (long) (this.waitTimeExclusive.TotalSeconds / this.activeTimer.Elapsed.TotalSeconds * 100);
                }

                if (this.waitTimeSyncCounts > 0)
                {
                    this.WaitTimeAverageSyncLock.RawValue = Convert.ToInt64(this.waitTimeSync.TotalSeconds / this.waitTimeSyncCounts);
                    this.WaitTimeSyncLock.RawValue = (long) (this.waitTimeSync.TotalSeconds / this.activeTimer.Elapsed.TotalSeconds * 100);
                }

                if (this.waitTimeCounts > 0)
                {
                    this.WaitTimeAverage.RawValue = Convert.ToInt64(this.waitTimeTotal.TotalSeconds / this.waitTimeCounts);
                    this.WaitTime.RawValue = (long) (this.waitTimeTotal.TotalSeconds / this.activeTimer.Elapsed.TotalSeconds * 100);
                }

                if (this.executionTimeCounts > 0)
                {
                    this.ExecutionTimeAverage.RawValue = Convert.ToInt64(this.executionTimeTotal.TotalSeconds / this.executionTimeCounts);
                    this.ExecutionTimeTotal.RawValue = (long) (this.executionTimeTotal.TotalSeconds / this.activeTimer.Elapsed.TotalSeconds * 100);
                }

                this.IdleTimePercent.RawValue = (long) (((this.activeTimer.Elapsed.TotalSeconds - this.executionTimeTotal.TotalSeconds - this.waitTimeTotal.TotalSeconds) / this.activeTimer.Elapsed.TotalSeconds) * 100);

                this.executionHistory.RemoveAll(t => t < DateTime.Now.AddMinutes(-10));

                this.RunCount.RawValue = this.executionHistory.Count;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Performance counter update failed");
            }
        }
    }
}
