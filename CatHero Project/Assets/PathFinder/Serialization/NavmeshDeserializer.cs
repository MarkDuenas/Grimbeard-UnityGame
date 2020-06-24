using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Serialization2 {
    public class NavmeshDeserializer {
        //public Cell[] cellPool;

        private GameObject[] _gameObjectLibrary;
        private AgentProperties _properties;
        private SerializedNavmesh _serializedData;

        public NavmeshDeserializer(SerializedNavmesh serializedData, AgentProperties properties, GameObject[] gameObjectLibrary) {
            _properties = properties;
            _serializedData = serializedData;
            _gameObjectLibrary = gameObjectLibrary;
        }
        
        public void Deserialize(out Graph[] graphs, ref Cell[] deserializedGlobalCells) {
            SerializedGraph[] serializedGraphs = _serializedData.serializedGraphs;
            graphs = new Graph[serializedGraphs.Length];

            #region cells
            int thisGraphMaxGlobalID = 0;
            for (int graphIndex = 0; graphIndex < serializedGraphs.Length; graphIndex++) {
                SerializedGraph serializedGraph = serializedGraphs[graphIndex];            
                graphs[graphIndex] = new Graph(serializedGraph.chunk, _properties);

                SerializedCell[] cells = serializedGraph.serializedCells;
                for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++) {
                    int cellGlobalID = cells[cellIndex].globalID;      
                    if (thisGraphMaxGlobalID < cellGlobalID)
                        thisGraphMaxGlobalID = cellGlobalID;
                }
            }
            if(deserializedGlobalCells.Length <= thisGraphMaxGlobalID)
                GenericPoolArray<Cell>.IncreaseSizeTo(ref deserializedGlobalCells, thisGraphMaxGlobalID + 1);

            //deserialization of cells
            for (int graphIndex = 0; graphIndex < serializedGraphs.Length; graphIndex++) {
                Graph graph = graphs[graphIndex];
                SerializedCell[] cells = serializedGraphs[graphIndex].serializedCells;
                for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++) {
                    Cell cell = cells[cellIndex].DeserializeCellBody(this, graph);
                    deserializedGlobalCells[cell.globalID] = cell; 
                }
            }
            #endregion       

            for (int graphIndex = 0; graphIndex < serializedGraphs.Length; graphIndex++) {
                SerializedGraph serializedGraph = serializedGraphs[graphIndex];
                Graph graph = graphs[graphIndex];
                
                SerializedCell[] cellsGraphSerialized = serializedGraph.serializedCells;
                Cell[] cellsGraphDeserialized = GenericPoolArray<Cell>.Take(cellsGraphSerialized.Length);

                for (int cellIndex = 0; cellIndex < cellsGraphSerialized.Length; cellIndex++) {
                    var curCell = deserializedGlobalCells[cellsGraphSerialized[cellIndex].globalID];
                    if(curCell.graphID >= cellsGraphSerialized.Length)
                        Debug.LogError("serialized cell graph index higher than it should be");
                    cellsGraphDeserialized[curCell.graphID] = curCell;
                }

                //contour       
                CellContentConnectionData[] borderInfo = serializedGraph.borderInfo;
                IndexLengthInt[] borderInfoLayout = serializedGraph.borderInfoLayout;

                CellContentConnectionData[] borderInfoDeserialized = GenericPoolArray<CellContentConnectionData>.Take(borderInfo.Length);
                for (int i = 0; i < borderInfo.Length; i++) {
                    borderInfoDeserialized[i] = borderInfo[i];
                }

                var graphBorderData = graph.borderData;

                graphBorderData.SetOptimizedData(borderInfoDeserialized, borderInfoLayout, Graph.BORDER_DATA_LENGTH);
                
                //map
                int serializedCellMapResolution = serializedGraph.serializedCellMapResolution;
                int[] serializedCellRichMap = serializedGraph.serializedRichCellMap;
                int[] serializedCellSimpleMap = serializedGraph.serializedSimpleCellMap;
                IndexLengthInt[] serializedCellRichMapLayout = serializedGraph.serializedRichCellMapLayout;
                IndexLengthInt[] serializedCellSimpleMapLayout = serializedGraph.serializedSimpleCellMapLayout;
                bool serializedSimpleMapHaveEmptySpots = serializedGraph.serializedSimpleMapHaveEmptySpots;

                Cell[] cellRichMap = GenericPoolArray<Cell>.Take(serializedCellRichMap.Length);
                Cell[] cellSimpleMap = GenericPoolArray<Cell>.Take(serializedCellSimpleMap.Length);
                IndexLengthInt[] cellRichMapLayout = GenericPoolArray<IndexLengthInt>.Take(serializedCellRichMapLayout.Length);
                IndexLengthInt[] cellSimpleMapLayout = GenericPoolArray<IndexLengthInt>.Take(serializedCellSimpleMapLayout.Length);

                for (int i = 0; i < serializedCellRichMap.Length; i++) {
                    cellRichMap[i] = deserializedGlobalCells[serializedCellRichMap[i]];
                }
                for (int i = 0; i < serializedCellSimpleMap.Length; i++) {
                    cellSimpleMap[i] = deserializedGlobalCells[serializedCellSimpleMap[i]];
                }

                int cmrSqr = serializedCellMapResolution * serializedCellMapResolution;
                for (int i = 0; i < cmrSqr; i++) {
                    cellRichMapLayout[i] = serializedCellRichMapLayout[i];
                }
                for (int i = 0; i < cmrSqr; i++) {
                    cellSimpleMapLayout[i] = serializedCellSimpleMapLayout[i];
                }

                graph.SetCellMapFromSerialization(
                    serializedCellMapResolution,
                    cellRichMap, cellRichMapLayout,
                    cellSimpleMap, cellSimpleMapLayout,
                    serializedSimpleMapHaveEmptySpots);

                //TOTAL MAP VERY IMPORTANT TO MAKE IT RIGHT
                //here deserialized tital map and all connections
                //also all connections added to relative cells
                //(so make it right)                

                if (cellsGraphSerialized.Length == 0) {
                    //nothig to deserialize. just place empty data
                    CellContentData[] tempData = GenericPoolArray<CellContentData>.Take(1);
                    CellConnection[] tempValue = GenericPoolArray<CellConnection>.Take(1);
                    IndexLengthInt[] tempLayout = GenericPoolArray<IndexLengthInt>.Take(1);

                    graph.SetGraphData(cellsGraphDeserialized, 0, tempData, tempValue, tempLayout);

                    GenericPoolArray<CellContentData>.ReturnToPool(ref tempData);
                    GenericPoolArray<CellConnection>.ReturnToPool(ref tempValue);
                    GenericPoolArray<IndexLengthInt>.ReturnToPool(ref tempLayout);
                }
                else {
                    //serialized            
                    CellContentData[] serializedEdges = serializedGraph.serializedEdges;
                    CellConnection[] serializedConnections = serializedGraph.serializedConnections;
                    IndexLengthInt[] serializedLayout = serializedGraph.serializedLayout;

                    //deserialized              
                    CellContentData[] graphEdges = GenericPoolArray<CellContentData>.Take(serializedLayout[serializedLayout.Length - 1].indexPlusLength);
                    CellConnection[] graphConnections = GenericPoolArray<CellConnection>.Take(serializedLayout[serializedLayout.Length - 1].indexPlusLength);
                    IndexLengthInt[] graphLayout = GenericPoolArray<IndexLengthInt>.Take(serializedLayout.Length);

                    for (int cellIndex = 0; cellIndex < serializedLayout.Length; cellIndex++) {
                        IndexLengthInt curLayout = serializedLayout[cellIndex];
                        graphLayout[cellIndex] = curLayout;

                        for (int i = 0; i < curLayout.length; i++) {
                            int index = curLayout.index + i;
                            CellContentData connectionData = serializedEdges[index];
                            CellConnection connection = serializedConnections[index];

                            graphEdges[index] = connectionData;
                            graphConnections[index] = connection;

                            if (connection.type != CellConnectionType.Invalid){
                                deserializedGlobalCells[connection.from].AddConnectionDeserialized(connection, connectionData);                            
                            }
                        }
                    }                    

                    graph.SetGraphData(cellsGraphDeserialized, cellsGraphSerialized.Length, graphEdges, graphConnections, graphLayout);                
                }
                GenericPoolArray<Cell>.ReturnToPool(ref cellsGraphDeserialized);
            }         
        }
        

        public GameObject GetGameObject(int index) {
            return _gameObjectLibrary[index];
        }

        public Area GetArea(int value) {
            Area result = PathFinder.GetArea(value);
            if (result == null)
                result = PathFinder.getDefaultArea;
            return result;
        }
    }


    public struct DeserializationResult {
        public XZPosInt chunkPosition;
        public int chunkMinY, chunkMaxY;
        public Graph graph;

        public DeserializationResult(Graph graph, XZPosInt chunkPosition, int minY, int maxY) {
            this.graph = graph;
            this.chunkPosition = chunkPosition;
            this.chunkMinY = minY;
            this.chunkMaxY = maxY;
        }
    }
}