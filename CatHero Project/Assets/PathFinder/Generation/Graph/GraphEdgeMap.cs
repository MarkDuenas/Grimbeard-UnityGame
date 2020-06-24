using K_PathFinder.CoolTools;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//namespace K_PathFinder.Graphs {
//    //map of edges inside graph
//    //used in raycasting
//    //current rule: in layout first edges should have connection
//    //and second edges can have nulls
//    //cause order is matter
//    public partial class Graph {
//        public CellContentConnectionData[] edgeMapData;
//        public IndexLengthInt[] edgeMapDataLayout;

//        StackedList<CellContentConnectionData> edgeMapTempStackedList;
//        CellContentConnectionData currentAddedEdgeValue, currentRemovedEdgeValue;
//        //Cell currentRemoveTarget;

//        private void GenerateInitialEdgeMap(IEnumerable<Cell> cells) {
//            int cgs = PathFinder.CELL_GRID_SIZE;
//            float pixelSize = cgs / PathFinder.gridSize;
//            edgeMapTempStackedList = StackedList<CellContentConnectionData>.PoolTake(cgs * cgs, cgs);

//            float chunkRealX = chunk.realX;
//            float chunkRealZ = chunk.realZ;
            
//            foreach (var cell in cells) {
//                foreach (var pair in cell.cellDataDictionary) {
//                    bool add = false;

//                    if (pair.Value != null) {
//                        if (pair.Value is CellContentGenericConnection) {//skip jump connections since they dont participate in raycasting right now           
//                            currentAddedEdgeValue = new CellContentConnectionData(cell, pair.Value.connection, pair.Key);
//                            add = true;
//                        }
//                    }
//                    else {
//                        currentAddedEdgeValue = new CellContentConnectionData(cell, null, pair.Key);
//                        add = true;
//                    }
//                    if (add) {
//                        CellContentData data = currentAddedEdgeValue.data;
//                        DDARasterization.DrawLine(
//                            data.xLeft - chunkRealX, 
//                            data.zLeft - chunkRealZ, 
//                            data.xRight - chunkRealX, 
//                            data.zRight - chunkRealZ,
//                            pixelSize, 
//                            AddEdgeToMapDelegate);
//                    }
//                }
//            }   

//            edgeMapTempStackedList.GetOptimizedData(out edgeMapData, out edgeMapDataLayout);
//            StackedList<CellContentConnectionData>.PoolReturn(ref edgeMapTempStackedList);
//        }

//        //public void AddEdgeToMap(Cell origin, Cell connection, CellContentData data) {
//        //    int cgs = PathFinder.CELL_GRID_SIZE; 
//        //    edgeMapTempStackedList = StackedList<CellContentConnectionData>.PoolTake(cgs * cgs, cgs);
//        //    edgeMapTempStackedList.SetOptimizedData(edgeMapData, edgeMapDataLayout, cgs * cgs);
//        //    GenericPoolArray<CellContentConnectionData>.ReturnToPool(ref edgeMapData);
//        //    GenericPoolArray<IndexLengthInt>.ReturnToPool(ref edgeMapDataLayout);
//        //    currentAddedEdgeValue = new CellContentConnectionData(origin, connection, data);

//        //    DDARasterization.DrawLine(
//        //        data.xLeft - chunk.realX,
//        //        data.zLeft - chunk.realZ,
//        //        data.xRight - chunk.realX,
//        //        data.zRight - chunk.realZ,
//        //        cgs / PathFinder.gridSize,
//        //        AddEdgeToMapDelegate);

//        //    edgeMapTempStackedList.GetOptimizedData(out edgeMapData, out edgeMapDataLayout);
//        //    StackedList<CellContentConnectionData>.PoolReturn(ref edgeMapTempStackedList);
//        //}

//        //public void RemoveEdgeFromMap(Cell connection, CellContentData data) {
//        //    int cgs = PathFinder.CELL_GRID_SIZE;
//        //    edgeMapTempStackedList = StackedList<CellContentConnectionData>.PoolTake(cgs * cgs, cgs);
//        //    edgeMapTempStackedList.SetOptimizedData(edgeMapData, edgeMapDataLayout, cgs * cgs);
//        //    GenericPoolArray<CellContentConnectionData>.ReturnToPool(ref edgeMapData);
//        //    GenericPoolArray<IndexLengthInt>.ReturnToPool(ref edgeMapDataLayout);
//        //    currentRemoveTarget = connection;

//        //    DDARasterization.DrawLine(
//        //        data.xLeft - chunk.realX,
//        //        data.zLeft - chunk.realZ,
//        //        data.xRight - chunk.realX,
//        //        data.zRight - chunk.realZ,
//        //        cgs / PathFinder.gridSize,
//        //        RemoveEdgeFromMapDelegate);

