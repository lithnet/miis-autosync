using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Lithnet.Miiserver.AutoSync
{
    internal class ExecutionParameterCollection : IProducerConsumerCollection<ExecutionParameters>
    {
        private List<ExecutionParameters> internalList;

        public ExecutionParameterCollection()
        {
            this.internalList = new List<ExecutionParameters>();
        }

        public IEnumerator<ExecutionParameters> GetEnumerator()
        {
            lock (this.SyncRoot)
            {
                return this.internalList.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            lock (this.SyncRoot)
            {
                this.internalList.ToArray().CopyTo(array, index);
            }
        }

        public int Count
        {
            get
            {
                lock (this.SyncRoot)
                {
                    return this.internalList.Count;
                }
            }
        }

        public object SyncRoot { get; } = new object();

        public bool IsSynchronized { get; } = true;

        public void CopyTo(ExecutionParameters[] array, int index)
        {
            lock (this.SyncRoot)
            {
                this.internalList.CopyTo(array, index);
            }
        }

        public void MoveToFront(ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                this.internalList.Remove(item);
                this.internalList.Insert(0, item);
            }
        }

        public bool TryAdd(ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                this.internalList.Add(item);
            }

            return true;
        }

        public bool Contains(ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                return this.internalList.Contains(item);
            }
        }

        public bool TryTake(out ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                if (this.internalList.Count == 0)
                {
                    item = null;
                    return false;
                }

                item = this.internalList[0];
                this.internalList.RemoveAt(0);
                return true;
            }
        }

        public void Remove(ExecutionParameters item)
        {
            lock (this.SyncRoot)
            {
                this.internalList.Remove(item);
            }
        }

        public ExecutionParameters[] ToArray()
        {
            lock (this.SyncRoot)
            {
                return this.internalList.ToArray();
            }
        }
    }
}
