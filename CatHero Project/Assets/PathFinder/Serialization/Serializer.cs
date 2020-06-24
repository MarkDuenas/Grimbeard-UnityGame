using UnityEngine;
using System;
using System.Collections.Generic;

using K_PathFinder.VectorInt;
using K_PathFinder.Graphs;
using K_PathFinder.NodesNameSpace;
using K_PathFinder.EdgesNameSpace;
using K_PathFinder;
using K_PathFinder.CoverNamespace;
using System.Text;

//namespace K_PathFinder.Serialization {
//    public class NavmeshLayserSerializer {
//        //Dictionary<Cell, int> cells = new Dictionary<Cell, int>();
//        List<Graph> targetGraphs = new List<Graph>();

//        //Dictionary<BattleGridPoint, int> bgIDs = new Dictionary<BattleGridPoint, int>();
//        Dictionary<GameObject, int> gameObjectLibraryIDs;

//        public int cellCount = 0;

//        //create cell dictionary
//        public NavmeshLayserSerializer(Dictionary<GeneralXZData, Graph> chunkData, Dictionary<XZPosInt, YRangeInt> chunkRangeData, Dictionary<GameObject, int> gameObjectLibraryIDs, AgentProperties properties) {
//            //int cellCounter = 0
//            //    //, bgpCounter = 0
//            //    ;

//            this.gameObjectLibraryIDs = gameObjectLibraryIDs;

//            //creating cell dictionary cause connections are lead outside chunk

//            foreach (var graph in chunkData.Values) {
//                if (graph.empty || graph.properties != properties)
//                    continue;

//                targetGraphs.Add(graph);


//                //Cell[] cellsArray;
//                //int cellsCount;
//                //graph.GetCells(out cellsArray, out cellsCount);

//                //for (int cellID = 0; cellID < cellsCount; cellID++) {
//                //    cells.Add(cellsArray[cellID], cellCounter++);
//                //}

//                //var samplePoints = graph.samplePoints;

//                //BattleGrid bg = graph.battleGrid;
//                //if (bg != null) {
//                //    foreach (var p in bg.points) {
//                //        bgIDs.Add(p, bgpCounter++);
//                //    }
//                //}
//            }
//        }
        
//        public SerializedNavmesh Serialize() {
//            SerializedNavmesh serializedNM = new SerializedNavmesh();   
//            serializedNM.serializedGraphs = new List<SerializedGraph>();
//            serializedNM.cellCount = 0;

//            foreach (var graph in targetGraphs) {
//                GraphSerializer serializer = new GraphSerializer(this, graph);
//                serializedNM.serializedGraphs.Add(serializer.Serialize());   
//            }

//            //serializedNM.cellCount = cellCount;
//            //serializedNM.bgPointsCount = bgIDs.Count;

//            Debug.LogFormat("saved {0} graphs", serializedNM.serializedGraphs.Count);
//            return serializedNM;
//        }

//        //public int GetCellID(Cell cell) {
//        //    return cells[cell];
//        //}
//        //public int GetBattleGridID(BattleGridPoint p) {
//        //    return bgIDs[p];
//        //}
//        public int GetGameObjectID(GameObject go) {
//            int result;

//            if (gameObjectLibraryIDs.TryGetValue(go, out result) == false) {
//                result = gameObjectLibraryIDs.Count;
//                gameObjectLibraryIDs.Add(go, result);
//            }
//            return result;
//        }
//    }
    
//    public class NavmeshLayerDeserializer {
//        public const int INVALID_VALUE = -1;

//        SerializedNavmesh _serializedData;
//        //Cell[] _cells;
//        //BattleGridPoint[] _points;
//        AgentProperties _properties;

//        public Cell[] cellPool;
//        //public BattleGridPoint[] bgPool = null;
//        public GameObject[] gameObjectLibrary;

