using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

//TODO: use sqr values as raw weight of node

namespace K_PathFinder {
    //struct to hold point query result/ it contains value and costover navmesh graph to reach it
    //it should be readeadonly but there multiple problems with that in unity so pretend it's readonly
    public struct PointQueryResult<T> where T : ICellContentValue {
        public T value;      
        public float cost;   

        public PointQueryResult(T Value, float Weight) {
            value = Value;
            cost = Weight;
        }

        public override bool Equals(object obj) {
            if (obj is PointQueryResult<T> == false)
                return false;
            PointQueryResult<T> casted = (PointQueryResult<T>)obj;
            return 
                value.Equals(casted.value) && 
                casted.cost == cost;
        }


        public override int GetHashCode() {
            return value.GetHashCode() + (int)(cost);
        }

        public override string ToString() {
            return string.Format("PointQueryResult. Cost {0}, {1}", cost, value.ToString());
        }
    }
    
    //query that search on navmesh target object type on navmesh
    //note: for multiple starting points if more than 1 point inside cell then it used as starting point and second one ignored
    //so if starting points are too close then cramp it up inside 1 position
    public class NavMeshPointQuery<ContentValueType> : NavMeshQueryAbstract<List<PointQueryResult<ContentValueType>>>, IThreadPoolWorkBatcherBeforeNavmeshPosition where ContentValueType : ICellContentValue {
        Predicate<ContentValueType> predicate = null;
        bool richCost = false;
        bool ignoreCrouchCost = false;  
        float maxCost;

        //if just one position
        Vector3 position;
        //if multiple positions
        int positionsArrayLength = 0;
        Vector3[] positionsArray = null;

        //position sampling
        int positionSamplesLength = 0;
        int[] positionSamples = null;
        
#if UNITY_EDITOR
        public bool debug = false;
#endif

        public NavMeshPointQuery(AgentProperties properties) : base (properties) {     
            threadSafeResult = new List<PointQueryResult<ContentValueType>>();
            notThreadSafeResult = new List<PointQueryResult<ContentValueType>>();
        }

        /// <summary>
        /// Function to queue work for point query. You can take result at threadSafeResult
        /// </summary>
        /// <param name="position">position from where search is performed</param>
        /// <param name="maxCost">max cost on navmesh graph that query will check. large numbers lead to poor perfomance</param>
        /// <param name="layerMask">determine what parts of navmesh will be used with bitmask</param>
        /// <param name="costModifyerMask">determine what cost modifyers will be used with bitmask</param>
        /// <param name="richCost">
        /// if true then calculate more accurate cost but this will do more expensive operations. 
        /// recomend to make it accurate only if target results is clustered so you can distinguish better</param>
        /// <param name="ignoreCrouchCost">if true then criuch cost will be calculated as normal cost</param>
        /// <param name="predicate">predicate for points if you want to use one</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. 
        /// if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        public void QueueWork(
            Vector3 position, 
            float maxCost,
            int layerMask = 1,
            int costModifyerMask = 0,
            Predicate<ContentValueType> predicate = null,
            bool richCost = false, 
            bool ignoreCrouchCost = false,
            bool updatePathFinder = true) {

            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;
                this.costModifyerMask = costModifyerMask;
                this.position = position;
                positionsArrayLength = 0; //cause array is not used in this case
                this.maxCost = maxCost;
                this.richCost = richCost;
                this.ignoreCrouchCost = ignoreCrouchCost;
                this.predicate = predicate;
                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        /// <summary>
        /// Function to queue work for point query. You can take result at threadSafeResult
        /// </summary>
        /// <param name="maxCost">max cost on navmesh graph</param>
        /// <param name="richCost">
        /// if true then calculate more accurate cost but this will do more expensive operations. 
        /// recomend to make it accurate only if target results is clustered so you can distinguish better</param>
        /// <param name="ignoreCrouchCost">if true then criuch cost will be calculated as normal cost</param>
        /// <param name="predicate">predicate for points if you want to use one</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. 
        /// if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        /// <param name="positions">position from where search is performed</param>
        public void QueueWork(
            float maxCost,
            int layerMask = 1,
            int costModifyerMask = 0,
            Predicate<ContentValueType> predicate = null,
            bool richCost = false, 
            bool ignoreCrouchCost = false,    
            bool updatePathFinder = true, 
            params Vector3[] positions) {
            
            if (positions.Length == 0) {
                Debug.LogWarning("Pathfinder point query cant do anything with 0 positions");
                return;
            }

            if (positions.Length == 1) {
                QueueWork(positions[0], maxCost, layerMask, costModifyerMask, predicate, richCost, ignoreCrouchCost, updatePathFinder);
                return;
            }


            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;
                this.costModifyerMask = costModifyerMask;
                this.maxCost = maxCost;
                this.richCost = richCost;
                this.ignoreCrouchCost = ignoreCrouchCost;
                this.predicate = predicate;
                positionsArray = GenericPoolArray<Vector3>.Take(positions.Length);
                for (int i = 0; i < positions.Length; i++) {
                    positionsArray[i] = positions[i];
                }
                positionsArrayLength = positions.Length;
                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }               
        }
        
