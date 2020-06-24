using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using K_PathFinder.Pool;
using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using System;
using K_PathFinder.PFTools;

namespace K_PathFinder {
    //CONVENTION:
    //order arguments to queue
    //[important data like positions],[masks],[enum value that determine behavior],[optional bools that potentialy should be added into masks],[rest]
    //todo: comments
    public abstract class NavMeshPathQueryAbstract : NavMeshQueryAbstract<Path>, IThreadPoolWorkBatcherBeforeNavmeshPosition {  
        protected Vector3 startPosition, targetPosition;
        protected bool ignoreCrouchCost;
        public IPathOwner pathOwner { get; private set; }
        protected int pathStartSample, pathEndSample;

        public NavMeshPathQueryAbstract(AgentProperties properties, IPathOwner pathOwner) : base(properties) {
            this.pathOwner = pathOwner;    
        }
        
        public virtual void OnBeforeNavmeshPositionUpdate() {
            pathStartSample = PathFinder.RegisterNavmeshSample(properties, startPosition, layerMask, true);
            pathEndSample = PathFinder.RegisterNavmeshSample(properties, targetPosition, layerMask, true);
        }

        protected override void OnUnityMainThreadFinalize() {
            queryHaveWork = false;
            if (threadSafeResult != null)
                threadSafeResult.ReturnToPool(); //maybe should do it after?
            threadSafeResult = notThreadSafeResult;
 
            if (recieveDelegate_TS != null)
                recieveDelegate_TS.Invoke(threadSafeResult);
        }




        //return if path is valid
        //dummy connection is NOT added but there is space fot it at the end of CellContent[] cellPath
        //static int debugVal;
        protected static bool SearchGeneric(
            int layerMask,
            int costModifyerMask,
            int maxExecutionTime,
            AgentProperties properties,
            bool ignoreCrouchCost,
            BestFitOptions bestFitSearch,
            Cell startCell,
            Vector3 startPosition,
            ref Cell targetCell,
            ref Vector3 targetPosition,
            out CellConnection[] cellPath,
            out int cellPathCount,
            out PathResultType resultType,
            out float pathAproxCost) {
            bool[] usedCells = GenericPoolArray<bool>.Take(PathFinderData.maxRegisteredCellID + 1, defaultValue: false);
            //inside function heap implementation
            HeapValueSpecialOne[] heap = GenericPoolArray<HeapValueSpecialOne>.Take(128);
            int heapCount = 0;

            Cell[] globalCells = PathFinderData.cells;

            //since we need siquence of nodes here are collection of all passed nodes
            //nodes have it root index to recreate all path
            HeapValueSpecialOne[] allAddedHeapValues = GenericPoolArray<HeapValueSpecialOne>.Take(128);
            CellConnection[] allAddedHeapValuesRelativeConnections = GenericPoolArray<CellConnection>.Take(128);
            int allAddedHeapValuesCount = 0;

            //flags that tell resultand if it ever was
            HeapValueSpecialOne? lastNode = null;
            HeapValueSpecialOne? bestFitNode = null;
            Vector3? bestFitPos = null;
            float bestFitSqrDist = float.MaxValue;

            //mod groups array
            float[] cellDeltaCostArray = PathFinder.cellDeltaCostArray;
            int cellDeltaCostLayout = PathFinder.deltaCostMaxGroupCount;

            //adding start cell connections in special way      
            usedCells[startCell.globalID] = true;

            int cellConnectionsCount = startCell.connectionsCount;
            CellConnection[] cellConnections = startCell.connections;
            
            for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                CellConnection cellConnection = cellConnections[connectionIndex];

                Cell con = globalCells[cellConnection.connection];

                if ((1 << con.bitMaskLayer & layerMask) == 0)
                    continue;
                
                //applying delta cost groups
                float deltaCost = 0f;
                if (costModifyerMask != 0) {
                    for (int group = 0; group < cellDeltaCostLayout; group++) {
                        if ((costModifyerMask & 1 << group) != 0)
                            deltaCost += cellDeltaCostArray[cellConnection.connection * cellDeltaCostLayout + group];
                    }
                }

                float globalCost = cellConnection.Cost(startPosition, properties, ignoreCrouchCost) + deltaCost;

                HeapValueSpecialOne heapValue = new HeapValueSpecialOne(-1,
                    allAddedHeapValuesCount,
                    globalCost,
                    globalCost + GetHeuristic(targetPosition, con.centerVector3),
                    0, cellConnection.connection);

                allAddedHeapValues[allAddedHeapValuesCount] = heapValue;
                allAddedHeapValuesRelativeConnections[allAddedHeapValuesCount] = cellConnection;

                allAddedHeapValuesCount++;
                if (allAddedHeapValuesCount == allAddedHeapValues.Length) {
                    GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref allAddedHeapValues);
                    GenericPoolArray<CellConnection>.IncreaseSize(ref allAddedHeapValuesRelativeConnections);
                }

