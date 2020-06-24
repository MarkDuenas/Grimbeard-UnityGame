using UnityEngine;
using System.Collections.Generic;
using System.Linq;

using K_PathFinder.EdgesNameSpace;
using K_PathFinder.NodesNameSpace;
using K_PathFinder.CoverNamespace;
using System;
using K_PathFinder.VectorInt;
using System.Collections.ObjectModel;
using System.Text;
//using K_PathFinder.Serialization;
using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using K_PathFinder.CoolTools;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif

namespace K_PathFinder.Graphs {
    //core class of graph
    public partial class Graph {
        //Notes:
        //to get cells use GetCells. It return array of cells and amount of cells in this array
        //to get jump spots use GetPortalBases. It return array of cells and amount of cells in this array
        public const int INVALID_VALUE = -1;
        public ChunkData chunk { get; private set; }
        public AgentProperties properties { get; private set; }
        public List<Cover> covers = new List<Cover>();

        //enum Direction for target index/direction
        //0-3 = border edges that graph was created with
        //4-7 = border edges that appear when graph connected to other graphs
        //8 = coutour of graph that made out of initial edges and from is global index
        //9 = coutour of graph that made out of initial edges and from is graph index
        public StackedList<CellContentConnectionData> borderData; //have 10 length
        public const int BORDER_DATA_LENGTH = 10;

        //all graph edges and connections
        int _cellsCount;
        Cell[] _cellsArray; //allgraph cells
        IndexLengthInt[] _graphLayout; //index = cell index
        CellContentData[] _graphEdges; //layout.index + (from 0 to layout.length) is target data range for cell
        CellConnection[] _graphConnections;//same thing but for connections. CAUTION: check connection type

        //portals base array
        JumpPortalBase[] _portalBasesArray;
        int _portalBasesArrayCount;

        //neighbours of graph
        Graph[] _neighbours = new Graph[4];

        public bool alive = false;
        
        public Graph(ChunkData chunk, AgentProperties properties) {
            borderData = StackedList<CellContentConnectionData>.PoolTake(BORDER_DATA_LENGTH, 64);
            this.chunk = chunk;
            this.properties = properties;
        }
        
        public void FunctionsToFinishGraphInPathfinderMainThread() {
            PathFinderData.AddCells(_cellsArray, _cellsCount);

            //setting neighbours
            for (int i = 0; i < 4; i++) {
                Graph neighbour;
                if (PathFinder.TryGetGraphFrom(gridPosition, (Directions)i, properties, out neighbour)) {
                    SetNeighbour((Directions)i, neighbour);
                }
            }
            //ConnectBattleGrid(properties.samplePointsDencity * (PathFinder.gridSize / properties.voxelsPerChunk) * 1.732051f);

            CheckCovers();
            RefreshCellMap();

            if (_cellsCount != 0)
                MakeBorders(properties.maxStepHeight);

            alive = true;

            lock (properties)
                properties.internal_flagList = 1;
        }


        public void FunctionsToFinishGraphInUnityThread() {
            CheckJumpConnections();
            CheckCellsForAdvancedAreas();
        }

        //this function will be callse when graph are finished in unity main thread
        public void OnFinishGraph() {     
            for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                _cellsArray[cellIndex].OnGraphGenerationEnd();
            }   
        }


        //this function will disconnect graph and remove it's data from here and there
        //is is complete wipe then it skip restoring borders (cause everything is destroyed and border information not actualy need to be propper)
        public void OnDestroyGraph(bool isCompleteWipe) {
            alive = false;
            //connections
            //since right now connections are only made by Cell this part is easy. just take all connections and disconnect wich have interconnection flag
            //HashSet<CellContent> interconnections = new HashSet<CellContent>();
            for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                Cell targetCell = _cellsArray[cellIndex];
                targetCell.OnGraphDestruction();
                PathFinderData.ReturnFreeCellID(targetCell.globalID);
            }

            //restore neighbour state for target side
            if (isCompleteWipe == false) {
                for (int i = 0; i < 4; i++) {
                    Directions dir = (Directions)i;
                    Directions opposite = Enums.Opposite(dir);
                    if (_neighbours[i] != null) {
                        _neighbours[i].RestoreEdges(opposite);
                        _neighbours[i]._neighbours[(int)opposite] = null;
                        _neighbours[i] = null;
                    }
                }
            }

