using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ActionQueue<T> where T : ExecutionParameters
    {
        private List<T> queue;
        private readonly SemaphoreSlim itemNotification;
        private readonly CancellationTokenSource completeAddingCancellationToken;
        private bool complete;
        private object lockItem;

        public ActionQueue()
        {
            this.complete = false;
            this.lockItem = new object();
            this.queue = new List<T>();
            this.itemNotification = new SemaphoreSlim(0);
            this.completeAddingCancellationToken = new CancellationTokenSource();
        }

        public bool Add(T item)
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            lock (this.lockItem)
            {
                if (this.queue.Contains(item))
                {
                    if (item.RunImmediate)
                    {
                        if (this.queue.Count == 1)
                        {
                            return false;
                        }

                        this.queue.Remove(item);
                        this.queue.Insert(0, item);
                    }

                    return false;
                }
                else
                {
                    if (item.RunImmediate)
                    {
                        this.queue.Insert(0, item);
                    }
                    else
                    {
                        this.queue.Add(item);
                    }
                }
            }

            this.itemNotification.Release();
            return true;
        }

        public void CompleteAdding()
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            this.complete = true;
            this.completeAddingCancellationToken.Cancel();
        }

        public IEnumerable<T> Consume(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                this.itemNotification.Wait(CancellationTokenSource.CreateLinkedTokenSource(token, this.completeAddingCancellationToken.Token).Token);

                if (this.complete || token.IsCancellationRequested)
                {
                    yield break;
                }

                T item = null;

                lock (this.lockItem)
                {
                    if (this.queue.Count > 0)
                    {
                        item = this.queue[0];
                        this.queue.RemoveAt(0);
                    }
                }

                if (item != null)
                {
                    yield return item;
                }
            }
        }

        public override string ToString()
        {
            lock (this.lockItem)
            {
                return string.Join(", ", this.queue.Select(t => t.RunProfileName)
                );
            }
        }
    }
}
