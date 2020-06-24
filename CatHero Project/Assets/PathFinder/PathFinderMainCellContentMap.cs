using K_PathFinder.CoolTools;
using K_PathFinder.Graphs;
using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder {
    //this part of code are responsible for updating cell content map. which is single point data (at this moment)
    //this data can be extracted later on from navmesh in various ways
    //easy way to store and search Vector3 data in some proximity
    
    //ISSUE:
    //In current inplementation i cant possible know in which chunk point will end up
    //So i need to make some sort of callback to ajaisent chunks when navmesh generation completed so 
    //position of items are recalculated
    public static partial class PathFinder {
        private static List<AgentProperties> cellContentMapTempPropertiesList = new List<AgentProperties>();

        //work batcher that tell what should be done with target
        //it sontain 2 batches of work so adding and performing batch are sepparated process
        private static LockedWorkDictionary<ICellContentValueExternal, CellMapActionType> cellContentMapWorkBatcher = new LockedWorkDictionary<ICellContentValueExternal, CellMapActionType>();

        private static CellContentMapMetaData[] cellContentMapMetaData = new CellContentMapMetaData[32];
        private static int cellContentMapMetaDataCount = 0;
        private static StackedDictionary<XZPosInt, ICellContentValueExternal> cellContentMap = new StackedDictionary<XZPosInt, ICellContentValueExternal>();
        private static StackedList<int> cellContentMapOccupiedCells;

        private static int[] cellContentMapFreeIDs;
        private static int cellContentMapFreeIDsCount = 0;
        
        //simply contain content and its last recorded position
        private struct CellContentMapMetaData {
            public XZPosInt gridPos;

            public CellContentMapMetaData(XZPosInt gridPos) {
                this.gridPos = gridPos;
            }

            public override bool Equals(object obj) {
                if (!(obj is CellContentMapMetaData))
                    return false;

                CellContentMapMetaData mys = (CellContentMapMetaData)obj;
                return mys.gridPos == gridPos;
            }

            public override int GetHashCode() {
                return gridPos.GetHashCode();
            }
        }

        //types of work
        private enum CellMapActionType : int {
            Process = 1,
            Remove = 3
        }

        /// <summary>
        /// Function to add OR update data. 
        /// on next iteration content will be added or updated
        /// </summary>
        public static void ProcessCellContent(ICellContentValueExternal processedContent) {
            cellContentMapWorkBatcher[processedContent] = CellMapActionType.Process;
        }

        /// <summary>
        /// Function to remove content
        /// on next iteration it will be removed
        /// </summary>
        public static void RemoveCellContent(ICellContentValueExternal removedContent) {
            //test it with nulls and bizzare ways to overload equality operators?
            cellContentMapWorkBatcher[removedContent] = CellMapActionType.Remove;
        }

        private static void InitCellContent() {
            if (cellContentMapOccupiedCells == null)
                cellContentMapOccupiedCells = StackedList<int>.PoolTake(32, 32);
            cellContentMapMetaDataCount = 0;

            if (cellContentMapFreeIDs == null)
                cellContentMapFreeIDs = GenericPoolArray<int>.Take(32);
            cellContentMapFreeIDsCount = 0;
        }

        private static void ClearCellContent() {
            cellContentMapWorkBatcher.Clear();
            cellContentMapMetaDataCount = 0;
            cellContentMapFreeIDsCount = 0;
            cellContentMapOccupiedCells.Clear();
            cellContentMap.Clear();
        }



        /// <summary>
        /// process current cell contents and return amount of processed values
        /// </summary>
        private static int ProcessCellContentEvents() {
            try {
                Dictionary<ICellContentValueExternal, CellMapActionType> curWorkBatch = cellContentMapWorkBatcher.GetCurrentBatch();
                cellContentMapTempPropertiesList.Clear();

                lock (allAgentProperties) {
                    foreach (var ap in allAgentProperties) {
                        if ((ap.internal_flagList & 1) != 0)
                            cellContentMapTempPropertiesList.Add(ap);
                    }
                }

                Cell[] globalCells = PathFinderData.cells;
                int[] pooledArray = GenericPoolArray<int>.Take(32);
                int pooledArrayLength;


                foreach (var work in curWorkBatch) {
                    var position = work.Key.position;
                    float maxDistance = work.Key.maxNavmeshDistance;
                    int id = work.Key.pathFinderID;

                    if (id != 0) {
                        CellContentMapMetaData metaData = cellContentMapMetaData[id];
                        //remove from global map so it is not added to graph
                        cellContentMap.Remove(metaData.gridPos, work.Key);
                        cellContentMapOccupiedCells.Read(id, ref pooledArray, out pooledArrayLength);

                        //remove from cell
                        //Debug.LogFormat("remove while updating. id {0}, occupied {1}", id, cellContentMapOccupiedCells.ToString(id));
                        for (int p_i = 0; p_i < pooledArrayLength; p_i++) {
                            globalCells[pooledArray[p_i]].cellContentValues.Remove(work.Key);
                        }
                        cellContentMapOccupiedCells.Clear(id);


                        if (work.Value == CellMapActionType.Remove) {
                            //remove from library                            
                            work.Key.pathFinderID = 0;                           
                            if (cellContentMapFreeIDs.Length == cellContentMapFreeIDsCount)
                                GenericPoolArray<int>.IncreaseSize(ref cellContentMapFreeIDs);
                            cellContentMapFreeIDs[cellContentMapFreeIDsCount++] = id;
                            cellContentMapOccupiedCells.Clear(id);
                            continue;
                        }
                    }
                    else {
                        if (work.Value == CellMapActionType.Remove) {
                            //tries to remove while it's already removed
                            continue;
                        }

                        //add this value
                        int newID;
                        if (cellContentMapFreeIDsCount > 0)
                            newID = cellContentMapFreeIDs[--cellContentMapFreeIDsCount];
                        else {
                            newID = cellContentMapMetaDataCount++;

                            if (cellContentMapMetaData.Length == newID)
                                Array.Resize(ref cellContentMapMetaData, cellContentMapMetaData.Length * 2);

                            if (cellContentMapOccupiedCells.baseSize == newID)
                                cellContentMapOccupiedCells.ExpandBaseSize(cellContentMapOccupiedCells.baseSize);
                        }

                        id = newID;
                        work.Key.pathFinderID = newID;                           
                    }

                    for (int i = 0; i < cellContentMapTempPropertiesList.Count; i++) {
                        var property = cellContentMapTempPropertiesList[i];

                        Cell cell;
                        Vector3 navmeshPos;
                        bool outside;
                        if(TryGetClosestCell_Internal(position.x, position.y, position.z, property, out navmeshPos, out cell, out outside) && 
                            cell != null && 
                            SomeMath.SqrDistance(position, navmeshPos) < maxDistance * maxDistance) {
                            cellContentMapOccupiedCells.AddLast(id, cell.globalID);
                            cell.AddCellContentValue(work.Key);
                        }                       
                    }

                    XZPosInt newGridPos = ToChunkPosition(position);
                    cellContentMap.Add(newGridPos, work.Key);
                    cellContentMapMetaData[id].gridPos = newGridPos;                    
                }

                int workCount = curWorkBatch.Count;
                curWorkBatch.Clear();
                GenericPoolArray<int>.ReturnToPool(ref pooledArray);
                return workCount;
            }
            catch (Exception e) {
                Debug.LogError(e);
                throw;
            }
        }

        static void AddToGraphExternalCellContent(Graph graph) {
            ICellContentValueExternal[] pooledArray = GenericPoolArray<ICellContentValueExternal>.Take(32);
            int count;
            cellContentMap.Read(graph.chunk.xzPos, ref pooledArray, out count);

            for (int i = 0; i < count; i++) {
                ICellContentValueExternal val = pooledArray[i];
                Vector3 position = val.position;
                float maxDistance = val.maxNavmeshDistance;

                //make a little offset so position that laying exactly on edges dont cause lots of trobles
                if (position.x % gridSize < 0.001f) position.x += 0.001f;
                if (position.z % gridSize < 0.001f) position.z += 0.001f;

                Cell cell;
                Vector3 navmeshPos;
                bool outside;
                if (graph.GetCellSimpleMap(position.x, position.y, position.z, -1, out cell, out navmeshPos, out outside) &&
                    cell != null &&
                    SomeMath.SqrDistance(position, navmeshPos) < maxDistance * maxDistance) {
                    cellContentMapOccupiedCells.AddLast(val.pathFinderID, cell.globalID);
                    cell.AddCellContentValue(val);
                }
            }       

            GenericPoolArray<ICellContentValueExternal>.ReturnToPool(ref pooledArray);
        }

        static void RemoveFromGraphExternalCellContent(Graph graph) {
            Cell[] cells;
            int cellsCount;
            graph.GetCells(out cells, out cellsCount);

            //Debug.LogWarningFormat("RemoveFromGraphExternalCellContent {0}", graph.chunk);

            for (int c = 0; c < cellsCount; c++) {
                Cell cell = cells[c];
                foreach (var item in cell.cellContentValues) {
                    if (item is ICellContentValueExternal) {
                        cellContentMapOccupiedCells.Remove((item as ICellContentValueExternal).pathFinderID, cell.globalID);

                        //int curID = (item as ICellContentValueExternal).pathFinderID;
                        //Debug.LogWarningFormat("id {0}, cell {1}, removed {2}, now over {3}",
                        //    curID,
                        //    cell.globalID,
                        //    cellContentMapOccupiedCells.Remove(curID, cell.globalID),
                        //    cellContentMapOccupiedCells.ToString(curID));
                    }
                }
            }
        }


        //static void RequeueCellContentWork(XZPosInt pos, AgentProperties property) {
        //    CellContentMapDataSet set;
        //    if (cellContentMapDataSets.TryGetValue(property, out set)) {
        //        List<ICellContentValueExternal> tempList = new List<ICellContentValueExternal>();
        //        set.map.Read(pos, tempList);
        //        foreach (var item in tempList) {
        //            ProcessCellContent(item);
        //        }
        //    }
        //}
#if UNITY_EDITOR
        public static void DebugCellsContent() {
            int counter = 0;
            StringBuilder sb = new StringBuilder();
            foreach (var graph in chunkData.Values) {
                graph.DebugContent(sb, ref counter);
            }
            Debug.Log("Content count " + counter);
            Debug.Log(sb.ToString());
        }
#endif

        public static int eventCellContentWorkCount {
            get { return cellContentMapWorkBatcher.workCount; }
        }

        public static bool eventCellContentHaveWork {
            get { return eventCellContentWorkCount > 0; }
        }

    }
}
