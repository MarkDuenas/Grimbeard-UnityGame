using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Threading;
using K_PathFinder.CoolTools;
using K_PathFinder.Graphs;
using K_PathFinder.Pool;
#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif

//this part of code batch searching on navmesh to single multipthreaded operation
//not realy helping to achive something very userful (yet) but simplify some code related to searching
//it working this way:
//you pass to RegisterNavmeshSample properties and it return int
//that int is index of request in navmeshPositionRequests
//there you can directly change position and is this point is valid
//if point is valid on next searching iteration on same index in navmeshPositionResults
//result will appear
//dont forget to return no longer used indexes
//not realy threadsafe in any way and should be used only in PathFinder main thread
namespace K_PathFinder {
    //TODO:
    //maybe make positions requests batched with properties? cause lookups in dictionary still takes time

    public enum NavmeshSampleResultType : int {
        InvalidNoNavmeshFound = -1,
        OutsideNavmesh = 0,
        InvalidByLayerMask = 1,
        InsideNavmesh = 2
    }

    public struct NavmeshSampleResult {
        public float 
            positionX, positionY, positionZ,
            originX, originY, originZ;

        public Cell cell;
        public NavmeshSampleResultType type;

        public Vector3 position {
            get { return new Vector3(positionX, positionY, positionZ); }
        }
        public Vector3 origin {
            get { return new Vector3(originX, originY, originZ); }
        }
    }
    public struct NavmeshSampleResult_Internal {
        public float
            positionX, positionY, positionZ,
            originX, originY, originZ;

        public int cellGlobalID;
        public NavmeshSampleResultType type;

        public NavmeshSampleResult GetResult(Cell cell) {
            NavmeshSampleResult result;
            result.positionX = positionX;
            result.positionY = positionY;
            result.positionZ = positionZ;
            result.originX = originX;
            result.originY = originY;
            result.originZ = originZ;
            result.type = type;
            result.cell = cell;
            return result;
        }

        public Vector3 position {
            get { return new Vector3(positionX, positionY, positionZ); }
        }
        public Vector3 origin {
            get { return new Vector3(originX, originY, originZ); }
        }
    }

    public struct NavmeshSamplePosition {
        public float x, y, z;
        public AgentProperties properties;
        public int layerMask;
        public bool valid;//flag that tells "it need to be updated" if true and become false after updated
        
        public void Set(AgentProperties prop, float x, float y, float z, int layerMask, bool set) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.layerMask = layerMask;
            properties = prop;
            valid = set;
        }

