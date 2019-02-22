using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ActionQueue<T>
    {
        private List<T> queue;
        private readonly SemaphoreSlim itemNotification;
        private readonly SemaphoreSlim completeAdding;
        private bool complete;
        private object lockItem;

        public ActionQueue()
        {
            this.complete = false;
            this.lockItem = new object();
            this.queue = new List<T>();
            this.itemNotification = new SemaphoreSlim(0);
            this.completeAdding = new SemaphoreSlim(0);
        }

        public void Add(T item)
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            lock (this.lockItem)
            {
                this.queue.Add(item);
            }

            this.itemNotification.Release();
        }

        public void AddToFront(T item)
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            lock (this.lockItem)
            {
                this.queue.Insert(0, item);
            }

            this.itemNotification.Release();
        }

        public void MoveToFront(T item)
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            lock (this.lockItem)
            {
                this.queue.Remove(item);
                this.queue.Insert(0, item);
            }
        }

        public bool Contains(T item)
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            lock (this.lockItem)
            {
                return this.queue.Contains(item);
            }
        }

        public bool MoveToFrontIfExists(T item)
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            lock (this.lockItem)
            {
                if (this.queue.Contains(item))
                {
                    if (this.queue.Count == 1)
                    {
                        return false;
                    }

                    this.queue.Remove(item);
                    this.queue.Insert(0, item);
                    return true;
                }
            }

            return false;
        }

        public void CompleteAdding()
        {
            if (this.complete)
            {
                throw new InvalidOperationException();
            }

            this.complete = true;
            this.completeAdding.Release();
        }

        public IEnumerable<T> Consume(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                WaitHandle.WaitAny(new[] { this.itemNotification.AvailableWaitHandle, this.completeAdding.AvailableWaitHandle, token.WaitHandle });

                if (this.complete || token.IsCancellationRequested)
                {
                    yield break;
                }

                lock (this.lockItem)
                {
                    T item = this.queue[0];
                    this.queue.RemoveAt(0);
                    yield return item;
                }
            }
        }

        public T[] ToArray()
        {
            lock (this.lockItem)
            {
                return this.queue.ToArray();
            }
        }
    }
}
