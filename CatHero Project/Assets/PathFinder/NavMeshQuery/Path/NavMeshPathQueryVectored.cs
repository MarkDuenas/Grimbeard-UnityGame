using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;


namespace K_PathFinder {
    public class NavMeshPathQueryVectored : NavMeshPathQueryAbstract {  
        float maxSearchCost;
        Vector3 targetVector;

        public NavMeshPathQueryVectored(AgentProperties properties, IPathOwner pathOwner) : base(properties, pathOwner) { }

        public void QueueWork(Vector3 start, float maxSearchCost, Vector3 targetVector, int layerMask = 1, bool updatePathFinder = true) {
            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;
                this.startPosition = start;
                this.maxSearchCost = maxSearchCost;
                this.targetVector = targetVector;
                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        public override void OnBeforeNavmeshPositionUpdate() {
            pathStartSample = PathFinder.RegisterNavmeshSample(properties, startPosition, layerMask, true);
        }

        //bare bones of path search
        public override void PerformWork(object context) {
            Path path = Path.PoolRent();

            NavmeshSampleResult_Internal startSample = PathFinder.UnregisterNavmeshSampleAndReturnResult(pathStartSample);
            if (startSample.type == NavmeshSampleResultType.InvalidNoNavmeshFound | startSample.cellGlobalID == -1) {
                notThreadSafeResult.Init(pathOwner, PathResultType.InvalidAgentOutsideNavmesh);
                Finish();
                return;
            }

            //snap to navmesh if outside and snap
            if (startSample.type != NavmeshSampleResultType.InsideNavmesh)
                startPosition = startSample.position;

            Cell startCell = PathFinderData.cells[startSample.cellGlobalID];

            int cellPathCount;
            CellConnection[] cellPath;
            PathResultType pathResult;
            if (SearchVectored(layerMask, maxExecutionTimeInMilliseconds, properties, ignoreCrouchCost, startCell, startPosition, maxSearchCost, targetVector, out cellPath, out cellPathCount, out pathResult)) {
                Cell targetCell = PathFinderData.cells[cellPath[cellPathCount - 1].connection];
                targetPosition = targetCell.centerVector3;
                GenericFunnel(path, cellPath, cellPathCount, startCell.passability, startPosition, targetCell.passability, targetPosition);
                GenericPoolArray<CellConnection>.ReturnToPool(ref cellPath); //return to pool array that was allocated while performing Search()
     
            }
            notThreadSafeResult.Init(pathOwner, pathResult);
            Finish();
        }

        //copy pasted version that only changed is heuristic function
        //return if path is valid
        //dummy connection is NOT added but there is space fot it at the end of CellContent[] cellPath
        protected static bool SearchVectored(
            int layerMask,
            int maxExecutionTime,
            AgentProperties properties, 
            bool ignoreCrouchCost, 
            Cell startCell, Vector3 startPosition, 
            float targetMaxCost, Vector3 targetVector, 
            out CellConnection[] cellPath, out int cellPathCount, out PathResultType resultType) {
            Cell[] globalCells = PathFinderData.cells;

            //taking from pool temp data           
            HashSet<Cell> closed = GenericPool<HashSet<Cell>>.Take(); //collection of cells that excluded from search
            HeapFloatFirstLowest<HeapValue> heap = GenericPool<HeapFloatFirstLowest<HeapValue>>.Take();//heap of nodes
            heap.TakeFromPoolAllocatedData(128);
            CellConnection[] heapRelativeConnections = GenericPoolArray<CellConnection>.Take(HEAP_VALUE_BASE_LENGTH);

            //since we need siquence of nodes hereare collection of all passed nodes
            //nodes have it root index to recreate all path
            HeapValue[] heapValues = GenericPoolArray<HeapValue>.Take(HEAP_VALUE_BASE_LENGTH);
            int heapValuesLength = HEAP_VALUE_BASE_LENGTH;
            int heapValuesCount = 0;

            //flags that tell resultand if it ever was
            HeapValue? lastNode = null;

            //adding start cell connections in special way
            closed.Add(startCell);

            int cellConnectionsCount = startCell.connectionsCount;
            CellConnection[] cellConnections = startCell.connections;

            for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                CellConnection cellConnection = cellConnections[connectionIndex];

                Cell con = globalCells[cellConnection.connection];
                if ((1 << con.bitMaskLayer & layerMask) != 0) {
                    HeapValue heapValue = new HeapValue(-1, heapValuesCount, cellConnection.Cost(startPosition, properties, ignoreCrouchCost), 0, con.globalID);
                    heapValues[heapValuesCount] = heapValue;
                    heapRelativeConnections[heapValuesCount] = cellConnection;

                    heapValuesCount++;
                    if (heapValuesCount == heapValuesLength) {
                        GenericPoolArray<HeapValue>.IncreaseSize(ref heapValues);
                        GenericPoolArray<CellConnection>.IncreaseSize(ref heapRelativeConnections);
                        heapValuesLength *= 2;
                    }
                    heap.Add(heapValue, heapValue.globalCost + GetHeuristicOther(con.centerVector3, startPosition, targetVector));
                }
            }
            float highestScore = 0f;

            int iterations = 0;
            Stopwatch stopwatch = GenericPool<Stopwatch>.Take();
            stopwatch.Start();
            resultType = PathResultType.InvalidInternalIssue;

            while (true) {
                if (heap.count == 0)
                    break;

                iterations++;
                if (iterations > 10) { //every 10 iterations we check if we exeed limits
                    iterations = 0;
                    if (stopwatch.ElapsedMilliseconds > maxExecutionTime) {
                        resultType = PathResultType.InvalidExceedTimeLimit;
                        break;
                    }
                }

                float curNodeWeight;
                HeapValue curNode = heap.RemoveFirst(out curNodeWeight);
                Cell curNodeCell = globalCells[curNode.connectionGlobalIndex];

                if (curNodeWeight > highestScore) {//first node that exeed target cost is right one
                    lastNode = curNode;
                    highestScore = curNodeWeight;
                }
                
                if ((1 << curNodeCell.bitMaskLayer & layerMask) == 0)
                    continue;

                //var c = curNode.content;
                //Cell c1 = c.from;
                //Cell c2 = c.connection;
                //Vector3 v1 = c1.centerVector3;
                //Vector3 v2 = c2.centerVector3;
                //Debuger_K.AddLine(v1, v2, Color.red, 0.1f);
                //Debuger_K.AddLabel(SomeMath.MidPoint(v1, v2), curNodeWeight);

                if (closed.Add(curNodeCell)) {
                    cellConnectionsCount = curNodeCell.connectionsCount;
                    cellConnections = curNodeCell.connections;

                    for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                        CellConnection cellConnection = cellConnections[connectionIndex];
                        Cell con = globalCells[cellConnection.connection];

                        if ((1 << con.bitMaskLayer & layerMask) == 0)
                            continue;

                        //new node in heap values
                        HeapValue heapValue = new HeapValue(
                            curNode.index,
                            heapValuesCount,
                            curNode.globalCost + cellConnection.Cost(properties, ignoreCrouchCost),
                            curNode.depth + 1,
                            con.globalID);

                        if (heapValue.globalCost > targetMaxCost)
                            continue;

                        //add new node to arrayof all nodes
                        heapValues[heapValuesCount] = heapValue;
                        heapRelativeConnections[heapValuesCount] = cellConnection;
                        heapValuesCount++;
                        if (heapValuesCount == heapValuesLength) {
                            GenericPoolArray<HeapValue>.IncreaseSize(ref heapValues);
                            GenericPoolArray<CellConnection>.IncreaseSize(ref heapRelativeConnections);
                            heapValuesLength *= 2;
                        }

                        //add new node to heap
                        float heapCost = heapValue.globalCost + GetHeuristicOther(con.centerVector3, startPosition, targetVector);
                        heap.Add(heapValue, heapCost);
                    }
                }
            }