//        //    edgeMapTempStackedList.GetOptimizedData(out edgeMapData, out edgeMapDataLayout);
//        //    StackedList<CellContentConnectionData>.PoolReturn(ref edgeMapTempStackedList);
//        //}

//        public void AddEdgesToMap(List<CellContentConnectionData> datas) {
//            int cgs = PathFinder.CELL_GRID_SIZE;
//            edgeMapTempStackedList = StackedList<CellContentConnectionData>.PoolTake(cgs * cgs, cgs);
//            edgeMapTempStackedList.SetOptimizedData(edgeMapData, edgeMapDataLayout, cgs * cgs);
//            GenericPoolArray<CellContentConnectionData>.ReturnToPool(ref edgeMapData);
//            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref edgeMapDataLayout);        

//            for (int i = 0; i < datas.Count; i++) {
//                currentAddedEdgeValue = datas[i];
//                var data = currentAddedEdgeValue.data;
//                DDARasterization.DrawLine(
//                    data.xLeft - chunk.realX,
//                    data.zLeft - chunk.realZ,
//                    data.xRight - chunk.realX,
//                    data.zRight - chunk.realZ,
//                    cgs / PathFinder.gridSize,
//                    AddEdgeToMapDelegate);
//            }

//            edgeMapTempStackedList.GetOptimizedData(out edgeMapData, out edgeMapDataLayout);
//            StackedList<CellContentConnectionData>.PoolReturn(ref edgeMapTempStackedList);
//        }

//        void RemoveEdgesFromMap(List<CellContentConnectionData> datas) {
//            int cgs = PathFinder.CELL_GRID_SIZE;
            
//            edgeMapTempStackedList = StackedList<CellContentConnectionData>.PoolTake(cgs * cgs, cgs);
//            edgeMapTempStackedList.SetOptimizedData(edgeMapData, edgeMapDataLayout, cgs * cgs);

//            GenericPoolArray<CellContentConnectionData>.ReturnToPool(ref edgeMapData);
//            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref edgeMapDataLayout);

//            for (int i = 0; i < datas.Count; i++) {
//                currentRemovedEdgeValue = datas[i];
//                var data = currentRemovedEdgeValue.data;
//                DDARasterization.DrawLine(
//                    data.xLeft - chunk.realX,
//                    data.zLeft - chunk.realZ,
//                    data.xRight - chunk.realX,
//                    data.zRight - chunk.realZ,
//                    cgs / PathFinder.gridSize,
//                    RemoveEdgeFromMapDelegate);
//            }

//            edgeMapTempStackedList.GetOptimizedData(out edgeMapData, out edgeMapDataLayout);
//            StackedList<CellContentConnectionData>.PoolReturn(ref edgeMapTempStackedList);
//        }

//        void AddEdgeToMapDelegate(int x, int z) {
//            if (x < 0)
//                x = 0;
//            else if (x > PathFinder.CELL_GRID_SIZE - 1)
//                x = PathFinder.CELL_GRID_SIZE - 1;

//            if (z < 0)
//                z = 0;
//            else if (z > PathFinder.CELL_GRID_SIZE - 1)
//                z = PathFinder.CELL_GRID_SIZE - 1;

//            if (currentAddedEdgeValue.connection != null)
//                edgeMapTempStackedList.AddFirst((PathFinder.CELL_GRID_SIZE * z) + x, currentAddedEdgeValue);
//            else
//                edgeMapTempStackedList.AddLast((PathFinder.CELL_GRID_SIZE * z) + x, currentAddedEdgeValue);
//        }

//        void RemoveEdgeFromMapDelegate(int x, int z) {
//            if (x < 0)
//                x = 0;
//            else if (x > PathFinder.CELL_GRID_SIZE - 1)
//                x = PathFinder.CELL_GRID_SIZE - 1;

//            if (z < 0)
//                z = 0;
//            else if (z > PathFinder.CELL_GRID_SIZE - 1)
//                z = PathFinder.CELL_GRID_SIZE - 1;

//            edgeMapTempStackedList.Remove((PathFinder.CELL_GRID_SIZE * z) + x, currentRemovedEdgeValue);
//        }

//        //bool RemoveEdgePredicate(CellContentConnectionData value) {
//        //    return value.connection == currentRemoveTarget;
//        //}

//        public static int GetEdgeMapIndex(int x, int z) {
//            return (PathFinder.CELL_GRID_SIZE * z) + x;
//        }
//    }
//}