//        public NavmeshLayerDeserializer(SerializedNavmesh serializedData, AgentProperties properties, GameObject[] gameObjectLibrary) {
//            _properties = properties;
//            _serializedData = serializedData;
//            cellPool = new Cell[serializedData.cellCount];
//            //if (properties.battleGrid && serializedData.bgPointsCount != 0)
//            //    bgPool = new BattleGridPoint[serializedData.bgPointsCount];

//            this.gameObjectLibrary = gameObjectLibrary;         
//        }

//        public Area GetArea(int value) {
//            Area result = PathFinder.GetArea(value);
//            if (result == null)
//                result = PathFinder.getDefaultArea;
//            return result;
//        }

//        public GameObject GetGameObject(int index) {
//            return gameObjectLibrary[index];
//        }

//        public SerializedNavmesh serializedData {
//            get { return _serializedData; }
//        }
        
//        public List<DeserializationResult> Deserialize() {
//            List<GraphDeserializer> graphDeserializers = new List<GraphDeserializer>();

//            foreach (var graph in _serializedData.serializedGraphs) {
//                graphDeserializers.Add(new GraphDeserializer(graph, this, _properties));
//            }

//            //now we have populate cell dictionary and have all cells in layer
//            foreach (var gd in graphDeserializers) {
//                gd.DeserializeCells();
//            }
 
//            //now all cells have connections
//            foreach (var gd in graphDeserializers) {
//                gd.DeserializeConnections(_properties.canJump);
//            }

//            if (_properties.canCover) {
//                foreach (var gd in graphDeserializers) {
//                    gd.DeserializeCovers();
//                }
//            }

//            //if (_properties.samplePoints) {
//            //    //foreach (var gd in graphDeserializers) {
//            //    //    gd.DeserializeBattleGridPoints();
//            //    //}
//            //    //foreach (var gd in graphDeserializers) {
//            //    //    gd.ConnectBattleGridPoints();
//            //    //}
//            //}
    
//            List<DeserializationResult> result = new List<DeserializationResult>();
//            foreach (var item in graphDeserializers) {
//                result.Add(new DeserializationResult(item.GetGraph(), item.serializedGraph.chunkPos, item.serializedGraph.minY, item.serializedGraph.maxY));
//            }
    
//            return result;
//        }
//    }

//    public struct DeserializationResult {
//        public XZPosInt chunkPosition;
//        public int chunkMinY, chunkMaxY;
//        public Graph graph;

//        public DeserializationResult(Graph graph, XZPosInt chunkPosition, int minY, int maxY) {
//            this.graph = graph;
//            this.chunkPosition = chunkPosition;
//            this.chunkMinY = minY;
//            this.chunkMaxY = maxY;
//        }
//    }

//    public class GraphSerializer {
//        //public Dictionary<CellContentData, int> edges = new Dictionary<CellContentData, int>();
//        //public Dictionary<NodeAbstract, int> nodes = new Dictionary<NodeAbstract, int>();

//        NavmeshLayserSerializer ns;
//        Graph graph;

//        public GraphSerializer(NavmeshLayserSerializer ns, Graph graph) {
//            this.ns = ns;
//            this.graph = graph;
//        }

//        public SerializedGraph Serialize() {
//            SerializedGraph serializedGraph = new SerializedGraph();
//            serializedGraph.posX = graph.chunk.x;
//            serializedGraph.posZ = graph.chunk.z;
//            serializedGraph.minY = graph.chunk.min;
//            serializedGraph.maxY = graph.chunk.max;

//            List<SerializedCell> serializedCells = new List<SerializedCell>();
//            List<SerializedCover> serializedCovers = new List<SerializedCover>();

//            Cell[] cellsArray;
//            int cellsCount;
//            graph.GetCells(out cellsArray, out cellsCount);

//            for (int cellID = 0; cellID < cellsCount; cellID++) {
//                serializedCells.Add(new SerializedCell(ns, this, cellsArray[cellID]));
//            }
//            ns.cellCount += cellsCount;

//            foreach (var cover in graph.covers) {
//                serializedCovers.Add(new SerializedCover(ns, cover));
//            }

