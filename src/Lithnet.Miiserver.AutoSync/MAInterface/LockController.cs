using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NLog;

namespace Lithnet.Miiserver.AutoSync
{
    internal static class LockController
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static void Debug(string managementAgentName, string message)
        {
#if LOCKDEBUG
            logger.Trace($"{managementAgentName}: {message}");
#endif
        }

        public static void Wait(TimeSpan duration, string name, CancellationTokenSource ts, string managementAgentName, [CallerMemberName] string caller = "")
        {
            ts.Token.ThrowIfCancellationRequested();
            LockController.Debug(managementAgentName, $"SLEEP: {name}: {duration}: {caller}");
            ts.Token.WaitHandle.WaitOne(duration);
            ts.Token.ThrowIfCancellationRequested();
        }

        public static void Wait(WaitHandle wh, string name, CancellationTokenSource ts, string managementAgentName, [CallerMemberName] string caller = "")
        {
            LockController.Debug(managementAgentName, $"LOCK: WAIT: {name}: {caller}");
            WaitHandle.WaitAny(new[] { wh, ts.Token.WaitHandle });
            ts.Token.ThrowIfCancellationRequested();
            LockController.Debug(managementAgentName, $"LOCK: CLEARED: {name}: {caller}");
        }

        public static void WaitAndTakeLock(SemaphoreSlim mre, string name, CancellationTokenSource ts, string managementAgentName, [CallerMemberName] string caller = "")
        {
            LockController.Debug(managementAgentName, $"LOCK: WAIT: {name}: {caller}");
            mre.Wait(ts.Token);
            ts.Token.ThrowIfCancellationRequested();
            LockController.Debug(managementAgentName, $"LOCK: TAKE: {name}: {caller}");
        }

        public static void WaitAndTakeLockWithSemaphore(EventWaitHandle mre, SemaphoreSlim sem, string name, CancellationTokenSource ts, string managementAgentName, [CallerMemberName] string caller = "")
        {
            bool gotLock = false;

            try
            {
                LockController.Debug(managementAgentName, $"SYNCOBJECT: WAIT: {name}: {caller}");
                sem.Wait(ts.Token);
                ts.Token.ThrowIfCancellationRequested();
                gotLock = true;
                LockController.Debug(managementAgentName, $"SYNCOBJECT: LOCKED: {name}: {caller}");
                LockController.Wait(mre, name, ts, managementAgentName);
                LockController.TakeLockUnsafe(mre, name, ts, managementAgentName, caller);
                ts.Token.ThrowIfCancellationRequested();
            }
            finally
            {
                if (gotLock)
                {
                    sem.Release();
                    LockController.Debug(managementAgentName, $"SYNCOBJECT: UNLOCKED: {name}: {caller}");
                }
            }
        }

        public static void Wait(WaitHandle[] waitHandles, string name, CancellationTokenSource ts, string managementAgentName, [CallerMemberName] string caller = "")
        {
            LockController.Debug(managementAgentName, $"LOCK: WAIT: {name}: {caller}");

            if (waitHandles != null && waitHandles.Length > 0)
            {
                while (!WaitHandle.WaitAll(waitHandles, 1000))
                {
                    ts.Token.ThrowIfCancellationRequested();
                }
            }

            ts.Token.ThrowIfCancellationRequested();
            LockController.Debug(managementAgentName, $"LOCK: CLEARED: {name}: {caller}");
        }

        public static void TakeLockUnsafe(EventWaitHandle mre, string name, CancellationTokenSource ts, string managementAgentName, string caller)
        {
            LockController.Debug(managementAgentName, $"LOCK: TAKE: {name}: {caller}");
            mre.Reset();
            ts.Token.ThrowIfCancellationRequested();
        }

        public static void ReleaseLock(EventWaitHandle mre, string name, string managementAgentName, [CallerMemberName] string caller = "")
        {
            LockController.Debug(managementAgentName, $"LOCK: RELEASE: {name}: {caller}");
            mre.Set();
        }

        public static void ReleaseLock(SemaphoreSlim mre, string name, string managementAgentName, [CallerMemberName] string caller = "")
        {
            LockController.Debug(managementAgentName, $"LOCK: RELEASE: {name}: {caller}");
            mre.Release();
        }
    }
}
