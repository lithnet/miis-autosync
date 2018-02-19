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

        private Stopwatch activeTimer;

        private Timer timer;

        public PerformanceCounter RunCount { get; }

        public PerformanceCounter WaitTime { get; }
        
        public PerformanceCounter WaitTimeAverage { get; }

        private PerformanceCounter ExecutionTimeAverage { get; }

        public PerformanceCounter ExecutionTimeTotal { get; }

        public PerformanceCounter IdleTimePercent { get; }

        public PerformanceCounter CurrentQueueLength { get; }

        public static void MeasureCommand(Action command, Action<TimeSpan> addElapsed)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            command();
            stopwatch.Stop();
            addElapsed(stopwatch.Elapsed);
        }

        public MAControllerPerfCounters(string maName)
        {
            this.RunCount = MAControllerPerfCounters.CreateCounter("Runs/10 min", maName);
            this.WaitTimeAverage = MAControllerPerfCounters.CreateCounter("Wait time average", maName);
            this.WaitTime = MAControllerPerfCounters.CreateCounter("Wait time %", maName);
            this.ExecutionTimeAverage = MAControllerPerfCounters.CreateCounter("Execution time average", maName);
            this.ExecutionTimeTotal = MAControllerPerfCounters.CreateCounter("Execution time %", maName);
            this.CurrentQueueLength = MAControllerPerfCounters.CreateCounter("Queue length", maName);
            this.IdleTimePercent = MAControllerPerfCounters.CreateCounter("Idle time %", maName);

            this.activeTimer = new Stopwatch();
            this.timer = new Timer();
            this.timer.Interval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            this.timer.Elapsed += this.Timer_Elapsed;
            this.executionHistory = new List<DateTime>();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.UpdateCounters();
        }

        public void Stop()
        {
            this.timer?.Stop();
            this.activeTimer?.Stop();
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
            this.IdleTimePercent.RawValue = 0;
            this.WaitTime.RawValue = 0;
            this.WaitTimeAverage.RawValue = 0;
            this.RunCount.RawValue = 0;

            this.executionTimeCounts = 0;
            this.executionTimeTotal = new TimeSpan();

            this.waitTimeCounts = 0;
            this.waitTimeTotal = new TimeSpan();

            this.executionHistory.Clear();
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

        private List<DateTime> executionHistory;

        public void AddExecutionTime(TimeSpan value)
        {
            this.executionTimeTotal = this.executionTimeTotal.Add(value);
            this.executionTimeCounts++;
            this.executionHistory.Add(DateTime.Now);

            this.UpdateCounters();
        }

        private int waitTimeCounts;
        private TimeSpan waitTimeTotal;

        public void AddWaitTimeTotal(TimeSpan value)
        {
            this.waitTimeTotal = this.waitTimeTotal.Add(value);
            this.waitTimeCounts++;
            this.UpdateCounters();
        }

        private void UpdateCounters()
        {
            try
            {
                double elapsed = this.activeTimer.Elapsed.TotalSeconds;

                if (elapsed <= 0)
                {
                    return;
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