//            serializedGraph.serializedCells = serializedCells;
//            serializedGraph.serializedCovers = serializedCovers;

//            //battlegrid
//            //if (graph.samplePoints != null)
//            //    serializedGraph.samplePoints = new SerializedSamplePoints(ns, graph.samplePoints);
//            //else
//            //    serializedGraph.samplePoints = null;

//            //cell map
//            Cell[] cellRichMap, cellSimpleMap;
//            IndexLengthInt[] cellRichMapLayout, cellSimpleMapLayout;
//            int cellMapResolution;
//            bool cellSimpleMapHaveEmptySpots;

//            graph.GetCellMapToSerialization(out cellMapResolution, out cellRichMap, out cellRichMapLayout, out cellSimpleMap, out cellSimpleMapLayout, out cellSimpleMapHaveEmptySpots);
//            int resolutionSize = cellMapResolution * cellMapResolution;

//            IndexLengthInt lastRichLayout = cellRichMapLayout[resolutionSize - 1];
//            int[] serializedRichCellMap = new int[lastRichLayout.index + lastRichLayout.length];
//            int[] serializedRichCellMapIndexes = new int[resolutionSize];
//            int[] serializedRichCellMapLengths = new int[resolutionSize];
//            IndexLengthInt lastSimpleLayout = cellSimpleMapLayout[resolutionSize - 1];
//            int[] serializedSimpleCellMap = new int[lastRichLayout.index + lastRichLayout.length];
//            int[] serializedSimpleCellMapIndexes = new int[resolutionSize];
//            int[] serializedSimpleCellMapLengths = new int[resolutionSize];

//            for (int i = 0; i < resolutionSize; i++) {
//                IndexLengthInt il;
//                il = cellRichMapLayout[i];
//                serializedRichCellMapIndexes[i] = il.index;
//                serializedRichCellMapLengths[i] = il.length;

//                il = cellSimpleMapLayout[i];
//                serializedSimpleCellMapIndexes[i] = il.index;
//                serializedSimpleCellMapLengths[i] = il.length;
//            }

//            for (int i = 0; i < lastRichLayout.index + lastRichLayout.length; i++) {
//                serializedRichCellMap[i] = cellRichMap[i].ID;
//            }
            
//            for (int i = 0; i < lastSimpleLayout.index + lastSimpleLayout.length; i++) {
//                serializedSimpleCellMap[i] = cellSimpleMap[i].ID;
//            }

//            serializedGraph.serializedCellMapResolution = cellMapResolution;
//            serializedGraph.serializedRichCellMap = serializedRichCellMap;
//            serializedGraph.serializedRichCellMapLayoutIndexes = serializedRichCellMapIndexes;
//            serializedGraph.serializedRichCellMapLayoutLengths = serializedRichCellMapLengths;

//            serializedGraph.serializedSimpleCellMap = serializedSimpleCellMap;
//            serializedGraph.serializedSimpleCellMapLayoutIndexes = serializedSimpleCellMapIndexes;
//            serializedGraph.serializedSimpleCellMapLayoutLengths = serializedSimpleCellMapLengths;
//            serializedGraph.serializedSimpleMapHaveEmptySpots = cellSimpleMapHaveEmptySpots;

//            //contour and border
//            var borderData = graph.borderDataNew;
//            //enum Direction for target index/direction
//            //0-3 = border edges that graph was created with
//            //4-7 = border edges that appear when graph connected to other graphs
//            //8 = coutour of graph that made out of initial edges
//            CellContentConnectionData[] cccd;
//            IndexLengthInt[] cccdLayout;
//            borderData.GetOptimizedData(out cccd, out cccdLayout);

//            int totalBorderLength = cccdLayout[8].index + cccdLayout[8].length;
//            SerializedContourDataNew[] borderInfo = new SerializedContourDataNew[totalBorderLength];
//            IndexLengthInt[] borderInfoLayout = new IndexLengthInt[9];

//            for (int i = 0; i < totalBorderLength; i++) {
//                var val = cccd[i];
//                borderInfo[i] = new SerializedContourDataNew(val.data.leftV3, val.data.rightV3, val.from.ID, val.connection != null ? val.connection.ID : -1);
//            }

