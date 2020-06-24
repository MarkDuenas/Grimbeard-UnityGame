using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace K_PathFinder {
    public abstract class NavMeshQueryAbstract<ResultType> : IThreadPoolWorkBatcherMember {
        protected const int HEAP_VALUE_BASE_LENGTH = 128;

        protected int layerMask;
        protected int costModifyerMask;
        protected AgentProperties properties;
        protected ResultType notThreadSafeResult;
        public ResultType threadSafeResult;
        protected Action<ResultType> recieveDelegate_TS = null;  //thread safe
        protected Action<ResultType> recieveDelegate_NTS = null; //not thread safe
        protected bool queryHaveWork = false;
        protected int maxExecutionTimeInMilliseconds = 5;

        public NavMeshQueryAbstract(AgentProperties Properties) {
            properties = Properties;
        }

        /// <summary>
        /// thread stuff not for public usage
        /// </summary>
        public abstract void PerformWork(object context);

        public void Reset() {
            queryHaveWork = false;
        }

        protected void Finish() {
            lock (this) {
                if (recieveDelegate_NTS != null)
                    recieveDelegate_NTS.Invoke(notThreadSafeResult);
            }
            PathFinder.AddMainThreadDelegate(OnUnityMainThreadFinalize);   
        }

        //threadsafe function to return result in main thread
        abstract protected void OnUnityMainThreadFinalize();
        
        public void SetOnFinishedDelegate(Action<ResultType> onFinishDelegate, AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            switch (mode) {
                case AgentDelegateMode.ThreadSafe:
                    recieveDelegate_TS = onFinishDelegate;
                    break;
                case AgentDelegateMode.NotThreadSafe:
                    lock (this)
                        recieveDelegate_NTS = onFinishDelegate;
                    break;
            }
        }

        public void RemoveOnFinishedDelegate(AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            switch (mode) {
                case AgentDelegateMode.ThreadSafe:
                    recieveDelegate_TS = null;
                    break;
                case AgentDelegateMode.NotThreadSafe:
                    lock (this)
                        recieveDelegate_NTS = null;
                    break;
            }
        }
        
        public void SetMaxExecutionTimeInMilliseconds(int maxExecutionTimeInMilliseconds = 5) {
            lock (this) {
                this.maxExecutionTimeInMilliseconds = maxExecutionTimeInMilliseconds;
            }
        }            

        //generic functions that probably will be need in every instance of querry at some degree 
        protected struct HeapValue {
            public int root, index, depth, connectionGlobalIndex;
            public float globalCost;

            public HeapValue(int Root, int Index, float GlobalCost, int Depth, int ConnectionGlobalIndex) {
                root = Root;
                index = Index;
                globalCost = GlobalCost;
                depth = Depth;
                connectionGlobalIndex = ConnectionGlobalIndex;
            }
        }

        //for direct heap implementation
        protected struct HeapValueSpecialOne {
            public int root, index, depth, connectedCell;
            public float globalCost, heapValue;

            public HeapValueSpecialOne(int Root, int Index, float GlobalCost, float HeapValue, int Depth, int ConnectedCell) {
                root = Root;
                index = Index;
                globalCost = GlobalCost;
                heapValue = HeapValue;
                depth = Depth;
                connectedCell = ConnectedCell;
            }
        }
        
        protected static float GetHeuristic(Vector3 a, Vector3 b) {
            return (float)Math.Sqrt(
                ((b.x - a.x) * (b.x - a.x)) +
                ((b.y - a.y) * (b.y - a.y)) +
                ((b.z - a.z) * (b.z - a.z)));
        }  
    }
}