            if (lastNode.HasValue) { //if there is some path
                HeapValue node = lastNode.Value;

                cellPath = GenericPoolArray<CellConnection>.Take(node.depth + 2);
                cellPathCount = node.depth + 1;

                for (int i = 0; i < cellPathCount; i++) {
                    cellPath[node.depth] = heapRelativeConnections[node.index];
                    if (node.root != -1)
                        node = heapValues[node.root];
                }
            }
            else {        //if there is no path
                cellPath = null;
                cellPathCount = 0;
            }

            stopwatch.Reset();
            GenericPool<Stopwatch>.ReturnToPool(ref stopwatch);

            //return to pool temp data 
            heap.ReturnToPoolAllocatedData();
            GenericPool<HeapFloatFirstLowest<HeapValue>>.ReturnToPool(ref heap);
            GenericPoolArray<CellConnection>.ReturnToPool(ref heapRelativeConnections);

            closed.Clear();
            GenericPool<HashSet<Cell>>.ReturnToPool(ref closed);

            GenericPoolArray<HeapValue>.ReturnToPool(ref heapValues);
            return lastNode.HasValue;
        }
        protected static float GetHeuristicOther(Vector3 testedPosition, Vector3 targetPosition, Vector3 targetRay) {
            //PFDebuger.Debuger_K.AddLine(SomeMath.NearestPointOnRay(targetPosition, targetRay, testedPosition), testedPosition);

            //return GetHeuristic(SomeMath.NearestPointOnRay(targetPosition, targetRay, testedPosition), testedPosition);
            return Vector3.Dot(targetPosition - testedPosition, targetRay) * -1;

             
        }

    }
}