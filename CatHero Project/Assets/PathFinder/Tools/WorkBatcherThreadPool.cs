using K_PathFinder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace K_PathFinder.PFTools {
    public interface IThreadPoolWorkBatcherBeforeNavmeshPosition {
        void OnBeforeNavmeshPositionUpdate();
    }

    public interface IThreadPoolWorkBatcherMember {
        void PerformWork(object context);
        void Reset();
    }

    /// <summary>
    /// generic class for grouping bunch of work in threads
    /// </summary>
    public class ThreadPoolWorkBatcher<T> where T : IThreadPoolWorkBatcherMember {
        object locker = new object();
        List<WorkValue> batch0 = new List<WorkValue>();
        List<WorkValue> batch1 = new List<WorkValue>();
        bool curBatch = false;
        ManualResetEvent[] eventPool;

        public void AddWork(T work, object context) {
            lock (locker) 
                (curBatch ? batch1 : batch0).Add(new WorkValue(work, context));            
        }

        public bool haveWork {
            get { return workCount > 0; }            
        }

        public int workCount {
            get {lock (locker) return (curBatch ? batch1 : batch0).Count;}
        }

        public void ClearAndReset() {
            lock (locker) {
                foreach (var item in batch0) { item.obj.Reset(); }
                batch0.Clear();

                foreach (var item in batch1) { item.obj.Reset(); }
                batch1.Clear();
            }
        }
        
        //lock out current work batch
        public void SwichBatch() {
            lock (locker) {
                curBatch = !curBatch;
            }
        }

        //it berformed on OPPOSITE batch. so call SwichBatch() before you call this function so all new work go to different batch after
        public int PerformBeforeNavmeshPositionUpdate() {
            List<WorkValue> batch;     

            lock (locker) {
                batch = curBatch ? batch0 : batch1;
            }

            foreach (var wv in batch) {
                if (wv.obj is IThreadPoolWorkBatcherBeforeNavmeshPosition)
                   (wv.obj as IThreadPoolWorkBatcherBeforeNavmeshPosition).OnBeforeNavmeshPositionUpdate();
            }

            return batch.Count;
        }

        //it berformed on OPPOSITE batch. so call SwichBatch() before you call this function so all new work go to different batch after
        public int PerformCurrentBatchThreadSafe() {
            List<WorkValue> batch;

            lock (locker) {
                batch = curBatch ? batch0 : batch1;
            }

            foreach (var wv in batch) {
                wv.obj.PerformWork(wv.context);
            }

            int wc = batch.Count;
            batch.Clear();
            return wc;
        }

        //it berformed on OPPOSITE batch. so call SwichBatch() before you call this function so all new work go to different batch after
        public int PerformCurrentBatch(int maxThreads) {
            List<WorkValue> batch;
            int workCount;

            lock (locker) {
                batch = curBatch ? batch0 : batch1;
            }

            workCount = batch.Count;

            if (eventPool == null || eventPool.Length != maxThreads) {
                eventPool = new ManualResetEvent[maxThreads];
                for (int i = 0; i < maxThreads; i++) {
                    eventPool[i] = new ManualResetEvent(true);
                }
            }

            int curIndex = 0;
            int worPerThread = (workCount / maxThreads) + 1;

            for (int i = 0; i < maxThreads; i++) {
                int end = curIndex + worPerThread;

                if (end >= workCount) {
                    end = workCount;
                    eventPool[i].Reset();
                    ThreadPool.QueueUserWorkItem(ThreadPoolCallbackLimited, new ThreadPoolContextLimited(eventPool[i], batch, curIndex, end));
                    break;
                }
                else {
                    eventPool[i].Reset();
                    ThreadPool.QueueUserWorkItem(ThreadPoolCallbackLimited, new ThreadPoolContextLimited(eventPool[i], batch, curIndex, end));
                }

                curIndex = end;
            }

            WaitHandle.WaitAll(eventPool);
            batch.Clear();
            return workCount;
        }

        //callbacks
        protected virtual void ThreadPoolCallbackLimited(object context) {  
            try {
                ThreadPoolContextLimited threadContext = (ThreadPoolContextLimited)context;
                List<WorkValue> workBatch = threadContext.workBatch;

                for (int i = threadContext.threadStart; i < threadContext.threadEnd; i++) {
                    workBatch[i].obj.PerformWork(workBatch[i].context);
                }

                threadContext.mre.Set();
            }
            catch (Exception e) {
                UnityEngine.Debug.LogError(e);
                throw;
            }
        }
        protected virtual void ThreadPoolCallbackSimple(object context) {
            try {
                ThreadPoolContextSimple threadContext = (ThreadPoolContextSimple)context;
                threadContext.obj.PerformWork(threadContext.context);
                threadContext.mre.Set();
            }
            catch (Exception e) {
                UnityEngine.Debug.LogError(e);
                throw;
            }
        }

        public override string ToString() {
            lock(locker)
                return string.Format("batch: {0}, true batch count {1}, false batc count {2}", curBatch, batch1.Count, batch0);
        }


        //structs
        protected struct ThreadPoolContextLimited {
            public readonly ManualResetEvent mre;
            public readonly int threadStart, threadEnd;
            public readonly List<WorkValue> workBatch;

            public ThreadPoolContextLimited(ManualResetEvent MRE, List<WorkValue> batch, int start, int end) {
                mre = MRE;
                threadStart = start;
                threadEnd = end;
                workBatch = batch;
            }
        }
        protected struct ThreadPoolContextSimple {
            public readonly ManualResetEvent mre;
            public readonly object context;
            public readonly T obj;

            public ThreadPoolContextSimple(WorkValue workValue, ManualResetEvent MRE) {
                obj = workValue.obj;
                context = workValue.context;
                mre = MRE;           
            }
        }
        protected struct WorkValue {
            public readonly object context;
            public readonly T obj;

            public WorkValue(T Obj, object Context) {
                obj = Obj;
                context = Context;
            }
        }
    }
    
    public class LockedWorkDictionary<Key, Value> {
        object locker = new object();
        Dictionary<Key, Value> batch0 = new Dictionary<Key, Value>();
        Dictionary<Key, Value> batch1 = new Dictionary<Key, Value>();
        bool curBatch = false;
        
        public Value this[Key key] {
            set {
                lock (locker) {
                    if (curBatch)
                        batch1[key] = value;
                    else
                        batch0[key] = value;
                }
            }
        }

        public bool haveWork {
            get { return workCount > 0; }
        }

        public int workCount {
            get {
                lock (locker) {
                    if (curBatch)
                        return batch1.Count;
                    else
                        return batch0.Count;
                }
            }
        }

        public void Clear() {
            lock (locker) {
                batch0.Clear();
                batch1.Clear();
            }
        }

        public Dictionary<Key, Value> GetCurrentBatch() {
            Dictionary<Key, Value> batch;

            lock (locker) {
                batch = curBatch ? batch1 : batch0;
                curBatch = !curBatch;
            }

            return batch;
        }
    }

    /// <summary>
    /// simple toy to have to stagger thread work
    /// </summary>
    public class WorkBatcher<T> {
        object locker = new object();
        Queue<T> batch0 = new Queue<T>();
        Queue<T> batch1 = new Queue<T>();
        bool curBatch = false;

        public void Add(T val) {
            lock (locker) {
                if (curBatch)
                    batch1.Enqueue(val);
                else
                    batch0.Enqueue(val);
            }
        }

        public Queue<T> currentBatch {
            get {
                lock (locker) {
                    if (curBatch)
                        return batch1;
                    else
                        return batch0;
                }
            }
        }


        public void Flip() {
            lock (locker) {
                curBatch = !curBatch;
            }
        }

        public bool haveWork {
            get { return workCount > 0; }
        }

        public int workCount {
            get {
                lock (locker) {
                    if (curBatch)
                        return batch1.Count;
                    else
                        return batch0.Count;
                }
            }
        }

        public void Clear() {
            lock (locker) {
                batch0.Clear();
                batch1.Clear();
            }
        }
    }
}