        //is threadsafe
        override protected void OnUnityMainThreadFinalize() {
            predicate = null;
            queryHaveWork = false;
            threadSafeResult.Clear();

            //add checks for unity object
            for (int i = 0; i < notThreadSafeResult.Count; i++) {
                threadSafeResult.Add(notThreadSafeResult[i]);
            }

            notThreadSafeResult.Clear();
            if (recieveDelegate_TS != null)
                recieveDelegate_TS.Invoke(threadSafeResult);
        }

        public void OnBeforeNavmeshPositionUpdate() {
            if (positionsArrayLength > 1) {
                //if multiple positions requested
                positionSamples = GenericPoolArray<int>.Take(positionsArrayLength); //get arrat for samples
                for (int i = 0; i < positionsArrayLength; i++) {
                    positionSamples[i] = PathFinder.RegisterNavmeshSample(properties, positionsArray[i], layerMask, true);   //registering samples
                }
                positionSamplesLength = positionsArrayLength;

                //return array with starting points
                positionsArrayLength = 0;
                GenericPoolArray<Vector3>.ReturnToPool(ref positionsArray);
            }
            else {
                positionSamples = GenericPoolArray<int>.Take(1, false);
                positionSamples[0] = PathFinder.RegisterNavmeshSample(properties, position, layerMask, true);
                positionSamplesLength = 1;
            }
        }
        
        public override void PerformWork(object context) {
#if UNITY_EDITOR
            if (debug)
                Debuger_K.ClearGeneric();
#endif
                
            //t for thread so user cant change this values in middle of process    
            bool t_richCost = richCost;
            bool t_ignoreCrouchCost = ignoreCrouchCost;

            //mod groups array
            float[] cellDeltaCostArray = PathFinder.cellDeltaCostArray;
            int cellDeltaCostLayout = PathFinder.deltaCostMaxGroupCount;

            int maxCells = PathFinderData.maxRegisteredCellID;
            Cell[] globalCells = PathFinderData.cells;
            bool[] usedCells = GenericPoolArray<bool>.Take(maxCells + 1, defaultValue: false);
            
            HeapFloatFirstLowest<HeapValueRich> heap = GenericPool<HeapFloatFirstLowest<HeapValueRich>>.Take();
            heap.TakeFromPoolAllocatedData(128);
            
            //Debuger_K.ClearGeneric();
            //float[] dc = cellDeltaCostArray[0];
            //for (int i = 0; i < maxCells; i++) {
            //    Cell c = globalCells[i];
            //    if (c != null) {
            //        Debuger_K.AddLabel(c.centerVector3, dc[i]);
            //    }
            //}

            //generation initial data
            //iterating over navmesh samples to collect data and generate initial connections
            for (int i = 0; i < positionSamplesLength; i++) {
                NavmeshSampleResult_Internal sample = PathFinder.navmeshPositionResults[positionSamples[i]];

                if (sample.cellGlobalID == -1 || usedCells[sample.cellGlobalID])
                    continue;
                            
                usedCells[sample.cellGlobalID] = true;
                Cell cell = globalCells[sample.cellGlobalID];

                float richCostMultiplier = 0f;
                if (t_richCost)
                    richCostMultiplier = cell.area.cost * (cell.passability == Passability.Crouchable && t_ignoreCrouchCost == false ? properties.crouchMod : properties.walkMod);
                
                //applying delta cost groups
                float deltaCost = 0f;
                if (costModifyerMask != 0) {
                    for (int group = 0; group < cellDeltaCostLayout; group++) {
                        if ((costModifyerMask & 1 << group) != 0)
                            deltaCost += cellDeltaCostArray[sample.cellGlobalID * cellDeltaCostLayout + group];
                    }
                }

                //adding content of start cells
                foreach (var content in globalCells[sample.cellGlobalID].cellContentValues) {
                    //Debuger_K.AddLabel(content.position, content is ContentValueType);
                    //Debuger_K.AddLine(content.position, cell.centerVector3, Color.red);

                    if (content is ContentValueType && content != null) {
                        ContentValueType casted = (ContentValueType)content;
                        if (predicate != null && !predicate(casted))
                            continue;

                        float cost = 0f;
                        if (t_richCost)
                            cost = Vector3.Distance(sample.position, content.position) * richCostMultiplier;


                        cost += deltaCost;
                        if (cost <= maxCost)
                            notThreadSafeResult.Add(new PointQueryResult<ContentValueType>(casted, cost));
                    }
                }
                
                int cellConnectionsCount = globalCells[sample.cellGlobalID].connectionsCount;
                CellConnection[] cellConnections = globalCells[sample.cellGlobalID].connections;

                for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                    CellConnection connection = cellConnections[connectionIndex];

                    if (usedCells[connection.connection] || (1 << globalCells[connection.connection].bitMaskLayer & layerMask) == 0)
                        continue;

                    float costToPoint, costTotal;
                    Vector3 point;
                    connection.CostForPointSearch(sample.position, properties, t_ignoreCrouchCost, out point, out costToPoint, out costTotal);

                    if (costToPoint > maxCost)
                        continue;

                    heap.Add(new HeapValueRich(connection.from, connection.connection, point, costToPoint), costTotal);
                }
            }      

