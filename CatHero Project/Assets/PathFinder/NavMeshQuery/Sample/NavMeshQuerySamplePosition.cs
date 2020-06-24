using K_PathFinder.Graphs;
using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    /// <summary>
    /// query that sample navmesh positions in PathFinder thread. use this to sample positions if you constantly rebuilding navmesh
    /// </summary>
    public class NavMeshQuerySample : NavMeshQueryAbstract<List<NavmeshSampleResult>>, IThreadPoolWorkBatcherBeforeNavmeshPosition {
        int[] sampleIndexes;
        Vector3[] samples; //return to pool after query finish it work
        int samplesCount;  //amount of samples in array
        
        public NavMeshQuerySample(AgentProperties properties) : base(properties) {
            threadSafeResult = new List<NavmeshSampleResult>();
        }


        /// <summary>
        /// to sample single position
        /// </summary>
        /// <param name="pos">position</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. 
        /// if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        public void QueueWork(Vector3 pos, int layerMask = 1, bool updatePathFinder = true) {
            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;

                samplesCount = 1;
                samples = GenericPoolArray<Vector3>.Take(1);
                samples[0] = pos;

                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        /// <summary>
        /// to sample multiple positions
        /// </summary>
        /// <param name="positions">array with positions</param>
        /// <param name="positionsLength">how much take from this array</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. 
        /// if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        public void QueueWork(Vector3[] positions, int positionsLength, int layerMask = 1, bool updatePathFinder = true) {
            if (positions == null)
                throw new System.ArgumentNullException("positions");

            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;

                samplesCount = positionsLength;
                samples = GenericPoolArray<Vector3>.Take(samplesCount);
                for (int i = 0; i < samplesCount; i++) {
                    samples[i] = positions[i];
                }

                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        /// <summary>
        /// to sample multiple positions
        /// </summary>
        /// <param name="positions">array with positions</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. 
        /// if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        public void QueueWork(Vector3[] positions, int layerMask = 1, bool updatePathFinder = true) {
            QueueWork(positions, positions.Length, layerMask, updatePathFinder);
        }

        /// <summary>
        /// to sample multiple positions
        /// </summary>
        /// <param name="positions">array with positions</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. 
        /// if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        public void QueueWork(int layerMask = 1, bool updatePathFinder = true, params Vector3[] positions) {
            QueueWork(positions, positions.Length, layerMask, updatePathFinder);
        }

        /// <summary>
        /// to sample multiple positions
        /// </summary>
        /// <param name="positions">list woth positions</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. 
        /// if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        public void QueueWork(List<Vector3> positions, int layerMask = 1, bool updatePathFinder = true) {
            if (positions == null)
                throw new System.ArgumentNullException("positions");

            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;

                samplesCount = positions.Count;
                samples = GenericPoolArray<Vector3>.Take(samplesCount);
                for (int i = 0; i < positions.Count; i++) {
                    samples[i] = positions[i];
                }

                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        //is threadsafe
        override protected void OnUnityMainThreadFinalize() {
            queryHaveWork = false;
            threadSafeResult.Clear();
            threadSafeResult.AddRange(notThreadSafeResult);
            GenericPool<List<NavmeshSampleResult>>.ReturnToPool(ref notThreadSafeResult);

            if (recieveDelegate_TS != null)
                recieveDelegate_TS.Invoke(threadSafeResult);
        }
        
        public void OnBeforeNavmeshPositionUpdate() {
            sampleIndexes = GenericPoolArray<int>.Take(samplesCount);
            for (int i = 0; i < samplesCount; i++) {
                sampleIndexes[i] = PathFinder.RegisterNavmeshSample(properties, samples[i], layerMask, true);
            }
            GenericPoolArray<Vector3>.ReturnToPool(ref samples); //positions no longer needed and array returned to pool
        }
                
        public override void PerformWork(object context) {
            notThreadSafeResult = GenericPool<List<NavmeshSampleResult>>.Take();
            notThreadSafeResult.Clear();

            Cell[] globalCells = PathFinderData.cells;
            var resultsArray = PathFinder.navmeshPositionResults;

            for (int i = 0; i < samplesCount; i++) {
                var result = resultsArray[sampleIndexes[i]];
                notThreadSafeResult.Add(result.GetResult(result.cellGlobalID == -1 ? null : globalCells[result.cellGlobalID]));   
            }

            PathFinder.UnregisterNavmeshSample(sampleIndexes, samplesCount);
            GenericPoolArray<int>.ReturnToPool(ref sampleIndexes);
            Finish();
        }

    }
}
