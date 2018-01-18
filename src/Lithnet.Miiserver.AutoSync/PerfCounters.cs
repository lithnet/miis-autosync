using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Lithnet.Miiserver.AutoSync
{
    internal class MAControllerPerfCounters
    {
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
            this.RunCount = MAControllerPerfCounters.CreateCounter("Total run count", maName);
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

            this.activeTimer = new Stopwatch();
            this.timer = new Timer();
            this.timer.Interval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            this.timer.Elapsed += this.Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.UpdateCounters();
        }

        public void Stop()
        {
            this.timer?.Stop();
            this.ResetValues();
        }

        public void Start()
        {
            this.activeTimer.Restart();
            this.ResetValues();
            this.timer.Start();
        }

        private void ResetValues()
        {
            this.CurrentQueueLength.RawValue = 0;
            this.ExecutionTimeTotal.RawValue = 0;
            this.ExecutionTimeAverage.RawValue = 0;
            this.WaitTime.RawValue = 0;
            this.WaitTimeExclusiveLock.RawValue = 0;
            this.WaitTimeSyncLock.RawValue = 0;
            this.WaitTimeAverage.RawValue = 0;
            this.WaitTimeAverageExclusiveLock.RawValue = 0;
            this.WaitTimeAverageSyncLock.RawValue = 0;
            this.RunCount.RawValue = 0;
        }

        private static PerformanceCounter CreateCounter(string name, string maName)
        {
            return new PerformanceCounter()
            {
                CategoryName = "Lithnet AutoSync",
                CounterName = name,
                MachineName = ".",
                InstanceLifetime = PerformanceCounterInstanceLifetime.Global,
                InstanceName = maName,
                ReadOnly = false,
            };
        }

        private int executionTimeCounts;

        private TimeSpan executionTimeTotal;

        public void AddExecutionTime(TimeSpan value)
        {
            this.executionTimeTotal = this.executionTimeTotal.Add(value);
            this.executionTimeCounts++;
            this.UpdateCounters();
        }

        private int waitTimeCounts;
        private TimeSpan waitTimeTotal;

        public void AddWaitTime(TimeSpan value)
        {
            this.waitTimeTotal = this.waitTimeTotal.Add(value);
            this.waitTimeCounts++;
            this.UpdateCounters();
        }

        private int waitTimeSyncCounts;
        private TimeSpan waitTimeSync;

        public void AddWaitTimeSync(TimeSpan value)
        {
            this.waitTimeSync = this.waitTimeSync.Add(value);
            this.waitTimeSyncCounts++;
            this.UpdateCounters();
        }

        private int waitTimeExclusiveCounts;
        private TimeSpan waitTimeExclusive;

        public void AddWaitTimeExclusive(TimeSpan value)
        {
            this.waitTimeExclusive = this.waitTimeExclusive.Add(value);
            this.waitTimeExclusiveCounts++;
            this.UpdateCounters();
        }

        private void UpdateCounters()
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

            this.IdleTimePercent.RawValue = (long)(((this.activeTimer.Elapsed.TotalSeconds - this.executionTimeTotal.TotalSeconds - this.waitTimeTotal.TotalSeconds) / this.activeTimer.Elapsed.TotalSeconds) * 100);
        }
    }
}