//            for (int i = 0; i < 9; i++) {
//                borderInfoLayout[i] = cccdLayout[i];
//            }

//            serializedGraph.borderInfo = borderInfo;
//            serializedGraph.borderInfoLayout = borderInfoLayout;
//            return serializedGraph;
//        }
//    }

//    public class GraphDeserializer {
//        public List<Cell> cells = new List<Cell>();
//        public List<Cover> covers = new List<Cover>();

//        public SerializedGraph serializedGraph;
//        private Graph targetGraph;
//        private NavmeshLayerDeserializer deserializer;

//        Cell[] cellPool; //shortcut to cell pool

//        public GraphDeserializer(SerializedGraph serializedGraph, NavmeshLayerDeserializer deserializer, AgentProperties properties) {
//            this.serializedGraph = serializedGraph;
//            this.deserializer = deserializer;
//            targetGraph = new Graph(serializedGraph.chunkData, properties);

//            //nodes = new Node[this.serializedGraph.serializedNodes.Count];
//            //edges = new EdgeGraph[this.serializedGraph.serializedEdges.Count];

//            cellPool = deserializer.cellPool;
//        }

//        //before all next
//        public void DeserializeCells() {
//            //cells
//            List<SerializedCell> serializedCells = serializedGraph.serializedCells;

//            foreach (var curSerializedCell in serializedCells) {
//                Area cellArea;
//                if (curSerializedCell.isAdvancedAreaCell) {
//                    GameObject targetGO = deserializer.GetGameObject(curSerializedCell.area);
//                    if(targetGO == null) {
//                        Debug.LogWarning("Deserializer cant find GameObject so Cell area became default area");
//                        cellArea = PathFinder.getDefaultArea;
//                    }
//                    else {
//                        AreaWorldMod areaWorldMod = targetGO.GetComponent<AreaWorldMod>();
//                        if(areaWorldMod == null) {
//                            Debug.LogWarning("Deserializer cant find AreaModifyer on gameObject so Cell area became default area");
//                            cellArea = PathFinder.getDefaultArea;
//                        }
//                        else {
//                            if(areaWorldMod.useAdvancedArea == false) {
//                                Debug.LogWarning("Area Modifyer don't use advanced area so Cell area became default area");
//                                cellArea = PathFinder.getDefaultArea;
//                            }
//                            else {
//                                cellArea = areaWorldMod.advancedArea;
//                            }
//                        }
//                    }
//                }
//                else {
//                    cellArea = deserializer.GetArea(curSerializedCell.area);
//                }
            

//                Cell newC = new Cell(cellArea, (Passability)curSerializedCell.passability, curSerializedCell.layer, targetGraph, curSerializedCell.originalEdges);
//                newC.SetCenter(curSerializedCell.center);

//                foreach (var data in curSerializedCell.data) {
//                    newC.TryAddData(data);
//                }

//                cellPool[curSerializedCell.id] = newC;
//                cells.Add(newC);


//                //Vector3 CC = c.center;
//                //foreach (var data in c.data) {
//                //    Vector3 DC = data.centerV3;
//                //    PFDebuger.Debuger_K.AddLine(CC, DC, Color.red);
//                //    Vector3 CCDC = SomeMath.MidPoint(CC, DC);
//                //    PFDebuger.Debuger_K.AddLine(CCDC, data.rightV3, Color.blue);
//                //    PFDebuger.Debuger_K.AddLine(CCDC, data.leftV3, Color.cyan);
//                //}           
//            }

//            //map
//            int serializedCellMapResolution = serializedGraph.serializedCellMapResolution;
//            int[] serializedRichCellMap = serializedGraph.serializedRichCellMap;
//            int[] serializedRichCellMapLayoutIndexes = serializedGraph.serializedRichCellMapLayoutIndexes;
//            int[] serializedRichCellMapLayoutLengths = serializedGraph.serializedRichCellMapLayoutLengths;