        public void Set(float x, float y, float z, int layerMask, bool set) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.layerMask = layerMask;
            valid = set;
        }

        public void Set(Vector3 pos, int layerMask, bool set) {
            x = pos.x;
            y = pos.y;
            z = pos.z;
            this.layerMask = layerMask;
            valid = set;
        }
    }


    //here some sample positions can be registered for internal usage
    public static partial class PathFinder {
        private const int NAVMESH_SAMPLE_NITIAL_SIZE = 8;
        private static int lastTimeNavmeshPositionsSampled = 0;

        private static NavmeshSamplePosition[] navmeshPositionRequests = new NavmeshSamplePosition[NAVMESH_SAMPLE_NITIAL_SIZE];
        private static int navmeshPositionRequestsCount = 0;

        public static NavmeshSampleResult_Internal[] navmeshPositionResults = new NavmeshSampleResult_Internal[NAVMESH_SAMPLE_NITIAL_SIZE];  

        private static int[] navmeshPositionFreeIndexStack = new int[NAVMESH_SAMPLE_NITIAL_SIZE];
        private static int navmeshPositionFreeIndexStackength = 0;
        
        //update agent navmesh position
        private static ManualResetEvent[] _samplePositionEvents;
        private struct SamplePositionThreadInfo {
            public int threadStart, threadEnd;
            public ManualResetEvent manualResetEvent;
            public SamplePositionThreadInfo(int start, int end, ManualResetEvent resetEvent) {
                threadStart = start;
                threadEnd = end;
                manualResetEvent = resetEvent;
            }
        }

        static void ClearPositionSampling() {
            navmeshPositionRequestsCount = 0;
            navmeshPositionFreeIndexStackength = 0;

            for (int i = 0; i < navmeshPositionRequests.Length; i++) {
                navmeshPositionRequests[i].valid = false;
            }

            for (int i = 0; i < navmeshPositionResults.Length; i++) {
                navmeshPositionResults[i].cellGlobalID = -1;
                navmeshPositionResults[i].type = NavmeshSampleResultType.OutsideNavmesh;
            }
        }

        public static int RegisterNavmeshSample(AgentProperties properties) {
            int result;
            if(navmeshPositionFreeIndexStackength > 0) {
                result= navmeshPositionFreeIndexStack[--navmeshPositionFreeIndexStackength];
            }
            else {
                result = navmeshPositionRequestsCount++;
                if (result == navmeshPositionRequests.Length) {
                    int oldLength = navmeshPositionRequests.Length;

                    NavmeshSamplePosition[] newSamplesPosition = new NavmeshSamplePosition[oldLength * 2];
                    Array.Copy(navmeshPositionRequests, newSamplesPosition, oldLength);
                    navmeshPositionRequests = newSamplesPosition;

                    NavmeshSampleResult_Internal[] newPositionResults = new NavmeshSampleResult_Internal[oldLength * 2];
                    Array.Copy(navmeshPositionResults, newPositionResults, oldLength);
                    navmeshPositionResults = newPositionResults;
                }              
            }
            navmeshPositionRequests[result].properties = properties;
            return result;
        }

        /// <summary>
        /// if set == true then it automaticaly eligable for next search
        /// </summary>
        public static int RegisterNavmeshSample(AgentProperties properties, float x, float y, float z, int layerMask, bool set) {
            int result;
            if (navmeshPositionFreeIndexStackength > 0) {
                result = navmeshPositionFreeIndexStack[--navmeshPositionFreeIndexStackength];
            }
            else {
                result = navmeshPositionRequestsCount++;
                if (result == navmeshPositionRequests.Length) {
                    int oldLength = navmeshPositionRequests.Length;

                    NavmeshSamplePosition[] newSamplesPosition = new NavmeshSamplePosition[oldLength * 2];
                    Array.Copy(navmeshPositionRequests, newSamplesPosition, oldLength);
                    navmeshPositionRequests = newSamplesPosition;

                    NavmeshSampleResult_Internal[] newPositionResults = new NavmeshSampleResult_Internal[oldLength * 2];
                    Array.Copy(navmeshPositionResults, newPositionResults, oldLength);
                    navmeshPositionResults = newPositionResults;
                }
            }
            navmeshPositionRequests[result].Set(properties, x, y, z, layerMask, set);
            return result;
        }
        
        /// <summary>
        /// if set == true then it automaticaly eligable for next search
        /// </summary>
        public static int RegisterNavmeshSample(AgentProperties properties, Vector3 position, int layerMask, bool set) {
            int result;
            if (navmeshPositionFreeIndexStackength > 0) {
                result = navmeshPositionFreeIndexStack[--navmeshPositionFreeIndexStackength];
            }
            else {
                result = navmeshPositionRequestsCount++;
                if (result == navmeshPositionRequests.Length) {
                    int oldLength = navmeshPositionRequests.Length;

                    NavmeshSamplePosition[] newSamplesPosition = new NavmeshSamplePosition[oldLength * 2];
                    Array.Copy(navmeshPositionRequests, newSamplesPosition, oldLength);
                    navmeshPositionRequests = newSamplesPosition;

                    NavmeshSampleResult_Internal[] newPositionResults = new NavmeshSampleResult_Internal[oldLength * 2];
                    Array.Copy(navmeshPositionResults, newPositionResults, oldLength);
                    navmeshPositionResults = newPositionResults;
                }
            }
            navmeshPositionRequests[result].Set(properties, position.x, position.y, position.z, layerMask, set);
            return result;
        }
        
        public static void UnregisterNavmeshSample(int id) {
            if (navmeshPositionFreeIndexStack.Length == navmeshPositionFreeIndexStackength)
                GenericPoolArray<int>.IncreaseSize(ref navmeshPositionFreeIndexStack);
            navmeshPositionRequests[id].valid = false;
            navmeshPositionFreeIndexStack[navmeshPositionFreeIndexStackength++] = id;
        }

        public static void UnregisterNavmeshSample(int[] ids) {
            UnregisterNavmeshSample(ids, ids.Length);
        }

        public static void UnregisterNavmeshSample(int[] ids, int idsLength) {
            for (int i = 0; i < idsLength; i++) {
                int id = ids[i];
                navmeshPositionRequests[id].valid = false;
                if (navmeshPositionFreeIndexStack.Length == navmeshPositionFreeIndexStackength)
                    GenericPoolArray<int>.IncreaseSize(ref navmeshPositionFreeIndexStack);

                navmeshPositionFreeIndexStack[navmeshPositionFreeIndexStackength++] = id;
            }
        }

        public static NavmeshSampleResult_Internal UnregisterNavmeshSampleAndReturnResult(int id) {
            navmeshPositionRequests[id].valid = false;
            if (navmeshPositionFreeIndexStack.Length == navmeshPositionFreeIndexStackength)
                GenericPoolArray<int>.IncreaseSize(ref navmeshPositionFreeIndexStack);
            navmeshPositionFreeIndexStack[navmeshPositionFreeIndexStackength++] = id;
            return navmeshPositionResults[id];
        }

        /// <summary>
        /// if threads == 0 then it is current thread and make it single threaded
        /// return how much positions was updated
        /// </summary>
        private static void UpdatePositionSamples(int threads) {
            lastTimeNavmeshPositionsSampled = 0;
            if (threads > 0) {
                if (_samplePositionEvents == null || _samplePositionEvents.Length != threads) {
                    _samplePositionEvents = new ManualResetEvent[threads];
                    for (int i = 0; i < settings.maxThreads; i++) {
                        _samplePositionEvents[i] = new ManualResetEvent(true);
                    }
                }

                int curIndex = 0;
                int agentsPerThread = (navmeshPositionRequestsCount / threads) + 1;

                for (int i = 0; i < threads; i++) {
                    int end = curIndex + agentsPerThread;

                    if (end >= navmeshPositionRequestsCount) {                  
                        _samplePositionEvents[i].Reset();
                        ThreadPool.QueueUserWorkItem(SamplePositionThreadFunction, new SamplePositionThreadInfo(curIndex, navmeshPositionRequestsCount, _samplePositionEvents[i]));
                        break;
                    }
                    else {
                        _samplePositionEvents[i].Reset();
                        ThreadPool.QueueUserWorkItem(SamplePositionThreadFunction, new SamplePositionThreadInfo(curIndex, end, _samplePositionEvents[i]));
                    }

                    curIndex = end;
                }

                WaitHandle.WaitAll(_samplePositionEvents);
            }
            else {
                SamplePositionThreadFunction(new SamplePositionThreadInfo(0, navmeshPositionRequestsCount, null));
            }
        }

        private static void SamplePositionThreadFunction(System.Object threadContext) {
            try {
                SamplePositionThreadInfo contex = (SamplePositionThreadInfo)threadContext;

                for (int i = contex.threadStart; i < contex.threadEnd; i++) {
                    NavmeshSamplePosition pos = navmeshPositionRequests[i];                             

                    if (pos.valid) {
                        navmeshPositionResults[i].originX = pos.x;
                        navmeshPositionResults[i].originY = pos.y;
                        navmeshPositionResults[i].originZ = pos.z;
                        Cell resultCell;
                        Vector3 resultPos;
                        navmeshPositionResults[i].type = TryGetClosestCell(pos.x, pos.y, pos.z,pos.properties, out resultPos, out resultCell, layerMask: pos.layerMask);
                        navmeshPositionResults[i].positionX = resultPos.x;
                        navmeshPositionResults[i].positionY = resultPos.y;
                        navmeshPositionResults[i].positionZ = resultPos.z;
                        navmeshPositionResults[i].cellGlobalID = resultCell == null ? -1 : resultCell.globalID;
                    }

                    navmeshPositionRequests[i].valid = false;
                    lastTimeNavmeshPositionsSampled++;
                }
                if (contex.manualResetEvent != null)
                    contex.manualResetEvent.Set();
            }
            catch (Exception e) {
                Debug.LogErrorFormat("Error occured while trying to find nearest point on navmesh for agent: {0}", e);
                throw;
            }
        }
    }
}