                #region adding to heap 1
                if (heap.Length == heapCount)
                    GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref heap);
                heap[heapCount] = heapValue;
                if (heapCount > 0) {
                    int index = heapCount;
                    int parentIndex;

                    HeapValueSpecialOne item = heap[index];

                    while (true) {
                        parentIndex = (index - 1) / 2;
                        HeapValueSpecialOne parentItem = heap[parentIndex];

                        if (item.heapValue < parentItem.heapValue) {
                            HeapValueSpecialOne valA = heap[index];
                            heap[index] = heap[parentIndex];
                            heap[parentIndex] = valA;
                            index = parentIndex;
                        }
                        else
                            break;
                    }
                }
                heapCount++;
                #endregion
            }

            int iterations = 0;
            System.Diagnostics.Stopwatch stopwatch = GenericPool<System.Diagnostics.Stopwatch>.Take();
            stopwatch.Start();
            resultType = PathResultType.InvalidInternalIssue;

            while (true) {
                if (heapCount == 0)
                    break;

                iterations++;
                if (iterations > 50) { //every 50 iterations we check if we exeed time limits
                    iterations = 0;
                    if (stopwatch.ElapsedMilliseconds > maxExecutionTime) {
                        resultType = PathResultType.InvalidExceedTimeLimit;
                        break;
                    }
                }

                HeapValueSpecialOne firstHeapValue = heap[0];
                #region remove heap value
                heapCount--;
                heap[0] = heap[heapCount];

                int removeHeapValueIndex = 0;
                HeapValueSpecialOne heapItemRemoving = heap[removeHeapValueIndex];
                int childIndexLeft, childIndexRight, swapIndex;

                while (true) {
                    childIndexLeft = removeHeapValueIndex * 2 + 1;
                    childIndexRight = removeHeapValueIndex * 2 + 2;
                    swapIndex = 0;

                    if (childIndexLeft < heapCount) {
                        swapIndex = childIndexLeft;

                        if (childIndexRight < heapCount && heap[childIndexLeft].heapValue > heap[childIndexRight].heapValue)
                            swapIndex = childIndexRight;

                        if (heapItemRemoving.heapValue > heap[swapIndex].heapValue) {
                            HeapValueSpecialOne valA = heap[removeHeapValueIndex];
                            heap[removeHeapValueIndex] = heap[swapIndex];
                            heap[swapIndex] = valA;
                            removeHeapValueIndex = swapIndex;
                        }
                        else
                            break;
                    }
                    else
                        break;
                }
                #endregion

                Cell curNodeCell = globalCells[firstHeapValue.connectedCell];
                usedCells[curNodeCell.globalID] = true;

                if ((1 << curNodeCell.bitMaskLayer & layerMask) == 0)
                    continue;

                //var c = allAddedHeapValuesRelativeConnections[firstHeapValue.index];
                //Cell c1 = globalCells[c.from];
                //Cell c2 = globalCells[c.connection];
                //Vector3 v1 = c1.centerVector3;
                //Vector3 v2 = c2.centerVector3;
                //Debuger_K.AddLine(v1, v2, Color.red, 0.05f * debugVal);
                //Debuger_K.AddLabel(SomeMath.MidPoint(v1, v2) + (Vector3.up * 0.05f * debugVal), debugVal + " : " + firstHeapValue.heapValue);
                //debugVal++;

                if (curNodeCell == targetCell) {//found path
                    lastNode = firstHeapValue;
                    break;
                }

                if (bestFitSearch != 0) {
                    if (bestFitSearch == BestFitOptions.FastSearch) {
                        float curSqrDist = SomeMath.SqrDistance(curNodeCell.centerVector3, targetPosition);
                        if (curSqrDist < bestFitSqrDist) {
                            bestFitSqrDist = curSqrDist;
                            bestFitNode = firstHeapValue;
                        }
                    }
                    else {
                        Vector3 closest;
                        float curSqrDist;
                        curNodeCell.GetClosestPointOnHull(targetPosition.x, targetPosition.z, out closest, out curSqrDist);
                        if (curSqrDist < bestFitSqrDist) {
                            bestFitSqrDist = curSqrDist;
                            bestFitNode = firstHeapValue;
                            bestFitPos = closest;
                        }
                    }
                }

                cellConnectionsCount = curNodeCell.connectionsCount;
                cellConnections = curNodeCell.connections;

                for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                    CellConnection cellConnection = cellConnections[connectionIndex];

                    if (usedCells[cellConnection.connection])
                        continue;

                    Cell con = globalCells[cellConnection.connection];

                    if ((1 << con.bitMaskLayer & layerMask) == 0)
                        continue;

                    //applying delta cost groups
                    float deltaCost = 0f;
                    if (costModifyerMask != 0) {
                        for (int group = 0; group < cellDeltaCostLayout; group++) {
                            if ((costModifyerMask & 1 << group) != 0)
                                deltaCost += cellDeltaCostArray[cellConnection.connection * cellDeltaCostLayout + group];
                        }
                    }

                    //new node in heap values
                    float globalCost = firstHeapValue.globalCost + cellConnection.Cost(properties, ignoreCrouchCost) + deltaCost;

                    HeapValueSpecialOne heapValue = new HeapValueSpecialOne(
                        firstHeapValue.index,
                        allAddedHeapValuesCount,
                        globalCost,
                        globalCost + GetHeuristic(targetPosition, con.centerVector3),
                        firstHeapValue.depth + 1,
                        cellConnection.connection);

                    //add new node to array of all nodes
                    if (allAddedHeapValuesCount == allAddedHeapValues.Length) {
                        GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref allAddedHeapValues);
                        GenericPoolArray<CellConnection>.IncreaseSize(ref allAddedHeapValuesRelativeConnections);
                    }
                    allAddedHeapValues[allAddedHeapValuesCount] = heapValue;
                    allAddedHeapValuesRelativeConnections[allAddedHeapValuesCount] = cellConnection;
                    allAddedHeapValuesCount++;

                    #region adding to heap 2
                    if (heap.Length == heapCount)
                        GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref heap);

                    heap[heapCount] = heapValue;

                    if (heapCount > 0) {
                        int index = heapCount;
                        int parentIndex;

                        HeapValueSpecialOne item = heap[index];

                        while (true) {
                            parentIndex = (index - 1) / 2;
                            HeapValueSpecialOne parentItem = heap[parentIndex];

                            if (item.heapValue < parentItem.heapValue) {
                                HeapValueSpecialOne valA = heap[index];
                                heap[index] = heap[parentIndex];
                                heap[parentIndex] = valA;
                                index = parentIndex;
                            }
                            else
                                break;
                        }
                    }

                    heapCount++;
                    #endregion                    
                }
            }

            if (lastNode.HasValue) { //if there is some path
                HeapValueSpecialOne node = lastNode.Value;
                cellPath = GenericPoolArray<CellConnection>.Take(node.depth + 1);
                cellPathCount = node.depth + 1;

                for (int i = 0; i < cellPathCount; i++) {
                    cellPath[node.depth] = allAddedHeapValuesRelativeConnections[node.index];
                    if (node.root != -1)
                        node = allAddedHeapValues[node.root];
                }
                resultType = PathResultType.Valid;
            }
            else if (bestFitSearch != BestFitOptions.DontSearch) {
                HeapValueSpecialOne node = bestFitNode.Value;

                targetCell = globalCells[node.connectedCell];
                if (bestFitSearch == BestFitOptions.PreciseSearch)
                    targetPosition = bestFitPos.Value;
                else {
                    float sqrD;
                    targetCell.GetClosestPointOnHull(targetPosition.x, targetPosition.z, out targetPosition, out sqrD);
                }

                cellPath = GenericPoolArray<CellConnection>.Take(node.depth + 2);
                cellPathCount = node.depth + 1;

                for (int i = 0; i < cellPathCount; i++) {
                    cellPath[node.depth] = allAddedHeapValuesRelativeConnections[node.index];

                    if (node.root != -1)
                        node = allAddedHeapValues[node.root];
                }

                lastNode = node;
                if (resultType != PathResultType.InvalidExceedTimeLimit)
                    resultType = PathResultType.BestFit;
            }
            else { //if there is no path
                cellPath = null;
                cellPathCount = 0;
                if (resultType != PathResultType.InvalidExceedTimeLimit)
                    resultType = PathResultType.InvalidNoPath;
            }

            stopwatch.Reset();
            GenericPool<System.Diagnostics.Stopwatch>.ReturnToPool(ref stopwatch);
            GenericPoolArray<HeapValueSpecialOne>.ReturnToPool(ref heap);
            GenericPoolArray<HeapValueSpecialOne>.ReturnToPool(ref allAddedHeapValues);
            GenericPoolArray<CellConnection>.ReturnToPool(ref allAddedHeapValuesRelativeConnections);
            GenericPoolArray<bool>.ReturnToPool(ref usedCells);

            pathAproxCost = lastNode.HasValue ? lastNode.Value.globalCost : 0f;
            return lastNode.HasValue;
        }

        //return if path is valid
        //dummy connection is NOT added but there is space fot it at the end of CellContent[] cellPath
        protected static bool SearchGenericWithPredicate(
            int layerMask,
            int costModifyerMask,
            int maxExecutionTime,
            AgentProperties properties,
            bool ignoreCrouchCost,
            Cell startCell,
            Vector3 startPosition,
            Predicate<Cell> predicate,
            float maxSearchCost,
            out CellConnection[] cellPath,
            out int cellPathCount,
            out PathResultType resultType,
            out float pathAproxCost) {
            bool[] usedCells = GenericPoolArray<bool>.Take(PathFinderData.maxRegisteredCellID + 1, defaultValue: false);
            //inside function heap implementation
            HeapValueSpecialOne[] heap = GenericPoolArray<HeapValueSpecialOne>.Take(128);
            int heapCount = 0;

            Cell[] globalCells = PathFinderData.cells;

            //since we need siquence of nodes here are collection of all passed nodes
            //nodes have it root index to recreate all path
            HeapValueSpecialOne[] allAddedHeapValues = GenericPoolArray<HeapValueSpecialOne>.Take(128);
            CellConnection[] allAddedHeapValuesRelativeConnections = GenericPoolArray<CellConnection>.Take(128);
            int allAddedHeapValuesCount = 0;
            
            //mod groups array
            float[] cellDeltaCostArray = PathFinder.cellDeltaCostArray;
            int cellDeltaCostLayout = PathFinder.deltaCostMaxGroupCount;

            //adding start cell connections in special way      
            usedCells[startCell.globalID] = true;

            int cellConnectionsCount = startCell.connectionsCount;
            CellConnection[] cellConnections = startCell.connections;

            //flags that tell resultand if it ever was
            HeapValueSpecialOne? lastNode = null;

            for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                CellConnection cellConnection = cellConnections[connectionIndex];

                Cell con = globalCells[cellConnection.connection];                       

                if ((1 << con.bitMaskLayer & layerMask) == 0)
                    continue;

                //applying delta cost groups
                float deltaCost = 0f;
                if (costModifyerMask != 0) {
                    for (int group = 0; group < cellDeltaCostLayout; group++) {
                        if ((costModifyerMask & 1 << group) != 0)
                            deltaCost += cellDeltaCostArray[cellConnection.connection * cellDeltaCostLayout + group];
                    }
                }

                float globalCost = cellConnection.Cost(startPosition, properties, ignoreCrouchCost) + deltaCost;

                HeapValueSpecialOne heapValue = new HeapValueSpecialOne(-1,
                    allAddedHeapValuesCount,
                    globalCost, globalCost,
                    0, cellConnection.connection);

                allAddedHeapValues[allAddedHeapValuesCount] = heapValue;
                allAddedHeapValuesRelativeConnections[allAddedHeapValuesCount] = cellConnection;

                allAddedHeapValuesCount++;
                if (allAddedHeapValuesCount == allAddedHeapValues.Length) {
                    GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref allAddedHeapValues);
                    GenericPoolArray<CellConnection>.IncreaseSize(ref allAddedHeapValuesRelativeConnections);
                }

                #region adding to heap 1
                if (heap.Length == heapCount)
                    GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref heap);
                heap[heapCount] = heapValue;
                if (heapCount > 0) {
                    int index = heapCount;
                    int parentIndex;

                    HeapValueSpecialOne item = heap[index];

                    while (true) {
                        parentIndex = (index - 1) / 2;
                        HeapValueSpecialOne parentItem = heap[parentIndex];

                        if (item.heapValue < parentItem.heapValue) {
                            HeapValueSpecialOne valA = heap[index];
                            heap[index] = heap[parentIndex];
                            heap[parentIndex] = valA;
                            index = parentIndex;
                        }
                        else
                            break;
                    }
                }
                heapCount++;
                #endregion
            }

            int iterations = 0;
            System.Diagnostics.Stopwatch stopwatch = GenericPool<System.Diagnostics.Stopwatch>.Take();
            stopwatch.Start();
            resultType = PathResultType.InvalidInternalIssue;

            while (true) {
                if (heapCount == 0)
                    break;

                iterations++;
                if (iterations > 50) { //every 50 iterations we check if we exeed time limits
                    iterations = 0;
                    if (stopwatch.ElapsedMilliseconds > maxExecutionTime) {
                        resultType = PathResultType.InvalidExceedTimeLimit;
                        break;
                    }
                }

                HeapValueSpecialOne firstHeapValue = heap[0];
                #region remove heap value
                heapCount--;
                heap[0] = heap[heapCount];

                int removeHeapValueIndex = 0;
                HeapValueSpecialOne heapItemRemoving = heap[removeHeapValueIndex];
                int childIndexLeft, childIndexRight, swapIndex;

                while (true) {
                    childIndexLeft = removeHeapValueIndex * 2 + 1;
                    childIndexRight = removeHeapValueIndex * 2 + 2;
                    swapIndex = 0;

                    if (childIndexLeft < heapCount) {
                        swapIndex = childIndexLeft;

                        if (childIndexRight < heapCount && heap[childIndexLeft].heapValue > heap[childIndexRight].heapValue)
                            swapIndex = childIndexRight;

                        if (heapItemRemoving.heapValue > heap[swapIndex].heapValue) {
                            HeapValueSpecialOne valA = heap[removeHeapValueIndex];
                            heap[removeHeapValueIndex] = heap[swapIndex];
                            heap[swapIndex] = valA;
                            removeHeapValueIndex = swapIndex;
                        }
                        else
                            break;
                    }
                    else
                        break;
                }
                #endregion

                Cell curNodeCell = globalCells[firstHeapValue.connectedCell];
                usedCells[curNodeCell.globalID] = true;
                
                if ((1 << curNodeCell.bitMaskLayer & layerMask) == 0)
                    continue;


                //var c = allAddedHeapValuesRelativeConnections[firstHeapValue.index];
                //Cell c1 = globalCells[c.from];
                //Cell c2 = globalCells[c.connection];
                //Vector3 v1 = c1.centerVector3;
                //Vector3 v2 = c2.centerVector3;
                //Debuger_K.AddLine(v1, v2, Color.red, 0.05f * debugVal);
                //Debuger_K.AddLabel(SomeMath.MidPoint(v1, v2) + (Vector3.up * 0.05f * debugVal), debugVal + " : " + firstHeapValue.heapValue);
                //debugVal++;


                if (predicate(curNodeCell)) {//found path
                    lastNode = firstHeapValue;
                    break;
                }
                
                cellConnectionsCount = curNodeCell.connectionsCount;
                cellConnections = curNodeCell.connections;

                for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                    CellConnection cellConnection = cellConnections[connectionIndex];

                    if (usedCells[cellConnection.connection])
                        continue;

                    Cell con = globalCells[cellConnection.connection];

                    if ((1 << con.bitMaskLayer & layerMask) == 0)
                        continue;

                    //applying delta cost groups
                    float deltaCost = 0f;
                    if (costModifyerMask != 0) {
                        for (int group = 0; group < cellDeltaCostLayout; group++) {
                            if ((costModifyerMask & 1 << group) != 0)
                                deltaCost += cellDeltaCostArray[cellConnection.connection * cellDeltaCostLayout + group];
                        }
                    }

                    //new node in heap values
                    float globalCost = firstHeapValue.globalCost + cellConnection.Cost(properties, ignoreCrouchCost) + deltaCost;

                    if (globalCost > maxSearchCost)
                        continue;

                    HeapValueSpecialOne heapValue = new HeapValueSpecialOne(
                        firstHeapValue.index,
                        allAddedHeapValuesCount,
                        globalCost, globalCost,
                        firstHeapValue.depth + 1,
                        cellConnection.connection);

                    //add new node to array of all nodes
                    if (allAddedHeapValuesCount == allAddedHeapValues.Length) {
                        GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref allAddedHeapValues);
                        GenericPoolArray<CellConnection>.IncreaseSize(ref allAddedHeapValuesRelativeConnections);
                    }
                    allAddedHeapValues[allAddedHeapValuesCount] = heapValue;
                    allAddedHeapValuesRelativeConnections[allAddedHeapValuesCount] = cellConnection;
                    allAddedHeapValuesCount++;

                    #region adding to heap 2
                    if (heap.Length == heapCount)
                        GenericPoolArray<HeapValueSpecialOne>.IncreaseSize(ref heap);

                    heap[heapCount] = heapValue;

                    if (heapCount > 0) {
                        int index = heapCount;
                        int parentIndex;

                        HeapValueSpecialOne item = heap[index];

                        while (true) {
                            parentIndex = (index - 1) / 2;
                            HeapValueSpecialOne parentItem = heap[parentIndex];

                            if (item.heapValue < parentItem.heapValue) {
                                HeapValueSpecialOne valA = heap[index];
                                heap[index] = heap[parentIndex];
                                heap[parentIndex] = valA;
                                index = parentIndex;
                            }
                            else
                                break;
                        }
                    }

                    heapCount++;
                    #endregion                    
                }
            }

            if (lastNode.HasValue) { //if there is some path
                HeapValueSpecialOne node = lastNode.Value;
                cellPath = GenericPoolArray<CellConnection>.Take(node.depth + 1);
                cellPathCount = node.depth + 1;

                for (int i = 0; i < cellPathCount; i++) {
                    cellPath[node.depth] = allAddedHeapValuesRelativeConnections[node.index];
                    if (node.root != -1)
                        node = allAddedHeapValues[node.root];
                }
                resultType = PathResultType.Valid;
            }
            else { //if there is no path
                cellPath = null;
                cellPathCount = 0;
                if (resultType != PathResultType.InvalidExceedTimeLimit)
                    resultType = PathResultType.InvalidNoPath;
            }

            stopwatch.Reset();
            GenericPool<System.Diagnostics.Stopwatch>.ReturnToPool(ref stopwatch);
            GenericPoolArray<HeapValueSpecialOne>.ReturnToPool(ref heap);
            GenericPoolArray<HeapValueSpecialOne>.ReturnToPool(ref allAddedHeapValues);
            GenericPoolArray<CellConnection>.ReturnToPool(ref allAddedHeapValuesRelativeConnections);
            GenericPoolArray<bool>.ReturnToPool(ref usedCells);

            pathAproxCost = lastNode.HasValue ? lastNode.Value.globalCost : 0f;
            return lastNode.HasValue;
        }



        #region some generic functions that probably wount be heavily modifyed
        //it also sepparate cell path to sections between jumps and funnel them
        protected static void GenericFunnel(Path path, CellConnection[] connections, int cellPathCount, Passability startCellPassability, Vector3 startPosition, Passability targetCellPassability, Vector3 targetPosition) {
            CellConnection[] tempConnections = GenericPoolArray<CellConnection>.Take(cellPathCount + 1);
            CellContentData[] tempDatas = GenericPoolArray<CellContentData>.Take(cellPathCount + 1);
            int curPathLength = 0;

            Cell[] globalCells = PathFinderData.cells;

            path.AddMove(startPosition, (MoveState)(int)startCellPassability);

            for (int c = 0; c < cellPathCount; c++) {
                CellConnection connection = connections[c];
                CellContentData cсd = globalCells[connection.from].connectionsDatas[connection.cellInternalIndex];

                if (connection.type == CellConnectionType.Generic) {
                    tempConnections[curPathLength] = connection;
                    tempDatas[curPathLength] = cсd;
                    curPathLength++;
                }
                else if (connection.type == CellConnectionType.jumpUp | connection.type == CellConnectionType.jumpDown) {
                    Vector3 enter = cсd.leftV3;
                    Vector3 axis = connection.intersection;
                    Vector3 exit = cсd.rightV3;

                    DoFunnelIteration(path.lastV3, enter, tempConnections, tempDatas, curPathLength, path);  //do funnel
                    curPathLength = 0;

                    //then add jump
                    if (connection.type == CellConnectionType.jumpUp) {
                        path.AddMove(enter, (MoveState)(int)connection.passabilityFrom);
                        path.AddJumpUp(enter, axis);
                        path.AddMove(exit, (MoveState)(int)connection.passabilityConnection);
                    }
                    else {
                        path.AddMove(enter, (MoveState)(int)connection.passabilityFrom);                   
                        path.AddJumpDown(axis, exit);
                        path.AddMove(exit, (MoveState)(int)connection.passabilityConnection);
                    }
                }
                else if (connection.type == CellConnectionType.Invalid)
                    Debug.LogErrorFormat("unknown type of CellConnectionAbstract node {0}", connection.GetType().Name);
            }

            tempConnections[curPathLength] = new CellConnection(tempConnections[cellPathCount - 1]); //adding dummy connection 
            tempDatas[curPathLength] = new CellContentData(targetPosition, targetPosition);
            curPathLength++;

            DoFunnelIteration(path.lastV3, targetPosition, tempConnections, tempDatas, curPathLength, path); //do last funnel iteration
            path.SetCurrentIndex(1);

            GenericPoolArray<CellConnection>.ReturnToPool(ref tempConnections);
            GenericPoolArray<CellContentData>.ReturnToPool(ref tempDatas);
        }
        protected static void DoFunnelIteration(Vector3 startV3, Vector3 endV3, CellConnection[] cellPath, CellContentData[] cellDatas, int pathLength, Path path) {
            //for (int i = 0; i < pathLength; i++) {
            //    Vector3 L = cellPathVectors[i * 2];
            //    Vector3 R = cellPathVectors[i * 2 + 1];
            //    PFDebuger.Debuger_K.AddLabel(L - new Vector3(0, 0.2f, 0), "L" + i);
            //    PFDebuger.Debuger_K.AddLabel(R - new Vector3(0, 0.2f, 0), "R" + i);
            //}

            //NOTES:
            //startV3 reused as PATH LAST POINT. rename it for readability      
            int lastIteration = 0;

            for (int iterationStart = 0; iterationStart < pathLength; iterationStart++) {
                float lowLeftDirX = cellDatas[iterationStart].xLeft - startV3.x;   //cur lowest value
                float lowLeftDirZ = cellDatas[iterationStart].zLeft - startV3.z;    //cur lowest value
                float lowRightDirX = cellDatas[iterationStart].xRight - startV3.x; //cur lowest value
                float lowRightDirZ = cellDatas[iterationStart].zRight - startV3.z; //cur lowest value

                int stuckLeft = iterationStart;
                int stuckRight = iterationStart;

                for (int curGateIndex = iterationStart + 1; curGateIndex < pathLength; curGateIndex++) {
                    float curNodeDirX, curNodeDirZ;

                    //RIGHT NODE
                    curNodeDirX = cellDatas[curGateIndex].xRight - startV3.x;//direction to RIGHT
                    curNodeDirZ = cellDatas[curGateIndex].zRight - startV3.z;//direction to RIGHT

                    //if (SomeMath.SqrDistance(lowRightDirX, lowRightDirZ, curNodeDirX, curNodeDirZ) < 0.01f | //sqr distance between directions to current node and best node. if nodes too close anyway by XZ then they considered as same thing
                    //    SomeMath.V2Cross(lowRightDirX, lowRightDirZ, curNodeDirX, curNodeDirZ) <= 0f) { //current right is lefter than last right                    
                    if (((curNodeDirX - lowRightDirX) * (curNodeDirX - lowRightDirX)) + ((curNodeDirZ - lowRightDirZ) * (curNodeDirZ - lowRightDirZ)) < 0.01f | //sqr distance between directions to current node and best node. if nodes too close anyway by XZ then they considered as same thing
                        (lowRightDirZ * curNodeDirX) - (lowRightDirX * curNodeDirZ) <= 0f) { //checking cross product between cur dicrection and cur best direction. current right is lefter than last right

                        //if (SomeMath.V2Cross(lowLeftDirX, lowLeftDirZ, curNodeDirX, curNodeDirZ) > 0f) {//current right is lefter than left
                        if ((lowLeftDirZ * curNodeDirX) - (lowLeftDirX * curNodeDirZ) > 0f) {//current right is lefter than left
                            stuckRight = curGateIndex;//move stucked RIGHT gate 
                            lowRightDirX = curNodeDirX;//this should be above this if but this value dont used after so dont need to asign it there
                            lowRightDirZ = curNodeDirZ;//this should be above this if but this value dont used after so dont need to asign it there
                        }
                        else {
                            for (int cycle = iterationStart; cycle < stuckLeft; cycle++) {
                                if (cellPath[cycle].difPassabilities) {
                                    Vector3 ccInt;
                                    if (cellDatas[cycle].LineIntersectXZ(
                                        startV3.x, startV3.y, startV3.z,
                                        cellDatas[stuckLeft].xLeft, //cur end node
                                        cellDatas[stuckLeft].yLeft,
                                        cellDatas[stuckLeft].zLeft,
                                        out ccInt))
                                        path.AddMove(ccInt, (MoveState)(int)cellPath[cycle].passabilityFrom);
                                }
                            }
                            startV3 = cellDatas[stuckLeft].leftV3;//start now end 
                            path.AddMove(startV3, (MoveState)(int)cellPath[stuckLeft].passabilityFrom);

                            iterationStart = stuckLeft; //shift start index of loop
                            lastIteration = stuckLeft;
                            break;
                        }
                    }

                    //LEFT NODE                  
                    curNodeDirX = cellDatas[curGateIndex].xLeft - startV3.x;//direction to LEFT
                    curNodeDirZ = cellDatas[curGateIndex].zLeft - startV3.z;//direction to LEFT        

                    //if (SomeMath.SqrDistance(lowLeftDirX, lowLeftDirZ, curNodeDirX, curNodeDirZ) < 0.01f |//sqr distance between directions to current node and best node. if nodes too close anyway by XZ then they considered as same thing
                    //    SomeMath.V2Cross(lowLeftDirX, lowLeftDirZ, curNodeDirX, curNodeDirZ) >= 0) { //current left is righter than left

                    if (((curNodeDirX - lowLeftDirX) * (curNodeDirX - lowLeftDirX)) + ((curNodeDirZ - lowLeftDirZ) * (curNodeDirZ - lowLeftDirZ)) < 0.01f |//sqr distance between directions to current node and best node. if nodes too close anyway by XZ then they considered as same thing
                        (lowLeftDirZ * curNodeDirX) - (lowLeftDirX * curNodeDirZ) >= 0f) { //current left is righter than left

                        //if (SomeMath.V2Cross(lowRightDirX, lowRightDirZ, curNodeDirX, curNodeDirZ) < 0f) {//current left is righter than right
                        if ((lowRightDirZ * curNodeDirX) - (lowRightDirX * curNodeDirZ) < 0f) {//current left is righter than right
                            stuckLeft = curGateIndex;//mode stuked LEFT gate
                            lowLeftDirX = curNodeDirX;//this should be above this if but this value dont used after so dont need to asign it there
                            lowLeftDirZ = curNodeDirZ;//this should be above this if but this value dont used after so dont need to asign it there
                        }
                        else {
                            //adding in                 
                            for (int cycle = iterationStart; cycle < stuckRight; cycle++) {
                                if (cellPath[cycle].difPassabilities) {
                                    Vector3 ccInt;
                                    if (cellDatas[cycle].LineIntersectXZ(
                                        startV3.x, startV3.y, startV3.z,
                                        cellDatas[stuckRight].xRight,
                                        cellDatas[stuckRight].yRight,
                                        cellDatas[stuckRight].zRight,
                                        out ccInt))// cur end node
                                        path.AddMove(ccInt, (MoveState)(int)cellPath[cycle].passabilityFrom);
                                }
                            }
                            startV3 = cellDatas[stuckRight].rightV3;//start now end                      
                            path.AddMove(startV3, (MoveState)(int)cellPath[stuckRight].passabilityFrom);
                            iterationStart = stuckRight;//shift start index of loop
                            lastIteration = stuckRight;
                            break;
                        }
                    }
                }
            }

            //adding path from last node we stuck to the end
            if (lastIteration != pathLength) {
                for (int cycle = lastIteration; cycle < pathLength; cycle++) {
                    if (cellPath[cycle].difPassabilities) {
                        Vector3 ccInt;
                        if (cellDatas[cycle].LineIntersectXZ(
                            startV3.x, startV3.y, startV3.z,
                            endV3.x, endV3.y, endV3.z, out ccInt))
                            path.AddMove(ccInt, (MoveState)(int)cellPath[cycle].passabilityFrom);
                    }
                }
                path.AddMove(endV3, (MoveState)(int)cellPath[pathLength - 1].passabilityFrom);
            }
        }
        #endregion





        ////PRESUMABLE THIS FUNCTION FOR COPY-PASE. Cause you probably need to add some parts here and there
        ////return if path is valid
        ////dummy connection is NOT added but there is space for it at the end of CellContent[] cellPath
        //protected static bool SearchSimple(
        //    int layerMask,
        //    int maxExecutionTime,
        //    AgentProperties properties, 
        //    bool ignoreCrouchCost, 
        //    Cell startCell, Vector3 startPosition,
        //    Cell targetCell, Vector3 targetPosition,
        //    out CellConnection[] cellPath, 
        //    out int cellPathCount, 
        //    out PathResultType resultType,
        //    out float pathAproxCost) {
        //    Cell[] globalCells = PathFinderData.cells;

        //    //taking from pool temp data   
        //    bool[] usedCells = GenericPoolArray<bool>.Take(PathFinderData.maxRegisteredCellID + 1, defaultValue: false);//collection of cells that excluded from search            
        //    HeapFloatFirstLowest<HeapValue> heap = GenericPool<HeapFloatFirstLowest<HeapValue>>.Take();//heap of nodes
        //    heap.TakeFromPoolAllocatedData(128);

        //    //since we need siquence of nodes hereare collection of all passed nodes
        //    //nodes have it root index to recreate all path
        //    HeapValue[] heapValues = GenericPoolArray<HeapValue>.Take(HEAP_VALUE_BASE_LENGTH);
        //    int heapValuesCount = 0;
        //    CellConnection[] heapRelativeConnections = GenericPoolArray<CellConnection>.Take(HEAP_VALUE_BASE_LENGTH);

        //    //flags that tell result and if it ever was
        //    HeapValue? lastNode = null;

        //    //adding start cell connections in special way
        //    usedCells[startCell.globalID] = true;

        //    int cellConnectionsCount = startCell.connectionsCount;
        //    CellConnection[] cellConnections = startCell.connections;

        //    for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
        //        CellConnection cellConnection = cellConnections[connectionIndex];                
        //        Cell con = globalCells[cellConnection.connection];

        //        if ((1 << con.bitMaskLayer & layerMask) != 0) {
        //            HeapValue heapValue = new HeapValue(-1, heapValuesCount, cellConnection.Cost(startPosition, properties, ignoreCrouchCost), 0, con.globalID);
        //            heapValues[heapValuesCount] = heapValue;
        //            heapRelativeConnections[heapValuesCount] = cellConnection;
        //            heapValuesCount++;
        //            if (heapValuesCount == heapValues.Length) {
        //                GenericPoolArray<HeapValue>.IncreaseSize(ref heapValues);
        //                GenericPoolArray<CellConnection>.IncreaseSize(ref heapRelativeConnections);              
        //            }
        //            heap.Add(heapValue, heapValue.globalCost + GetHeuristic(targetPosition, con.centerVector3));
        //        }
        //    }


        //    int iterations = 0;
        //    System.Diagnostics.Stopwatch stopwatch = GenericPool<System.Diagnostics.Stopwatch>.Take();
        //    stopwatch.Start();
        //    resultType = PathResultType.InvalidInternalIssue;

        //    while (true) {
        //        if (heap.count == 0)
        //            break;

        //        iterations++;
        //        if (iterations > 10) { //every 10 iterations we check if we exeed limits
        //            iterations = 0;
        //            if (stopwatch.ElapsedMilliseconds > maxExecutionTime) {
        //                resultType = PathResultType.InvalidExceedTimeLimit;
        //                break;
        //            }
        //        }
        //        float curNodeWeight;
        //        HeapValue curNode = heap.RemoveFirst(out curNodeWeight);
        //        Cell curNodeCell = globalCells[curNode.connectionGlobalIndex];
        //        usedCells[curNodeCell.globalID] = true;

        //        if ((1 << curNodeCell.bitMaskLayer & layerMask) == 0)
        //            continue;

        //        //var c = heapRelativeConnections[curNode.index];
        //        //Cell c1 = globalCells[c.from];
        //        //Cell c2 = globalCells[c.connection];
        //        //Vector3 v1 = c1.centerVector3;
        //        //Vector3 v2 = c2.centerVector3;
        //        //Debuger_K.AddLine(v1, v2, Color.red, 0.1f);
        //        //Debuger_K.AddLabel(SomeMath.MidPoint(v1, v2), curNodeWeight);

        //        if (curNodeCell == targetCell) {//found path
        //            lastNode = curNode;
        //            break;
        //        }

        //        cellConnectionsCount = curNodeCell.connectionsCount;
        //        cellConnections = curNodeCell.connections;

        //        for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
        //            CellConnection cellConnection = cellConnections[connectionIndex];

        //            if (usedCells[cellConnection.connection])
        //                continue;

        //            Cell con = globalCells[cellConnection.connection];

        //            if ((1 << con.bitMaskLayer & layerMask) == 0)
        //                continue;

        //            //new node in heap values
        //            HeapValue heapValue = new HeapValue(
        //                curNode.index,
        //                heapValuesCount,
        //                curNode.globalCost + cellConnection.Cost(properties, ignoreCrouchCost),
        //                curNode.depth + 1,
        //                con.globalID);

        //            //add new node to array of all nodes
        //            heapValues[heapValuesCount] = heapValue;
        //            heapRelativeConnections[heapValuesCount] = cellConnection;

        //            heapValuesCount++;
        //            if (heapValuesCount == heapValues.Length) {
        //                GenericPoolArray<HeapValue>.IncreaseSize(ref heapValues);
        //                GenericPoolArray<CellConnection>.IncreaseSize(ref heapRelativeConnections);
        //            }

        //            //add new node to heap
        //            heap.Add(heapValue, heapValue.globalCost + GetHeuristic(targetPosition, con.centerVector3));
        //        }
        //    }

        //    if (lastNode.HasValue) { //if there is some path
        //        HeapValue node = lastNode.Value;

        //        cellPath = GenericPoolArray<CellConnection>.Take(node.depth + 2);
        //        cellPathCount = node.depth + 1;

        //        for (int i = 0; i < cellPathCount; i++) {
        //            cellPath[node.depth] = heapRelativeConnections[node.index];
        //            if (node.root != -1)
        //                node = heapValues[node.root];
        //        }
        //        resultType = PathResultType.Valid;
        //    }
        //    else {//if there is no path
        //        cellPath = null;
        //        cellPathCount = 0;
        //        if (resultType != PathResultType.InvalidExceedTimeLimit)
        //            resultType = PathResultType.InvalidNoPath;
        //    }

        //    stopwatch.Reset();
        //    GenericPool<System.Diagnostics.Stopwatch>.ReturnToPool(ref stopwatch);

        //    //return to pool temp data 
        //    heap.ReturnToPoolAllocatedData();
        //    GenericPool<HeapFloatFirstLowest<HeapValue>>.ReturnToPool(ref heap);
        //    GenericPoolArray<bool>.ReturnToPool(ref usedCells);
        //    GenericPoolArray<HeapValue>.ReturnToPool(ref heapValues);
        //    GenericPoolArray<CellConnection>.ReturnToPool(ref heapRelativeConnections);

        //    pathAproxCost = lastNode.HasValue ? lastNode.Value.globalCost : 0f;
        //    return lastNode.HasValue;
        //}

        //    protected static bool SearchSimpleWithPredicate(
        //        int layerMask,
        //        int maxExecutionTime,
        //        AgentProperties properties, 
        //        bool ignoreCrouchCost, 
        //        Cell startCell, Vector3 startPosition, 
        //        Predicate<Cell> predicate, 
        //        float maxSearchCost, 
        //        out CellConnection[] cellPath, 
        //        out int cellPathCount,
        //        out PathResultType resultType,
        //        out float pathAproxCost) {
        //        Cell[] globalCells = PathFinderData.cells;

        //        //taking from pool temp data     
        //        bool[] usedCells = GenericPoolArray<bool>.Take(PathFinderData.maxRegisteredCellID + 1, defaultValue: false);
        //        HeapFloatFirstLowest<HeapValue> heap = GenericPool<HeapFloatFirstLowest<HeapValue>>.Take();//heap of nodes
        //        heap.TakeFromPoolAllocatedData(128);

        //        //since we need siquence of nodes hereare collection of all passed nodes
        //        //nodes have it root index to recreate all path
        //        HeapValue[] heapValues = GenericPoolArray<HeapValue>.Take(HEAP_VALUE_BASE_LENGTH);
        //        int heapValuesLength = HEAP_VALUE_BASE_LENGTH;
        //        int heapValuesCount = 0;
        //        CellConnection[] heapRelativeConnections = GenericPoolArray<CellConnection>.Take(HEAP_VALUE_BASE_LENGTH);

        //        //flags that tell resultand if it ever was
        //        HeapValue? lastNode = null;

        //        //adding start cell connections in special way
        //        usedCells[startCell.globalID] = true;

        //        int cellConnectionsCount = startCell.connectionsCount;
        //        CellConnection[] cellConnections = startCell.connections;

        //        for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
        //            CellConnection cellConnection = cellConnections[connectionIndex];

        //            Cell con = globalCells[cellConnection.connection];
        //            if ((1 << con.bitMaskLayer & layerMask) != 0) {
        //                HeapValue heapValue = new HeapValue(-1, heapValuesCount, cellConnection.Cost(startPosition, properties, ignoreCrouchCost), 0, con.globalID);
        //                heapValues[heapValuesCount] = heapValue;
        //                heapRelativeConnections[heapValuesCount] = cellConnection;

        //                heapValuesCount++;
        //                if (heapValuesCount == heapValuesLength) {
        //                    GenericPoolArray<HeapValue>.IncreaseSize(ref heapValues);
        //                    GenericPoolArray<CellConnection>.IncreaseSize(ref heapRelativeConnections);
        //                    heapValuesLength *= 2;
        //                }
        //                heap.Add(heapValue, heapValue.globalCost);
        //            }
        //        }


        //        int iterations = 0;
        //        System.Diagnostics.Stopwatch stopwatch = GenericPool<System.Diagnostics.Stopwatch>.Take();
        //        stopwatch.Start();
        //        resultType = PathResultType.InvalidInternalIssue;

        //        while (true) {
        //            if (heap.count == 0)
        //                break;

        //            iterations++;
        //            if (iterations > 50) { //every 50 iterations we check if we exeed limits
        //                iterations = 0;
        //                if (stopwatch.ElapsedMilliseconds > maxExecutionTime) {
        //                    resultType = PathResultType.InvalidExceedTimeLimit;
        //                    break;
        //                }
        //            }

        //            float curNodeWeight;
        //            HeapValue curNode = heap.RemoveFirst(out curNodeWeight);

        //            Cell curNodeCell = globalCells[curNode.connectionGlobalIndex];
        //            usedCells[curNodeCell.globalID] = true;

        //            if ((1 << curNodeCell.bitMaskLayer & layerMask) == 0)
        //                continue;

        //            //var c = curNode.content;
        //            //Cell c1 = c.from;
        //            //Cell c2 = c.connection;
        //            //Vector3 v1 = c1.centerVector3;
        //            //Vector3 v2 = c2.centerVector3;
        //            //Debuger_K.AddLine(v1, v2, Color.red, 0.1f);
        //            //Debuger_K.AddLabel(SomeMath.MidPoint(v1, v2), curNodeWeight);

        //            if (predicate(curNodeCell)) {//found path
        //                lastNode = curNode;
        //                break;
        //            }

        //            cellConnectionsCount = curNodeCell.connectionsCount;
        //            cellConnections = curNodeCell.connections;

        //            for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
        //                CellConnection cellConnection = cellConnections[connectionIndex];

        //                if (usedCells[cellConnection.connection])
        //                    continue;

        //                Cell con = globalCells[cellConnection.connection];      

        //                if ((1 << con.bitMaskLayer & layerMask) == 0)
        //                    continue;

        //                float cost = curNode.globalCost + cellConnection.Cost(properties, ignoreCrouchCost);
        //                if (cost > maxSearchCost)
        //                    continue;

        //                //new node in heap values
        //                HeapValue heapValue = new HeapValue(
        //                    curNode.index,
        //                    heapValuesCount,
        //                    curNode.globalCost + cellConnection.Cost(properties, ignoreCrouchCost),
        //                    curNode.depth + 1,
        //                    con.globalID);

        //                //add new node to arrayof all nodes
        //                heapValues[heapValuesCount] = heapValue;
        //                heapRelativeConnections[heapValuesCount] = cellConnection;
        //                heapValuesCount++;
        //                if (heapValuesCount == heapValuesLength) {
        //                    GenericPoolArray<HeapValue>.IncreaseSize(ref heapValues);
        //                    GenericPoolArray<CellConnection>.IncreaseSize(ref heapRelativeConnections);
        //                    heapValuesLength *= 2;
        //                }

        //                //add new node to heap
        //                heap.Add(heapValue, heapValue.globalCost);
        //            }
        //        }


        //        if (lastNode.HasValue) { //if there is some path
        //            HeapValue node = lastNode.Value;

        //            cellPath = GenericPoolArray<CellConnection>.Take(node.depth + 2);
        //            cellPathCount = node.depth + 1;

        //            for (int i = 0; i < cellPathCount; i++) {
        //                cellPath[node.depth] = heapRelativeConnections[node.index];
        //                if (node.root != -1)
        //                    node = heapValues[node.root];
        //            }
        //            resultType = PathResultType.Valid;
        //        }
        //        else { //if there is no path
        //            cellPath = null;
        //            cellPathCount = 0;
        //            if (resultType != PathResultType.InvalidExceedTimeLimit)
        //                resultType = PathResultType.InvalidNoPath;
        //        }

        //        stopwatch.Reset();
        //        GenericPool<System.Diagnostics.Stopwatch>.ReturnToPool(ref stopwatch);

        //        //return to pool temp data
        //        heap.ReturnToPoolAllocatedData();
        //        GenericPool<HeapFloatFirstLowest<HeapValue>>.ReturnToPool(ref heap);
        //        GenericPoolArray<CellConnection>.ReturnToPool(ref heapRelativeConnections);
        //        GenericPoolArray<bool>.ReturnToPool(ref usedCells);
        //        GenericPoolArray<HeapValue>.ReturnToPool(ref heapValues);
        //        pathAproxCost = lastNode.HasValue ? lastNode.Value.globalCost : 0f;
        //        return lastNode.HasValue;
        //    }
    }
}
