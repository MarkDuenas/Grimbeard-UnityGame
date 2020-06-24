using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Serialization2 {
    [Serializable]
    public class SerializedCell {
        public int globalID, graphID, layer, bitMaskLayer, area, passability;
        public bool isAdvancedAreaCell;
        public Vector3 center;
        public List<CellContentData> originalEdges = new List<CellContentData>();

        public SerializedCell(NavmeshSerializer ns, Cell cell) {
            globalID = cell.globalID;
            graphID = cell.graphID;
            layer = cell.layer;
            bitMaskLayer = cell.bitMaskLayer;

            isAdvancedAreaCell = cell.advancedAreaCell;
            if (cell.advancedAreaCell) {
                AreaAdvanced aa = cell.area as AreaAdvanced;
                area = ns.GetGameObjectID(aa.container.gameObject);
            }
            else {
                area = cell.area.id;
            }

            passability = (int)cell.passability;
            center = cell.centerVector3;

            CellContentData[] cellOriginalEdges = cell.originalEdges;
            int cellOriginalEdgesCount = cell.originalEdgesCount;
            for (int i = 0; i < cellOriginalEdgesCount; i++) {
                originalEdges.Add(cellOriginalEdges[i]);
            }
        }

        public Cell DeserializeCellBody(NavmeshDeserializer nd, Graph graph) {
            Area cellArea;
            if (isAdvancedAreaCell) {
                GameObject targetGO = nd.GetGameObject(area);
                if (targetGO == null) {
                    Debug.LogWarning("Deserializer cant find GameObject so Cell area became default area");
                    cellArea = PathFinder.getDefaultArea;
                }
                else {
                    AreaWorldMod areaWorldMod = targetGO.GetComponent<AreaWorldMod>();
                    if (areaWorldMod == null) {
                        Debug.LogWarning("Deserializer cant find AreaModifyer on gameObject so Cell area became default area");
                        cellArea = PathFinder.getDefaultArea;
                    }
                    else {
                        if (areaWorldMod.useAdvancedArea == false) {
                            Debug.LogWarning("Area Modifyer don't use advanced area so Cell area became default area");
                            cellArea = PathFinder.getDefaultArea;
                        }
                        else {
                            cellArea = areaWorldMod.advancedArea;
                        }
                    }
                }
            }
            else {
                cellArea = nd.GetArea(area);
            }

            CellContentData[] newCellOriginalEdges = GenericPoolArray<CellContentData>.Take(originalEdges.Count);
            for (int i = 0; i < originalEdges.Count; i++) {
                newCellOriginalEdges[i] = originalEdges[i];
            }

            Cell newC = new Cell(cellArea, (Passability)passability, layer, graph);
            newC.SetCenter(center);
            newC.bitMaskLayer = bitMaskLayer;
            newC.originalEdges = newCellOriginalEdges;
            newC.originalEdgesCount = originalEdges.Count;
            newC.globalID = globalID;
            newC.graphID = graphID;
            return newC;
        }
    }

    //[Serializable]
    //public struct SerializedConnection {
    //    public CellConnectionType type;
    //    //public CellContentData data;
    //    public int fromCell, connectedCell;
    //    public Vector3 intersection;
    //    public float costFrom, costTo;

    //    public SerializedConnection(CellConnection connection) {
    //        type = connection.type;
    //        fromCell = connection.from;
    //        connectedCell = connection.connection;
    //        //data = connection.cellData;
    //        intersection = connection.intersection;
    //        costFrom = connection.costFrom;
    //        costTo = connection.costTo;
    //    }

    //    //public Vector3 enterPoint { get { return data.leftV3; } }
    //    //public Vector3 axis { get { return intersection; } }
    //    //public Vector3 exitPoint { get { return data.rightV3; } }
    //}


    //[Serializable]
    //public class SerializedNormalConnection {
    //    public int fromCell, connectedCell;
    //    public float costFrom, costTo;
    //    public Vector3 intersection;
    //    public CellContentData data;

    //    public SerializedNormalConnection(CellContentGenericConnection connection) { 
    //        fromCell = connection.from.globalID;
    //        connectedCell = connection.connection.globalID;
    //        data = connection.cellData;
    //        intersection = connection.intersection;
    //        costFrom = connection.costFrom;
    //        costTo = connection.costTo;
    //    }
    //}

    //[Serializable]
    //public class SerializedJumpConnection {
    //    public Vector3 enterPoint, lowerStandingPoint, exitPoint, axis;
    //    public int fromCell, connectedCell, jumpState;

    //    public SerializedJumpConnection(CellContentPointedConnection connection) {   
    //        fromCell = connection.from.globalID;
    //        connectedCell = connection.connection.globalID;
    //        enterPoint = connection.enterPoint;
    //        lowerStandingPoint = connection.lowerStandingPoint;
    //        exitPoint = connection.exitPoint;
    //        axis = connection.axis;
    //        jumpState = (int)connection.jumpState;
    //    }
    //}
}