//            int[] serializedSimpleCellMap = serializedGraph.serializedSimpleCellMap;
//            int[] serializedSimpleCellMapLayoutIndexes = serializedGraph.serializedSimpleCellMapLayoutIndexes;
//            int[] serializedSimpleCellMapLayoutLengths = serializedGraph.serializedSimpleCellMapLayoutLengths;
//            bool serializedSimpleMapHaveEmptySpots = serializedGraph.serializedSimpleMapHaveEmptySpots;

//            Cell[] cellRichMap = Pool.GenericPoolArray<Cell>.Take(serializedRichCellMap.Length, true);
//            Cell[] cellSimpleMap = Pool.GenericPoolArray<Cell>.Take(serializedRichCellMap.Length, true);
//            IndexLengthInt[] cellRichMapLayout = Pool.GenericPoolArray<IndexLengthInt>.Take(serializedRichCellMap.Length, true);
//            IndexLengthInt[] cellSimpleMapLayout = Pool.GenericPoolArray<IndexLengthInt>.Take(serializedRichCellMap.Length, true);

//            for (int i = 0; i < serializedRichCellMap.Length; i++) {
//                cellRichMap[i] = cellPool[serializedRichCellMap[i]];
//            }
//            for (int i = 0; i < serializedSimpleCellMap.Length; i++) {
//                cellSimpleMap[i] = cellPool[serializedSimpleCellMap[i]];
//            }

//            int cmrSqr = serializedCellMapResolution * serializedCellMapResolution;

//            for (int i = 0; i < cmrSqr; i++) {
//                cellRichMapLayout[i] = new IndexLengthInt(serializedRichCellMapLayoutIndexes[i], serializedRichCellMapLayoutLengths[i]);
//            }
//            for (int i = 0; i < cmrSqr; i++) {
//                cellSimpleMapLayout[i] = new IndexLengthInt(serializedSimpleCellMapLayoutIndexes[i], serializedSimpleCellMapLayoutLengths[i]);
//            }

//            targetGraph.SetCellMapFromSerialization(
//                serializedCellMapResolution, 
//                cellRichMap, cellRichMapLayout,
//                cellSimpleMap, cellSimpleMapLayout,
//                serializedSimpleMapHaveEmptySpots);

//            //contour       
//            SerializedContourDataNew[] borderInfo = serializedGraph.borderInfo;
//            IndexLengthInt[] borderInfoLayout = serializedGraph.borderInfoLayout;

//            CellContentConnectionData[] borderInfoDeserialized = Pool.GenericPoolArray<CellContentConnectionData>.Take(borderInfo.Length, true);
//            for (int i = 0; i < borderInfo.Length; i++) {
//                SerializedContourDataNew scd = borderInfo[i];
//                borderInfoDeserialized[i] = new CellContentConnectionData(cellPool[scd.from], scd.connection == -1 ? null : cellPool[scd.connection], scd.a, scd.b);
//            }
            
//            var graphBorderData = targetGraph.borderDataNew;
//            graphBorderData.SetOptimizedData(borderInfoDeserialized, borderInfoLayout, 9);

//            //var serializedContour = serializedGraph.contour;
//            //Dictionary<CellContentData, Cell> contour = new Dictionary<CellContentData, Cell>();
//            //foreach (var c in serializedContour) {
//            //    contour.Add(new CellContentData(c.a, c.b), cellPool[c.cell]);
//            //}
//            //targetGraph.SetBunchOfData(cells, contour);
//            ////borders  
//            //var serializedBorderData = serializedGraph.borderData;    
//            //if (serializedBorderData != null) {
//            //    foreach (var bd in serializedBorderData) {
//            //        targetGraph.SetBorderEdge((Directions)bd.direction, true, new CellContentData(bd.a, bd.b), cellPool[bd.cell], null);
//            //    }
//            //}
//            //Debug.Log("dont forget to add serialization for newly generated borders");
//            //debug
//            //Debug.Log(cells.Count);
//            //string s = "";
//            //for (int x = 0; x < cellMap.Length; x++) {
//            //    for (int z = 0; z < cellMap[x].Length; z++) {
//            //        s += cellMap[x][z].Count;
//            //    }
//            //    s += "\n";
//            //}
//            //Debug.Log(s);
//            //Debug.Log(contour.Count);
//        }

