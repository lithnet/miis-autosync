using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionParameterCollection : IProducerConsumerCollection<ExecutionParameters>
    {
        private List<ExecutionParameters> items;

        public ExecutionParameterCollection()
        {
            this.items = new List<ExecutionParameters>();
        }

        public IEnumerator<ExecutionParameters> GetEnumerator()
        {
            return this.items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            lock (this.SyncRoot)
            {
                this.items.ToArray().CopyTo(array, index);
            }
        }

        public int Count
        {
            get
            {
                lock (this.SyncRoot)
                {
                    return this.items.Count;
                }
            }
        }

        public object SyncRoot { get; } = new object();

        public bool IsSynchronized { get; } = true;

        public void CopyTo(ExecutionParameters[] array, int index)
        {
            lock (this.SyncRoot)
            {
                this.items.CopyTo(array, index);
            }
        }

        public void MoveToFront(ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                this.items.Remove(item);
                this.items.Insert(0, item);
            }
        }

        public bool TryAdd(ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                this.items.Add(item);
            }

            return true;
        }

        public bool TryTake(out ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                if (this.items.Count == 0)
                {
                    item = null;
                    return false;
                }

                item = this.items[0];
                this.items.RemoveAt(0);
                return true;
            }
        }

        public void Remove(ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                this.items.Remove(item);
            }
        }

        public ExecutionParameters[] ToArray()
        {
            lock (this.SyncRoot)
            {
                return this.items.ToArray();
            }
        }
    }
}
