using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder {
    //class without static constructor to hold some general data for current pathfinder state
    public static class PathFinderData {
        //cells
        //escential for having general array for all cells
        public static int[] cellFreeIDs;
        public static int cellFreeIDsCount;
        public static int cellIDused;
        public static object cellIDlock = new object();

        //value for pathfinder thread
        public static int maxRegisteredCellID;
        public static Cell[] cells;

        #region cell ID
        //part of coderesponcible for assigning cells IDs. not really userful outside internal usage
        public static void CellIDManagerInit() {
            if (cellFreeIDs == null)
                cellFreeIDs = GenericPoolArray<int>.Take(128);     
            if (cells == null)
                cells = GenericPoolArray<Cell>.Take(128);
        }

        public static void CellIDManagerClear() {
            cellFreeIDsCount = 0;
            cellIDused = 0;
            if (cells != null)
                Array.Clear(cells, 0, cells.Length);
        }

        public static void SetCellIDManagerState(Cell[] cellsArray) {
            lock (cellIDlock) {
                if (cells != null)
                    GenericPoolArray<Cell>.ReturnToPool(ref cells);
                if (cellFreeIDs != null)
                    GenericPoolArray<int>.ReturnToPool(ref cellFreeIDs);

                cells = cellsArray;

                bool[] flags = GenericPoolArray<bool>.Take(cellsArray.Length + 1);
      
                for (int i = 0; i < cellsArray.Length; i++) {
                    Cell cell = cellsArray[i];
                    if (cell == null)
                        flags[i] = false;
                    else {
                        flags[i] = true;
                        if (cellIDused < cell.globalID)
                            cellIDused = cell.globalID;
                    }
                }
                maxRegisteredCellID = cellIDused;

                cellFreeIDs = GenericPoolArray<int>.Take(128, true);
                cellFreeIDsCount = 0;
                for (int i = 0; i < cellIDused; i++) {
                    if(flags[i] == false) {
                        if(cellFreeIDs.Length == cellFreeIDsCount) 
                            GenericPoolArray<int>.IncreaseSize(ref cellFreeIDs);
                        cellFreeIDs[cellFreeIDsCount++] = i;
                    }
                }    
            }
        }

        //this function used in pathfinder main thread and it intended to set up data for all other pathfinder code that called in main pipeline
        public static void AddCells(Cell[] passedCells, int passedCellsCount) {
            for (int i = 0; i < passedCellsCount; i++) {
                Cell cell = passedCells[i];

                if (cell.globalID >= cells.Length)
                    GenericPoolArray<Cell>.IncreaseSizeTo(ref cells, cell.globalID + 1);

                cells[cell.globalID] = cell;
                if (maxRegisteredCellID < cell.globalID)
                    maxRegisteredCellID = cell.globalID;
            }
        }

        public static void RemoveCells(Cell[] passedCells, int passedCellsCount) {
            for (int i = 0; i < passedCellsCount; i++) {
                int id = passedCells[i].globalID;
                if (id >= cells.Length)
                    throw new Exception("removed cell id is higher than it can possible be");
                cells[passedCells[i].globalID] = null;
            }
        }

        public static int GetFreeCellID() {
            lock (cellIDlock) {
                if (cellFreeIDsCount == 0) {
                    return cellIDused++;
                }
                else
                    return cellFreeIDs[--cellFreeIDsCount];
            }
        }

        public static void ReturnFreeCellID(int id) {
            lock (cellIDlock) {
                if (cellFreeIDsCount >= cellFreeIDs.Length)
                    GenericPoolArray<int>.IncreaseSize(ref cellFreeIDs);
                cellFreeIDs[cellFreeIDsCount++] = id;
            }
        }

        public static void ReturnFreeCellID(int[] ids, int idsLength) {
            lock (cellIDlock) {
                if (cellFreeIDsCount + idsLength >= cellFreeIDs.Length)
                    GenericPoolArray<int>.IncreaseSizeTo(ref cellFreeIDs, cellFreeIDs.Length + idsLength);

                for (int i = 0; i < idsLength; i++) {
                    cellFreeIDs[cellFreeIDsCount++] = ids[i];
                }
            }
        }
        public static void ReturnFreeCellID(int[] ids) {
            ReturnFreeCellID(ids, ids.Length);
        }
        public static void CellFreeIDsDebug() {
            StringBuilder sb = new StringBuilder();
            lock (cellIDlock) {
                sb.AppendFormat("Cell IDs State:\nUsed IDs: {0}\nFree IDs count {1}\nFree IDs:", cellIDused, cellFreeIDsCount);
                string form = "{0} ,";
                for (int i = 0; i < cellFreeIDsCount; i++) {
                    sb.AppendFormat(form, cellFreeIDs[i]);
                }
            }
            Debug.Log(sb.ToString());
        }

        #endregion
    }
}