//        //after deserializing cells
//        public void DeserializeConnections(bool deserializeJumpConnections) {
//            foreach (var cell in serializedGraph.serializedCells) {
//                Cell fromCell = cellPool[cell.id];

//                foreach (var connection in cell.serializedNormalConnections) {
//                    CellContentGenericConnection newCon = CellContentGenericConnection.GetFromPool(
//                        connection.data,
//                        fromCell,
//                        cellPool[connection.connectedCell],                        
//                        connection.interconnection,         
//                        connection.costFrom,
//                        connection.costTo,
//                        connection.intersection);

//                    fromCell.SetContent(newCon);
//                }

//                if (deserializeJumpConnections) {
//                    foreach (var connection in cell.serializedJumpConnections) {
//                        CellContentPointedConnection newCon = CellContentPointedConnection.GetFromPool(
//                            connection.enterPoint,
//                            connection.lowerStandingPoint,
//                            connection.exitPoint,
//                            connection.axis,
//                            (ConnectionJumpState)connection.jumpState,
//                            fromCell,
//                            cellPool[connection.connectedCell],           
//                            connection.interconnection);

//                        fromCell.SetContent(newCon);
//                    }
//                }
//            }
//        }
//        public void DeserializeCovers() {
//            foreach (var cover in serializedGraph.serializedCovers) {
//                Cover unserializedCover = new Cover(cover.left, cover.right, cover.coverType, cover.normal);
//                foreach (var point in cover.coverPoints) {
//                    NodeCoverPoint unserializedCoverPoint = new NodeCoverPoint(point.position, point.cellPosition, cellPool[point.cell], unserializedCover);
//                    unserializedCover.AddCoverPoint(unserializedCoverPoint);
//                }
//                targetGraph.covers.Add(unserializedCover);
//                covers.Add(unserializedCover);
//            }
//        }

//        //battle grid
//        //public void DeserializeBattleGridPoints() {
//        //    List<CellSamplePoint_Internal> samplePoints = new List<CellSamplePoint_Internal>();

//        //    foreach (var item in serializedGraph.samplePoints.serializedPoints) {
//        //        samplePoints.Add(new CellSamplePoint_Internal(item.x, item.y, item.z, item.gridX, item.gridZ, cellPool[item.cellID]));
//        //    }

//        //    targetGraph.samplePoints = samplePoints;
//        //}
//        //public void ConnectBattleGridPoints() {
//        //    BattleGridPoint[] pool = deserializer.bgPool;
//        //    foreach (var p in serializedGraph.battleGrid.points) {
//        //        BattleGridPoint curP = pool[p.id];

//        //        for (int i = 0; i < 4; i++) {
//        //            if (p.neighbours[i] != -1) {
//        //                curP.neighbours[i] = pool[p.neighbours[i]];
//        //            }
//        //        }
//        //    }
//        //}  

//        public Graph GetGraph() {
//            return targetGraph;
//        }
//    }
        
//    [Serializable]
//    public class SerializedNavmesh {
//        public string pathFinderVersion;
//        public int cellCount, bgPointsCount;
//        public List<SerializedGraph> serializedGraphs;
//    }

//    [Serializable]
//    public class SerializedGraph {
//        public int posX, posZ, minY, maxY;
//        public List<SerializedCell> serializedCells;
//        [SerializeField]
//        public List<SerializedCover> serializedCovers;
//        //[SerializeField]
//        //public SerializedSamplePoints samplePoints;



//        //[SerializeField]public IndexLengthInt[] serializedCellMapLayout; //For some bizzare reason it refuses serialize. i wonder why