            while (true) {
                if (heap.count == 0)
                    break;

                float heapCost;
                HeapValueRich currentHeapValue = heap.RemoveFirst(out heapCost);
                usedCells[currentHeapValue.connection] = true;

                Cell curNodeCell = globalCells[currentHeapValue.connection];
                if ((1 << curNodeCell.bitMaskLayer & layerMask) == 0)
                    continue;

                //applying delta cost groups
                float deltaCost = 0f;
                if (costModifyerMask != 0) {
                    for (int group = 0; group < cellDeltaCostLayout; group++) {
                        if ((costModifyerMask & 1 << group) != 0)
                            deltaCost += cellDeltaCostArray[currentHeapValue.connection * cellDeltaCostLayout + group];
                    }
                }

#if UNITY_EDITOR
                if (debug) {
                    float colorValue = Mathf.Clamp(heapCost + deltaCost, 0f, maxCost) / maxCost;
                    Cell fromCell = globalCells[currentHeapValue.from];
                    Debuger_K.AddLine(fromCell.centerVector3, globalCells[currentHeapValue.connection].centerVector3, new Color(colorValue, 1f - colorValue, 0f));
                    //Debuger_K.AddLabel(currentHeapValue.content.from.centerVector3 + (Vector3.up * 0.2f), deltaCost);
                }
#endif

                float richCostMultiplier = 0f;
                if (t_richCost)
                    richCostMultiplier = curNodeCell.area.cost * (curNodeCell.passability == Passability.Crouchable && t_ignoreCrouchCost == false ? properties.crouchMod : properties.walkMod);
                
                foreach (var content in curNodeCell.cellContentValues) {
                    //Debuger_K.AddLabel(content.position, content is ContentValueType);
                    //Debuger_K.AddLine(content.position, currentCell.centerVector3, Color.red);

                    if (content is ContentValueType && content != null) {
                        ContentValueType casted = (ContentValueType)content;
                        if (predicate != null && !predicate(casted))
                            continue;


                        float cost = currentHeapValue.baseCost;

                        if (t_richCost)
                            cost += richCostMultiplier * Vector3.Distance(currentHeapValue.baseVector, content.position);

                        cost += deltaCost;
                        if (cost <= maxCost)
                            notThreadSafeResult.Add(new PointQueryResult<ContentValueType>(casted, cost));
                    }
                }
                
                int cellConnectionsCount = curNodeCell.connectionsCount;
                CellConnection[] cellConnections = curNodeCell.connections;

                for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                    CellConnection connection = cellConnections[connectionIndex];

                    if (usedCells[connection.connection])
                        continue;

                    float costToPoint, costTotal;
                    Vector3 point;
                    connection.CostForPointSearch(properties, t_ignoreCrouchCost, out point, out costToPoint, out costTotal);                           
         
                    if(heapCost + costToPoint < maxCost)
                        heap.Add(new HeapValueRich(connection.from, connection.connection, point, heapCost + costToPoint), heapCost + costTotal);
                }
            }
            
            GenericPoolArray<bool>.ReturnToPool(ref usedCells);

            heap.ReturnToPoolAllocatedData();
            GenericPool<HeapFloatFirstLowest<HeapValueRich>>.ReturnToPool(ref heap);

            PathFinder.UnregisterNavmeshSample(positionSamples, positionSamplesLength);
            GenericPoolArray<int>.ReturnToPool(ref positionSamples);
            Finish();
        }

        struct HeapValueRich {
            public int from, connection;
            public Vector3 baseVector;
            public float baseCost;

            public HeapValueRich(int from, int connection, Vector3 baseVector, float baseCost) {
                this.from = from;
                this.connection = connection;        
                this.baseVector = baseVector;
                this.baseCost = baseCost;
            }
        }
    }
}