            PathFinderData.RemoveCells(_cellsArray, _cellsCount);
            
            GenericPoolArray<Cell>.ReturnToPool(ref _cellsArray);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref _graphLayout);
            GenericPoolArray<CellContentData>.ReturnToPool(ref _graphEdges);
            GenericPoolArray<CellConnection>.ReturnToPool(ref _graphConnections);

            if (_portalBasesArray != null)
                GenericPoolArray<JumpPortalBase>.ReturnToPool(ref _portalBasesArray); //return portals array to pool

            ClearRichCellMap();

            if (borderData != null) {
                StackedList<CellContentConnectionData>.PoolReturn(ref borderData);       
            }

#if UNITY_EDITOR
            Debuger_K.ClearChunksDebug(new GeneralXZData(chunk.x, chunk.z, properties));
#endif
        }


        void RestoreEdges(Directions direction) {
            if (_cellsCount == 0) //no possible edges
                return;

            //Debug.LogFormat("restore edges at {0} : 1", chunk);
            //Debuger_K.AddLabel(chunk.centerV3, chunk);
            List<CellContentConnectionData> cccdList = GenericPool<List<CellContentConnectionData>>.Take();            

            borderData.Read((int)direction + 4, cccdList);//reading current state from border data
            borderData.Clear((int)direction + 4);         //removing old state

            StackedListWithKeys<CellContentData, CellConnection> stackedData = StackedListWithKeys<CellContentData, CellConnection>.PoolTake(1, 1);
            stackedData.SetOptimizedData(_graphEdges, _graphConnections, _graphLayout, _cellsCount);

            GenericPoolArray<CellContentData>.ReturnToPool(ref _graphEdges);
            GenericPoolArray<CellConnection>.ReturnToPool(ref _graphConnections);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref _graphLayout);
            //*************** done with preparations ***************//

            Cell[] globalCells = PathFinderData.cells;


            //removing old connections from cell
            for (int i = 0; i < cccdList.Count; i++) {
                CellContentConnectionData cccd = cccdList[i];
                Cell curFrom = globalCells[cccd.from];

                CellConnection value;
                if(stackedData.RemoveKey(curFrom.graphID, cccd.data, out value) == false) 
                    Debug.LogErrorFormat("could not find target edge for {0} cell in current graph state", curFrom.graphID);                
                else if (value.type != CellConnectionType.Invalid)
                    curFrom.RemoveConnection(value);                              
            }

            borderData.Read((int)direction, cccdList, true);//taking original edges from border data

            for (int i = 0; i < cccdList.Count; i++) {
                CellContentConnectionData cccd = cccdList[i];
                stackedData.AddKeyValue(globalCells[cccd.from].graphID, cccd.data, CellConnection.invalid);          
            }

            cccdList.Clear();
            GenericPool<List<CellContentConnectionData>>.ReturnToPool(ref cccdList);

            stackedData.GetOptimizedData(out _graphEdges, out _graphConnections, out _graphLayout);
            StackedListWithKeys<CellContentData, CellConnection>.PoolReturn(ref stackedData);
        }

        void SetBorderGeneratedEdges(Directions direction, List<CellContentConnectionData> newBorderData) {  
            if (_cellsCount == 0 | newBorderData.Count == 0) return; //there is nothig to change anyway

            List<CellContentConnectionData> currentBorderData = GenericPool<List<CellContentConnectionData>>.Take();
            borderData.Read((int)direction, currentBorderData);//reading current edges

            StackedListWithKeys<CellContentData, CellConnection> stackedData = StackedListWithKeys<CellContentData, CellConnection>.PoolTake(1, 1);
            stackedData.SetOptimizedData(_graphEdges, _graphConnections, _graphLayout, _cellsCount);
            GenericPoolArray<CellContentData>.ReturnToPool(ref _graphEdges);
            GenericPoolArray<CellConnection>.ReturnToPool(ref _graphConnections);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref _graphLayout);

            //*************** done with preparations ***************//

            Cell[] globalCells = PathFinderData.cells;

            foreach (var cccd in currentBorderData) {
                if (stackedData.RemoveKey(globalCells[cccd.from].graphID, cccd.data) == false) {
                    Debug.LogErrorFormat("failed to remove edge while setting new border. graph {0} cell {1}, edge {2}\ncurrent temporary dictionary content\n\n{3}", gridPosition, globalCells[cccd.from].graphID, cccd.data, stackedData);
                }
            }

            //adding new edges to border and cells
            foreach (var cccd in newBorderData) {
                borderData.AddLast((int)direction + 4, cccd);

                if(cccd.connection == -1) {
                    stackedData.AddKeyValue(globalCells[cccd.from].graphID, cccd.data, CellConnection.invalid);
                }
                else {
                    Cell from = globalCells[cccd.from];
                    Cell connection = globalCells[cccd.connection];
                    var data = cccd.data;

                    Vector3 intersection;
                    SomeMath.ClampedRayIntersectXZ(from.centerVector3, connection.centerVector3 - from.centerVector3, data.leftV3, data.rightV3, out intersection);
                    float cell1Cost = Vector3.Distance(from.centerVector3, intersection) * from.area.cost;  
                    float cell2Cost = Vector3.Distance(connection.centerVector3, intersection) * connection.area.cost;

                    CellConnection newConnection = from.AddConnectionGeneric(data, connection, cell1Cost, cell2Cost, intersection);
                    stackedData.AddKeyValue(from.graphID, cccd.data, newConnection);                    

#if UNITY_EDITOR
                    if (Debuger_K.doDebug) {
                        Debuger_K.AddEdgesInterconnected(x, z, properties, newConnection);       
                    }
#endif
                }
            }

            currentBorderData.Clear();
            GenericPool<List<CellContentConnectionData>>.ReturnToPool(ref currentBorderData);

            stackedData.GetOptimizedData(out _graphEdges, out _graphConnections, out _graphLayout);
            StackedListWithKeys<CellContentData, CellConnection>.PoolReturn(ref stackedData);

            ResetRaycastData();
        }

        public void ResetRaycastData() {
            for (int c = 0; c < _cellsCount; c++) {
                Cell cell = _cellsArray[c];

                if (cell.raycastData != null) {
                    for (int i = 0; i < cell.raycastDataCount; i++) {
                        cell.raycastData[i].connection = -1;
                    }
                    GenericPoolArray<CellContentRaycastData>.ReturnToPool(ref cell.raycastData);
                }

                IndexLengthInt layout = _graphLayout[c];

                var raycastData = GenericPoolArray<CellContentRaycastData>.Take(layout.length);
                int raycastDataCount = 0;

                for (int i = 0; i < layout.length; i++) {
                    var data = _graphEdges[layout.index + i];
                    var value = _graphConnections[layout.index + i];

                    if (value.type == CellConnectionType.Invalid)
                        raycastData[raycastDataCount++] = new CellContentRaycastData(data, -1);
                    else if (value.type == CellConnectionType.Generic) {
                        raycastData[raycastDataCount++] = new CellContentRaycastData(data, value.connection);
                    }
                }

                cell.raycastData = raycastData;
                cell.raycastDataCount = raycastDataCount;
            }
        }

        public void ResetRaycastData(int cellID) {
            if (!alive)
                return;

            Cell cell = _cellsArray[cellID];

            if (cell.raycastData != null) {
                for (int i = 0; i < cell.raycastDataCount; i++) {
                    cell.raycastData[i].connection = -1;
                }
                GenericPoolArray<CellContentRaycastData>.ReturnToPool(ref cell.raycastData);
            }

            IndexLengthInt layout = _graphLayout[cellID];

            var raycastData = GenericPoolArray<CellContentRaycastData>.Take(layout.length);
            int raycastDataCount = 0;

            for (int i = 0; i < layout.length; i++) {
                var data = _graphEdges[layout.index + i];
                var value = _graphConnections[layout.index + i];

                if (value.type == CellConnectionType.Invalid)
                    raycastData[raycastDataCount++] = new CellContentRaycastData(data, -1);
                else if (value.type == CellConnectionType.Generic) {
                    raycastData[raycastDataCount++] = new CellContentRaycastData(data, value.connection);
                }
            }

            cell.raycastData = raycastData;
            cell.raycastDataCount = raycastDataCount;
        }
        
        /// <summary>
        /// Function to set whole bunchonimportant data to graph
        /// !!!this function copy target data. you should dispose references on your own!!! 
        /// </summary>
        /// <param name="passedCells">array of cells that graph contain</param>
        /// <param name="passedCellsCount">amount of cells in cell array</param>
        /// <param name="edges">edges of graph</param>
        /// <param name="connections">connection on edges</param>
        /// <param name="dataLayout">layout of edges and connections. index in that array is cell index</param>
        public void SetGraphData(Cell[] passedCells, int passedCellsCount, CellContentData[] edges, CellConnection[] connections, IndexLengthInt[] dataLayout) {
            _cellsCount = passedCellsCount;
            _cellsArray = GenericPoolArray<Cell>.Take(_cellsCount, makeDefault: true);
            _graphLayout = GenericPoolArray<IndexLengthInt>.Take(passedCellsCount);
            _graphEdges = GenericPoolArray<CellContentData>.Take(edges.Length);
            _graphConnections = GenericPoolArray<CellConnection>.Take(connections.Length);    

            for (int cellIndex = 0; cellIndex < passedCellsCount; cellIndex++) {
                Cell cell = passedCells[cellIndex];
                _cellsArray[cell.graphID] = cell;

                IndexLengthInt curLayout = dataLayout[cellIndex];
                _graphLayout[cellIndex] = curLayout;

                for (int i = 0; i < curLayout.length; i++) {
                    int index = curLayout.index + i;
                    var data = edges[index];
                    var value = connections[index];

                    _graphEdges[index] = data;
                    _graphConnections[index] = value;

                    if (value.type == CellConnectionType.Invalid) {
                        borderData.AddLast(8, new CellContentConnectionData(cell.globalID, -1, data));
                        borderData.AddLast(9, new CellContentConnectionData(cell.graphID, -1, data));
                    }
                }
            }

            ResetRaycastData();
        }
        
        public void SetChunkData(ChunkData chunk) {
            this.chunk = chunk;
        }

        public Graph GetNeighbour(Directions direction) {
            return _neighbours[(int)direction];
        }
        public bool TryGetNeighbour(Directions direction, out Graph graph) {
            graph = _neighbours[(int)direction];
            return graph != null;
        }

        public void SetNeighbour(Directions direction, Graph graph) {
            _neighbours[(int)direction] = graph;
            graph._neighbours[(int)Enums.Opposite(direction)] = this;
        }
        

        
        #region functions that create graph
        public void InitPortalsArray(int size) {
            _portalBasesArray = GenericPoolArray<JumpPortalBase>.Take(size);
            _portalBasesArrayCount = 0;
        }

        //takes edges and axis. check if edge exist, if exist add closest point to cell
        public void AddPortal(VolumeArea point) {
            Vector3 positionV3 = point.position;
            List<CellContentData> pointCellContentData = point.cellContentDatas;

            Dictionary<Cell, Vector3> cellMountPoints = new Dictionary<Cell, Vector3>();

            foreach (var data in pointCellContentData) {
                Vector3 intersection = data.NearestPointXZ(positionV3.x, positionV3.z);

                for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                    Cell cell = _cellsArray[cellIndex]; //check if cell contain this edge to add portal to it

                    CellContentData[] originalEdges = cell.originalEdges;
                    int originalEdgesCount = cell.originalEdgesCount;

                    bool containsData = false;
                    for (int i = 0; i < originalEdgesCount; i++) {
                        if(data == originalEdges[i]) {
                            containsData = true;
                            break;
                        }
                    }
                    if (containsData) {
                        if (cellMountPoints.ContainsKey(cell)) {
                            if (SomeMath.SqrDistance(cellMountPoints[cell], positionV3) > SomeMath.SqrDistance(intersection, positionV3))
                                cellMountPoints[cell] = intersection;
                        }
                        else
                            cellMountPoints.Add(cell, intersection);
                    }
                }
            }

            Vector2 normalRaw;

            switch (cellMountPoints.Count) {
                case 0:
                    return;
                case 1:
                    normalRaw = ToV2((cellMountPoints.First().Value - positionV3)).normalized * -1;
                    break;

                case 2:
                    normalRaw = (
                            ToV2(cellMountPoints.First().Value - positionV3).normalized +
                            ToV2(cellMountPoints.Last().Value - positionV3).normalized).normalized * -1;
                    break;
                default:
                    normalRaw = Vector2.left;
                    Dictionary<Cell, float> cellAngles = new Dictionary<Cell, float>();
                    Cell first = cellMountPoints.First().Key;
                    cellAngles.Add(first, 0f);

                    Vector3 firstDirV3 = cellMountPoints.First().Value - positionV3;
                    Vector2 firstDirV2 = ToV2(firstDirV3);

                    foreach (var pair in cellMountPoints) {
                        if (pair.Key == first)
                            continue;

                        Vector2 curDir = new Vector2(pair.Value.x - positionV3.x, pair.Value.z - positionV3.z);
                        cellAngles.Add(pair.Key, Vector2.Angle(firstDirV2, curDir) * Mathf.Sign(SomeMath.V2Cross(firstDirV2, curDir)));
                    }

                    normalRaw = (
                        ToV2(cellMountPoints[cellAngles.Aggregate((l, r) => l.Value > r.Value ? l : r).Key] - positionV3).normalized +
                        ToV2(cellMountPoints[cellAngles.Aggregate((l, r) => l.Value < r.Value ? l : r).Key] - positionV3).normalized).normalized * -1;
                    break;
            }

            _portalBasesArray[_portalBasesArrayCount++] = new JumpPortalBase(cellMountPoints, positionV3, new Vector3(normalRaw.x, 0, normalRaw.y));
        }

        public void AddCover(Vector3 coverLeft, Vector3 coverRight, int coverSize, Vector3 normal, VolumeArea[] coverPoints, int coversStart, int coversCount) {
            Cover cover = new Cover(coverLeft, coverRight, coverSize, normal);

            Vector3 pointNormalOffset = normal * properties.radius;

            for (int i = 0; i < coversCount; i++) {
                VolumeArea coverPoint = coverPoints[coversStart + i];
                Vector3 pointPlusOffset = coverPoint.position + pointNormalOffset;
                HashSet<Cell> nearbyCells = new HashSet<Cell>();

                foreach (var edge in coverPoint.cellContentDatas) {
                    for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                        Cell cell = _cellsArray[cellIndex]; //check if cell contain this edge to add cover to it
                        var c_oo = cell.originalEdges;
                        int c_ooCount = cell.originalEdgesCount;
                        for (int e = 0; e < c_ooCount; e++) {                 
                            if (c_oo[e] == edge) nearbyCells.Add(cell);
                        }
                            
                    }
                }

                float closestSqrDistance = float.MaxValue;
                Cell closestCell = null;
                Vector3 closestPoint = Vector3.zero;

                foreach (var cell in nearbyCells) {
                    bool isOutside;
                    Vector3 currentPoint;
                    cell.GetClosestPointToCell(pointPlusOffset.x, pointPlusOffset.z, out currentPoint, out isOutside);

                    float curSqrDistance = SomeMath.SqrDistance(pointPlusOffset, currentPoint);
                    if (curSqrDistance < closestSqrDistance) {
                        closestCell = cell;
                        closestPoint = currentPoint;
                        closestSqrDistance = curSqrDistance;
                    }
                }

                if (closestCell == null) {
                    //Debuger3.AddDot(coverPoint.positionV3, Color.red);
                    continue;
                }

                NodeCoverPoint coverNode = new NodeCoverPoint(coverPoint.position, closestPoint, closestCell, cover);
                cover.AddCoverPoint(coverNode);
            }

            covers.Add(cover);
        }
        #endregion
        
        public void GetCells(out Cell[] cellsArray, out int cellsCount) {
            cellsArray = _cellsArray;
            cellsCount = _cellsCount;
        }    
        
        public void GetTotalMap(out CellContentData[] mapData, out CellConnection[] mapValue, out IndexLengthInt[] layout) {
            mapData = _graphEdges;
            mapValue = _graphConnections;
            layout = _graphLayout;
        }

        public void GetPortalBases(out JumpPortalBase[] portalBasesArray, out int portalBasesArrayCount) {
            portalBasesArray = _portalBasesArray;
            portalBasesArrayCount = _portalBasesArrayCount;
        }

        #region acessors
        public int x {
            get { return chunk.x; }
        }
        public int z {
            get { return chunk.z; }
        }
        public VectorInt.Vector2Int positionChunk {
            get { return chunk.position; }
        }

        public Vector3 positionCenter {
            get { return new Vector3(chunk.realX + (PathFinder.gridSize * 0.5f), 0, chunk.realZ + (PathFinder.gridSize * 0.5f)); }
        }

        public XZPosInt gridPosition {
            get { return chunk.xzPos; }
        }

        public bool empty {
            get { return _cellsCount == 0; }
        }
        #endregion        

        #region borders
        /// <summary>
        /// add edge to border information
        /// if original edge == true: then this is edge that graph was generated with. it does not contain information about neighbours and not simplifyed or changed after graph generated
        /// if false then it contain all information but will be wiped out when neighbour disconected
        /// if Cell from is null then search will be performed anyway (cause this information kinda lost anyway along generation but easy to find)
        /// if Cell connection is null then it considered to be empty edge. original edges expected to be all empty
        /// </summary>
        public void SetBorderEdge(Directions direction, CellContentData edge, Cell from = null, Cell connection = null) {
            if (from == null) {
                for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                    Cell cell = _cellsArray[cellIndex];
                    var originalEdges = cell.originalEdges;      

                    for (int i = 0; i < cell.originalEdgesCount; i++) {
                        if(originalEdges[i] == edge) {
                            from = cell;
                            break;
                        }
                    }
                }
                if (from == null)
                   Debug.LogErrorFormat("PathFinder: Somehow target edge was not on any Cell in graph. Graph at {0} probably very broken", chunk.position.ToString());
            }

            borderData.AddLast((int)direction, new CellContentConnectionData(from.globalID, connection == null ? -1 : connection.globalID, edge));            
        }

        public void GetCountour(List<CellContentConnectionData> list, bool clearList = false) {
            borderData.Read(8, list, clearList);
        }

        private void MakeBorders(float yError) {
            List<CellContentConnectionData> edgesFrom = new List<CellContentConnectionData>();
            List<CellContentConnectionData> edgesTo = new List<CellContentConnectionData>();

            List<CellContentConnectionData> generatedBorderFrom = new List<CellContentConnectionData>();
            List<CellContentConnectionData> generatedBorderTo = new List<CellContentConnectionData>();


            for (int i = 0; i < 4; i++) {
                Directions directionFrom = (Directions)i;
                Directions directionTo = Enums.Opposite(directionFrom);

                Graph neighbourGraph;
                if (TryGetNeighbour(directionFrom, out neighbourGraph) == false)
                    continue;

                borderData.Read((int)directionFrom, edgesFrom);
                neighbourGraph.borderData.Read((int)directionTo, edgesTo);

                if (edgesFrom.Count > 0 & edgesTo.Count > 0) {
                    //List<TempEdge> tempEdges = new List<TempEdge>();

                    Axis projectionAxis;
                    if (directionFrom == Directions.xMinus | directionFrom == Directions.xPlus)
                        projectionAxis = Axis.z;
                    else
                        projectionAxis = Axis.x;

                    generatedBorderFrom.Clear();
                    generatedBorderTo.Clear();
                    GenerateBorderEdges(edgesFrom, edgesTo, projectionAxis, yError, generatedBorderFrom, generatedBorderTo);

                    //foreach (var item in generatedBorderFrom) {
                    //    var data = item.data;
                    //    Vector3 a = data.a;
                    //    Vector3 b = data.b + (Vector3.up * 0.2f);
                    //    Vector3 mid = SomeMath.MidPoint(a, b);

                    //    Debuger_K.AddLine(a, b, Color.blue);
                    //    Debuger_K.AddLine(item.from.centerVector3, mid, Color.cyan);

                    //    if(item.connection != null) {
                    //        Debuger_K.AddLine(item.connection.centerVector3, SomeMath.MidPoint(item.from.centerVector3, mid), Color.magenta);
                    //    }
                    //}

                    //add to border map. remove from cell old edge. add new edge
                    this.SetBorderGeneratedEdges(directionFrom, generatedBorderFrom);
                    neighbourGraph.SetBorderGeneratedEdges(directionTo, generatedBorderTo);
                }
            }
        }
        #endregion

             




        
        private void CheckJumpConnections() {
            if (_portalBasesArrayCount == 0)
                return;

            float rad = properties.radius;
            float radSqr = rad * rad;

            LayerMask mask = properties.includedLayers;

            float jumpUpSqr = properties.JumpUp * properties.JumpUp;
            float jumpDownSqr = properties.JumpDown * properties.JumpDown;
            float maxCheckDistance = Math.Max(jumpUpSqr, jumpDownSqr);

            float sampleStep = PathFinder.gridSize / properties.voxelsPerChunk;
            int sampleSteps = Mathf.RoundToInt(properties.radius / sampleStep) + 2;//plus some extra
            float bottomOffset = 0.2f;

            float agentHeightAjusted = properties.height - rad;
            float agentBottomAjusted = properties.radius + bottomOffset;
            RaycastHit hitCapsule, hitRaycast;

            if (agentHeightAjusted - agentBottomAjusted < 0) // somehow became spherical
                agentHeightAjusted = agentBottomAjusted;


            CellTempJumpConnection[] newConnections = GenericPoolArray<CellTempJumpConnection>.Take(32, false);
            int newConnectionsCount = 0;

            for (int currentPortalBaseArrayIndex = 0; currentPortalBaseArrayIndex < _portalBasesArrayCount; currentPortalBaseArrayIndex++) {
                JumpPortalBase portal = _portalBasesArray[currentPortalBaseArrayIndex];

                Vector3 topAdd = new Vector3(0, agentBottomAjusted, 0);
                Vector3 bottomAdd = new Vector3(0, agentHeightAjusted, 0);
                Vector3 portalPosV3 = portal.positionV3;
                Vector3 mountPointBottom = portalPosV3 + topAdd;
                Vector3 mountPointTop = portalPosV3 + bottomAdd;

                if (Physics.CheckCapsule(mountPointBottom, mountPointTop, rad, properties.includedLayers) ||
                    (Physics.CapsuleCast(mountPointBottom, mountPointTop, rad, portal.normal, out hitCapsule, rad * 3, mask) &&
                    SomeMath.SqrDistance(ToV2(hitCapsule.point), portal.positionV2) < (rad * 3) * (rad * 3))) {
                    continue;
                }

                bool doContinue = false;
                for (int i = 0; i < sampleSteps; i++) {
                    Vector3 normalOffset = portal.normal * (properties.radius + (i * sampleStep));
                    Vector3 axisPoint = mountPointBottom + normalOffset;

                    if (Physics.Raycast(axisPoint, Vector3.down, out hitRaycast, maxCheckDistance, mask) == false)
                        continue;

                    Vector3 raycastHitPoint = hitRaycast.point;

                    if (Physics.CapsuleCast(axisPoint, mountPointTop + normalOffset, rad, Vector3.down, out hitCapsule, Mathf.Infinity, mask) == false)
                        continue;

                    if (SomeMath.SqrDistance(raycastHitPoint, hitCapsule.point) > radSqr)
                        continue;

                    if (SomeMath.SqrDistance(portal.positionV3 + normalOffset, hitCapsule.point) < radSqr || Vector3.Angle(Vector3.up, hitCapsule.normal) > properties.maxSlope)
                        continue;

                    bool outside;
                    Cell closest;
                    Vector3 closestPos;

                    //search in only internal map
                    GetCellSimpleMap(raycastHitPoint.x, raycastHitPoint.y, raycastHitPoint.z, INVALID_VALUE, out closest, out closestPos, out outside);

                    //Debuger3.AddLine(raycastHitPoint, closestPos, Color.red);
                    //Debuger3.AddRay(raycastHitPoint, Vector3.up, Color.magenta, 0.5f);

                    if (outside)
                        continue;

                    Cell cell;
                    bool outsideCell;
                    Vector3 closestPoint;
                    //search in only internal map
                    GetCellSimpleMap(closestPos.x, closestPos.y, closestPos.z, INVALID_VALUE, out cell, out closestPoint, out outsideCell);

                    if (SomeMath.SqrDistance(closestPos, hitCapsule.point) > radSqr) {
                        doContinue = true;
                        break;
                    }

                    float fallSqrDistance = SomeMath.SqrDistance(portal.positionV3 + normalOffset, raycastHitPoint);

                    if (fallSqrDistance < jumpUpSqr) {
                        foreach (var pair in portal.cellMountPoints) {
                            if (newConnectionsCount == newConnections.Length)
                                GenericPoolArray<CellTempJumpConnection>.IncreaseSize(ref newConnections);
                            //newConnections[newConnectionsCount++] = new CellConnection(closestPos, raycastHitPoint, pair.Value, portal.positionV3, ConnectionJumpState.jumpUp, cell, pair.Key);
                            newConnections[newConnectionsCount++] = new CellTempJumpConnection(true, cell, pair.Key, raycastHitPoint, portal.positionV3, pair.Value);
                        }
                    }

                    if (fallSqrDistance < jumpDownSqr) {
                        foreach (var pair in portal.cellMountPoints) {
                            if (newConnectionsCount == newConnections.Length)
                                GenericPoolArray<CellTempJumpConnection>.IncreaseSize(ref newConnections);
                            //newConnections[newConnectionsCount++] = CellContentPointedConnection.GetFromPool(pair.Value, raycastHitPoint, closestPos, portal.positionV3, ConnectionJumpState.jumpDown, pair.Key, cell);
                            
                            newConnections[newConnectionsCount++] = new CellTempJumpConnection(false, pair.Key, cell, pair.Value, portal.positionV3, closestPos);
                        }
                    }
                    //goto NEXT_PORTAL;
                    doContinue = true;
                    break;
                }
                if (doContinue)
                    continue;
            }
            
            PathFinder.AddPathfinderThreadDelegate(() => {
                StackedListWithKeys<CellContentData, CellConnection> stackedData = StackedListWithKeys<CellContentData, CellConnection>.PoolTake(1, 1);
                stackedData.SetOptimizedData(_graphEdges, _graphConnections, _graphLayout, _cellsCount);

                GenericPoolArray<CellContentData>.ReturnToPool(ref _graphEdges);
                GenericPoolArray<CellConnection>.ReturnToPool(ref _graphConnections);
                GenericPoolArray<IndexLengthInt>.ReturnToPool(ref _graphLayout);

                for (int i = 0; i < newConnectionsCount; i++) {
                    CellTempJumpConnection con = newConnections[i];
                    CellContentData data = new CellContentData(con.enter, con.exit);
                    CellConnection newConnection = con.from.AddConnectionJump(con.enter, con.exit, con.axis, con.jumpUp ? ConnectionJumpState.jumpUp : ConnectionJumpState.jumpDown, con.connection);
                    stackedData.AddKeyValue(con.from.graphID, data, newConnection);
                }

                //return to pool array with connections;
                GenericPoolArray<CellTempJumpConnection>.ReturnToPool(ref newConnections);

                stackedData.GetOptimizedData(out _graphEdges, out _graphConnections, out _graphLayout);
                StackedListWithKeys<CellContentData, CellConnection>.PoolReturn(ref stackedData);

#if UNITY_EDITOR
                if (Debuger_K.doDebug)
                    DebugGraph(false); //cause it already updated from generation
#endif
            });
            PathFinder.Update();
        }

        private void CheckCellsForAdvancedAreas() {
            for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                Cell cell = _cellsArray[cellIndex]; //check if cell have advanced area
                if (cell.advancedAreaCell) 
                   (cell.area as AreaAdvanced).cells.Add(cell);                
            }
        }

        private void CheckCovers() {
            if (covers.Count == 0)
                return;

            for (int i = covers.Count - 1; i < 0; i--) {
                if (covers[i].coverPoints.Count == 0)
                    covers.RemoveAt(i);
            }
        }

        public int GetMaxLayer() {
            int result = 0;
            for (int i = 0; i < _cellsCount; i++) {
                int cur = _cellsArray[i].layer;
                if (result < cur)
                    result = cur;
            }
            return result;
        }

        public int cellsCount {
            get { return _cellsCount; }
        }

        public int jumpPortalBasesCount {
            get { return _portalBasesArrayCount; }
        }

#if UNITY_EDITOR
        public void DebugGraph(bool addCellMap) {
            Debuger_K.AddCells(this);
            Debuger_K.AddPortalBases(this);
            Debuger_K.AddCovers(x, z, properties, covers);
            if (addCellMap)
                Debuger_K.UpdateCellMap(this);

            Debuger_K.QueueUpdateSceneImportantThings();
        }

        public void DebugContent(StringBuilder sb, ref int counter) {
            for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                Cell cell = _cellsArray[cellIndex];
                if (cell.cellContentValues.Count > 0) {
                    counter += cell.cellContentValues.Count;
                    sb.Length = 0;
                    foreach (var ccv in cell.cellContentValues) {
                        sb.AppendLine(ccv.ToString());
                        Debuger_K.AddLine(ccv.position, cell.centerVector3, Color.blue);
                    }
                    Debuger_K.AddLabel(cell.centerVector3, sb.ToString());
                }
            }
        }
#endif
        private Vector2 ToV2(Vector3 v3) {
            return new Vector2(v3.x, v3.z);
        }
    }
}