//        [SerializeField] public int serializedCellMapResolution;
//        [SerializeField] public int[] serializedRichCellMap;
//        [SerializeField] public int[] serializedRichCellMapLayoutIndexes;
//        [SerializeField] public int[] serializedRichCellMapLayoutLengths;

//        [SerializeField] public int[] serializedSimpleCellMap;
//        [SerializeField] public int[] serializedSimpleCellMapLayoutIndexes;
//        [SerializeField] public int[] serializedSimpleCellMapLayoutLengths;
//        [SerializeField] public bool serializedSimpleMapHaveEmptySpots;








//        Cell[] cellRichMap, cellSimpleMap;
//        IndexLengthInt[] cellRichMapLayout, cellSimpleMapLayout;
//        int cellMapResolution;
//        bool cellSimpleMapHaveEmptySpots;

//        [SerializeField] public SerializedContourDataNew[] borderInfo;
//        [SerializeField] public IndexLengthInt[] borderInfoLayout;

//        //[SerializeField]
//        //public List<SerializedContourData> contour;
//        //[SerializeField]
//        //public List<SerializedBorderData> borderData;

//        public XZPosInt chunkPos {
//            get { return new XZPosInt(posX, posZ); }
//        }

//        public ChunkData chunkData {
//            get { return new ChunkData(posX, posZ, minY, maxY); }
//        }
//    }

//    [Serializable]
//    public class SerializedCell {
//        public int id, layer, area, passability;
//        public bool isAdvancedAreaCell;
//        public Vector3 center;
//        public List<CellContentData> data = new List<CellContentData>();
//        public List<CellContentData> originalEdges = new List<CellContentData>();
//        public List<SerializedNormalConnection> serializedNormalConnections = new List<SerializedNormalConnection>();
//        public List<SerializedJumpConnection> serializedJumpConnections = new List<SerializedJumpConnection>();
        
//        public SerializedCell(NavmeshLayserSerializer ns, GraphSerializer gs, Cell cell) {
//            id = cell.ID;
//            layer = cell.layer;

//            isAdvancedAreaCell = cell.advancedAreaCell;
//            if (cell.advancedAreaCell) {
//                AreaAdvanced aa = cell.area as AreaAdvanced;
//                area = ns.GetGameObjectID(aa.container.gameObject);
//            }
//            else {
//                area = cell.area.id;
//            }
   
//            passability = (int)cell.passability;
//            center = cell.centerVector3;            

//            data = new List<CellContentData>(cell.data);
//            originalEdges = new List<CellContentData>(cell.originalEdges);

//            foreach (var connection in cell.connections) {
//                if (connection is CellContentGenericConnection)
//                    serializedNormalConnections.Add(new SerializedNormalConnection(ns, gs, connection as CellContentGenericConnection));

//                if (connection is CellContentPointedConnection)
//                    serializedJumpConnections.Add(new SerializedJumpConnection(ns, connection as CellContentPointedConnection));
//            }
//        }


//        public override string ToString() {
//            StringBuilder sb = new StringBuilder();
//            sb.AppendFormat("ID: {0}, Layer: {1}, Area: {2}, Passability: {3}\n", id, layer, area, passability);
//            sb.AppendFormat("Center: {0}\n", center);
//            sb.AppendFormat("Data ({0})\n:", data.Count);
//            foreach (var d in data) {
//                sb.AppendLine(d.ToString());
//            }
//            sb.AppendFormat("Normal Connections ({0})\n:", serializedNormalConnections.Count);
//            foreach (var c in serializedNormalConnections) {
//                sb.AppendLine(c.ToString());
//            }
//            sb.AppendFormat("Jump Connections ({0})\n:", serializedJumpConnections.Count);
//            foreach (var c in serializedJumpConnections) {
//                sb.AppendLine(c.ToString());
//            }

//            return sb.ToString();
//        }
//    }

//    //[Serializable]
//    //public class SerializedNode {
//    //    public float x, y, z;

//    //    public SerializedNode(Node node) {
//    //        x = node.x;
//    //        y = node.y;
//    //        z = node.z;
//    //    }

//    //    public Node Deserialize() {
//    //        return new Node(x, y, z);
//    //    }

//    //    public Vector3 position {
//    //        get { return new Vector3(x, y, z); }
//    //    }
//    //}

//    //[Serializable]
//    //public class SerializedEdge {
//    //    public int nodeA, nodeB, rightCell, leftCell, direction;

//    //    public SerializedEdge(NavmeshLayserSerializer ns, GraphSerializer gs, EdgeGraph edge) {
//    //        nodeA = gs.nodes[edge.a];
//    //        nodeB = gs.nodes[edge.b];
//    //        rightCell = edge.right == null ? -1 : ns.GetCellID(edge.right);
//    //        leftCell = edge.left == null ? -1 : ns.GetCellID(edge.left);
//    //        direction = edge.direction;
//    //    }
//    //}

//    [Serializable]
//    public class SerializedNormalConnection {
//        public bool interconnection;
//        public int fromCell, connectedCell;
//        public float costFrom, costTo;
//        public Vector3 intersection;
//        public CellContentData data;

//        public SerializedNormalConnection(NavmeshLayserSerializer ns, GraphSerializer gs, CellContentGenericConnection connection) {
//            interconnection = connection.interconnection;
//            fromCell = connection.from.ID;
//            connectedCell = connection.connection.ID;
//            data = connection.cellData;
//            intersection = connection.intersection;
//            costFrom = connection.costFrom;
//            costTo = connection.costTo;
//        }
//    }

//    [Serializable]
//    public class SerializedJumpConnection {
//        public Vector3 enterPoint, lowerStandingPoint, exitPoint, axis;
//        public bool interconnection;
//        public int connectedCell, jumpState;  

//        public SerializedJumpConnection(NavmeshLayserSerializer ns, CellContentPointedConnection connection) {
//            interconnection = connection.interconnection;
//            connectedCell = connection.connection.ID;
//            enterPoint = connection.enterPoint;
//            lowerStandingPoint = connection.lowerStandingPoint;
//            exitPoint = connection.exitPoint;
//            axis = connection.axis;
//            jumpState = (int)connection.jumpState;
//        }
//    }
    
//    [Serializable]
//    public class SerializedCover {
//        public List<SerializedCoverPoint> coverPoints = new List<SerializedCoverPoint>();
//        public Vector3 left, right, normal;
//        public float leftX, leftY, leftZ, rightX, rightY, rightZ, normalX, normalY, normalZ;
//        public int coverType;

//        public SerializedCover(NavmeshLayserSerializer ns, Cover cover) {
//            coverType = cover.coverType;
//            left = cover.left;
//            right = cover.right;
//            normal = cover.normalV3;

//            foreach (var p in cover.coverPoints) {
//                coverPoints.Add(new SerializedCoverPoint(p, p.cell.ID));
//            }
//        }
//    }

//    [Serializable]
//    public class SerializedCoverPoint {
//        public Vector3 position, cellPosition;
//        public int cell;

//        public SerializedCoverPoint(NodeCoverPoint point, int cell) {
//            position = point.positionV3;
//            cellPosition = point.cellPos;
//            this.cell = cell;
//        }
//    }

//    [Serializable]
//    public struct SerializedSamplePoint {      
//        public float x, y, z;
//        public int gridX, gridZ, cellID;

//        public SerializedSamplePoint(NavmeshLayserSerializer ns, CellSamplePoint_Internal point) {
//            x = point.x;
//            y = point.y;
//            z = point.z;
//            gridX = point.gridX;
//            gridZ = point.gridZ;
//            cellID = point.cell.ID;
//        }
//    }
    
//    [Serializable]
    //public struct SerializedContourDataNew {    
    //    public Vector3 a, b;
    //    public int from, connection;

    //    public SerializedContourDataNew(Vector3 a, Vector3 b, int from, int connection) {
    //        this.a = a;
    //        this.b = b;
    //        this.from = from;
    //        this.connection = connection;
    //    }
    //}
//}