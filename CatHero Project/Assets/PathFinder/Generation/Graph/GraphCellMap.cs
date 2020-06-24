using K_PathFinder.CoolTools;
using K_PathFinder.Pool;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif


//TODO: swap Cell[] _cellRichMap to CompactCell?

namespace K_PathFinder.Graphs {
    //map of cells inside graph
    //this part of code responcible for updating cell map. it have 2 sets of maps:
    //1) simple = only this graph map
    //2) rich   = also have neighbour cells
    //all updates of rich map should be done in PathFinder thread and for this reason this class have static fields
    public partial class Graph {
        //rich map is map where searching nearby position to navmeshis performed
        //it have data for all layers and have closest cell where empty space are
        //it always should have at least 1 length
        Cell[] _cellRichMap;
        IndexLengthInt[] _cellRiсhMapLayout;

        //this map only have cells that laying down there. empty space have length of 0. include all layers
        //it used in generation of neighbours data
        Cell[] _cellSimpleMap;
        IndexLengthInt[] _cellSimpleMapLayout;
        bool _cellSimpleMapHaveEmptySpots;

        //side of map. right now map can be only quad so sides are equivalent
        int _cellMapResolution = 10;
               
        //Updating cell map actualy should be done in one big batch and only in main thread
        //So all kd tree values can be static to simplify it usage
        //KD tree values
        private static int rootBoundsTreeRoot;//index of first node
        private static RootInfo[] rootInfoArray;
        private static int rootInfoArrayLength;
        private static kDTreeBoundsBranch[] rootInfoBoundBranches;
        private static int boundBranchesLength = 0;

        private enum TreeSplitAxis : int {
            X = 0,
            Z = 1,
            END = 2
        }
        
        struct RootInfo {
            public Cell cell;
            public float x, z;

            public RootInfo(Cell Cell, float X, float Z) {
                cell = Cell;
                x = X;
                z = Z;
            }
        }

        struct kDTreeBoundsBranch {
            public int start, end, branchLow, branchHigh;
            public float minX, minZ, maxX, maxZ;
            public TreeSplitAxis splitAxis;

            public kDTreeBoundsBranch(int start, int end, int branchLow, int branchHigh, float minX, float minZ, float maxX, float maxZ, TreeSplitAxis splitAxis) {
                this.start = start;
                this.end = end;
                this.branchLow = branchLow;
                this.branchHigh = branchHigh;
                this.minX = minX;
                this.minZ = minZ;
                this.maxX = maxX;
                this.maxZ = maxZ;
                this.splitAxis = splitAxis;
            }
#if UNITY_EDITOR
            public void Draw(Color color, float height) {
                Vector3 v1 = new Vector3(minX, height, minZ);
                Vector3 v2 = new Vector3(minX, height, maxZ);
                Vector3 v3 = new Vector3(maxX, height, maxZ);
                Vector3 v4 = new Vector3(maxX, height, minZ);
                Debuger_K.AddLine(color, 0f, 0.001f, true, v1, v2, v3, v4);
            }
#endif
            public Vector3 center {
                get { return new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2); }
            }

            public override string ToString() {
                return string.Format("BL {0}, BH {1}, S{2}, E{3}, minX{4}, minZ{5}, maxX{6}, maxZ{7}", branchLow, branchHigh, start, end, minX, minZ, maxX, maxZ);
            }
        }
        
        //Debug
        class GenerationProfiler {
            public int layers;
            public int samples;

            public System.Diagnostics.Stopwatch total = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch getInitialData = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch rasterizationReading = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch buildingTree = new System.Diagnostics.Stopwatch();   
            public System.Diagnostics.Stopwatch getRootPoints = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch searchingInTree = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch generationEmptySpaceMap = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch settingNewMap = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch getOptimizedDataTime = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch poolOperationsTime = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch mergeTime = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch sqrTime = new System.Diagnostics.Stopwatch();
            public System.Diagnostics.Stopwatch test2 = new System.Diagnostics.Stopwatch();


            public void TellMeWhatHappened() {
                string timeLog = string.Format(
                    "total {0}\n" +
                    "getInitialData {1}\n" +
                    "rasterizationReading {2}\n" +
                    "buildingTree {3}\n" +
                    "getRootPoints {4}\n" +
                    "searchingInTree {5}\n" +
                    "generationEmptySpaceMap {6}\n" +
                    "settingNewMap {7}\n" +
                    "getOptimizaedDataTime {8}\n" +
                    "poolOperationsTime {9}\n" +
                    "mergeTime {10}\n" +
                    "mergeTime {11}\n" +
                    "test2 {12}\n",
                    total.Elapsed,
                    getInitialData.Elapsed,
                    rasterizationReading.Elapsed,
                    buildingTree.Elapsed,
                    getRootPoints.Elapsed,
                    searchingInTree.Elapsed,
                    generationEmptySpaceMap.Elapsed,
                    settingNewMap.Elapsed,
                    getOptimizedDataTime.Elapsed,
                    poolOperationsTime.Elapsed,
                    mergeTime.Elapsed,
                    sqrTime.Elapsed,
                    test2.Elapsed);

                Debug.LogFormat("Layers {0} samples {1}\n{2}",layers, samples, timeLog);
            }
        }
        
        /// <summary>
        /// generates simple map that contain raw data for cell map. it contain only raw data where cells are
        /// </summary>
        public void GenerateSimpleCellMap(int resolution) {
            float pixelSize = PathFinder.gridSize / resolution;
            int resolution_sqr = resolution * resolution;
            Vector3 chunkPosV3 = chunk.realPositionV3;
            float chunkX = chunkPosV3.x;
            float chunkZ = chunkPosV3.z;
            StackedList<Cell> map = StackedList<Cell>.PoolTake(resolution_sqr, resolution_sqr);

            //var curGraphCellArray = graph._cellsArray;
            //var curGraphCellArrayLength = graph._cellsCount;
            bool[] tempMap = GenericPoolArray<bool>.Take(resolution_sqr, makeDefault: true);
            for (int cellIndex = 0; cellIndex < _cellsCount; cellIndex++) {
                Cell cell = _cellsArray[cellIndex];
                Vector3 cellCenter = cell.centerVector3;
                CellContentData[] originalEdges = cell.originalEdges;
                int originalEdgesCount = cell.originalEdgesCount;

                for (int i = 0; i < originalEdgesCount; i++) {
                    CellContentData data = originalEdges[i];

                    //index is z * resolution + x
                    DDARasterization.Rasterize(tempMap, resolution, pixelSize,
                        cellCenter.x, cellCenter.z,
                        data.xLeft, data.zLeft,
                        data.xRight, data.zRight,
                        chunkX, chunkZ);
                }

                for (int x = 0; x < resolution; x++) {
                    for (int z = 0; z < resolution; z++) {
                        int index = (z * resolution) + x;
                        if (tempMap[index]) {
                            map.AddLast(index, cell);
                            tempMap[index] = false;
                            //float tX = chunkX + (x * pixelSize) + (pixelSize * 0.5f);
                            //float tZ = chunkZ + (z * pixelSize) + (pixelSize * 0.5f);
                            //Debuger_K.AddLine(new Vector3(tX, 0, tZ), cell.centerVector3, Color.cyan);
                        }
                    }
                }
            }

            _cellSimpleMapHaveEmptySpots = map.AnyEmptyBase();
            map.GetOptimizedData(out _cellSimpleMap, out _cellSimpleMapLayout);         

            StackedList<Cell>.PoolReturn(ref map);
            GenericPoolArray<bool>.ReturnToPool(ref tempMap);
        }
        
        //big refresh of all nearby maps
        //attemp number 9
        public void RefreshCellMap() {  
            //get profiler
            GenerationProfiler profiler = new GenerationProfiler();
            profiler.total.Start();

            profiler.getInitialData.Start();
            #region initial data
            int resolution = PathFinder.CELL_GRID_SIZE;
            float pixelSize = PathFinder.gridSize / PathFinder.CELL_GRID_SIZE;
            int resolution_sqr = resolution * resolution;

            rootInfoArray = GenericPoolArray<RootInfo>.Take(64);
            List<CellContentConnectionData> allContourEdgesList = GenericPool<List<CellContentConnectionData>>.Take();

            StackedList<Cell> mapLayer = StackedList<Cell>.PoolTake(resolution_sqr, resolution_sqr);

            //List<Cell> allCellsForRootMap = new List<Cell>();
            List<Graph> allGraphsForRootMap = new List<Graph>();
            List<Graph> graphsUpdated = new List<Graph>();
            
            bool[] rootSampleMap = new bool[25];

            PathFinder.TryGetGraph(gridPosition.x - 1, gridPosition.z - 1, 3, 3, properties, graphsUpdated, true);
            Dictionary<Graph, StackedList<Cell>> finishedMapDictionary = new Dictionary<Graph, StackedList<Cell>>();
                  
            
            for (int x = 0; x < 3; x++) {
                for (int z = 0; z < 3; z++) {
                    var graph = graphsUpdated[(z * 3) + x];
                    if (graph != null && graph._cellSimpleMapHaveEmptySpots) {                   
                        MarkRegionMask(rootSampleMap, x + 1, z + 1);
                        finishedMapDictionary.Add(graph, StackedList<Cell>.PoolTake(resolution_sqr, resolution_sqr));
                    }
                }
            }
            
            for (int x = 0; x < 5; x++) {
                for (int z = 0; z < 5; z++) {
                    Graph graph;
                    if (rootSampleMap[(z * 5) + x] && PathFinder.TryGetGraph(gridPosition.x + x - 2, gridPosition.z + z - 2, properties, out graph)) {
                        allGraphsForRootMap.Add(graph);

                        //var cArray = graph._cellsArray;
                        //int cCount = graph._cellsCount;

                        //for (ushort cellIndex = 0; cellIndex < cCount; cellIndex++) {
                        //    allCellsForRootMap.Add(cArray[cellIndex]);
                        //}
                    }
                }
            }
                  

            int maxLayer = 0;
            foreach (var graph in allGraphsForRootMap) {
                graph.GetCountour(allContourEdgesList);
                int maxGraphLayer = graph.GetMaxLayer();
                if (maxLayer < maxGraphLayer)
                    maxLayer = maxGraphLayer;
            }
            
            profiler.layers = maxLayer + 1;
            #endregion
            profiler.getInitialData.Stop(); 
            profiler.generationEmptySpaceMap.Start();

            //foreach (var item in allContourEdgesList) {
            //    Debuger_K.AddLine(item.data, Color.red, addOnTop:0.1f);
            //}

            Cell[] globalCells = PathFinderData.cells;

            //here treating each layer individualy
            for (int targetLayer = 0; targetLayer < maxLayer + 1; targetLayer++) {
                //generating root map
                profiler.getRootPoints.Start();
                rootInfoArrayLength = 0;
        
                foreach (var contourEdge in allContourEdgesList) {
                    Cell cell = globalCells[contourEdge.from];
                    if (cell.layer != targetLayer)
                        continue;

                    var data = contourEdge.data;
                    Vector2 dir = data.directionV2;
                    float magnitude = SomeMath.Magnitude(dir);
                    Vector2 normalized = dir / magnitude;
                    int sections = (int)(magnitude / pixelSize);
                    if (sections <= 1) {
                        if (rootInfoArray.Length == rootInfoArrayLength)
                            GenericPoolArray<RootInfo>.IncreaseSize(ref rootInfoArray);

                        rootInfoArray[rootInfoArrayLength++] = new RootInfo(cell,
                            normalized.x * (magnitude * 0.5f) + data.xLeft,
                            normalized.y * (magnitude * 0.5f) + data.zLeft
                        );
                    }
                    else {
                        float segment = magnitude / sections;

                        for (int i = 0; i < sections; i++) {
                            if (rootInfoArray.Length == rootInfoArrayLength)
                                GenericPoolArray<RootInfo>.IncreaseSize(ref rootInfoArray);

                            rootInfoArray[rootInfoArrayLength++] = new RootInfo(cell,
                                normalized.x * (segment * i) + data.xLeft + (normalized.x * segment * 0.5f),
                                normalized.y * (segment * i) + data.zLeft + (normalized.y * segment * 0.5f));
                        }
                    }
                }


                ////RootInfo[] newArrayDebug = new RootInfo[rootInfoArrayLength];
                //Debug.Log("Roots " + rootInfoArrayLength);
                //for (int i = 0; i < rootInfoArrayLength; i++) {
                //    var val = rootInfoArray[i];
                //    Debuger_K.AddDot(new Vector3(val.x, 0, val.z), Color.magenta);
                //    Debug.Log(val.x + " : " + val.z);
                //    //newArrayDebug[i] = val;
                //}
                ////rootInfoArray = newArrayDebug;


                profiler.getRootPoints.Stop();
                
                //building tree
                profiler.buildingTree.Start();
                BuildRootInfoBoundsTree(3);
                profiler.buildingTree.Stop();

                //DebugBoundsTree();

                profiler.test2.Start();
                foreach (var graph in graphsUpdated) {
                    if (graph == null || graph._cellSimpleMapHaveEmptySpots == false)
                        continue;

                    StackedList<Cell> mapFinished = finishedMapDictionary[graph];
                    mapLayer.Clear();

                    Vector3 chunkPosV3 = graph.chunk.realPositionV3;
                    float chunkX = chunkPosV3.x;
                    float chunkZ = chunkPosV3.z;

                    profiler.rasterizationReading.Start();
                    Cell[] curGraphSimpleMap = graph._cellSimpleMap;
                    IndexLengthInt[] curGraphSimpleMapLayout = graph._cellSimpleMapLayout;

                    for (int gridIndex = 0; gridIndex < resolution_sqr; gridIndex++) {
                        IndexLengthInt layout = curGraphSimpleMapLayout[gridIndex];
                        for (int i = 0; i < layout.length; i++) {
                            Cell cell = curGraphSimpleMap[layout.index + i];
                            if (cell.layer == targetLayer)
                                mapLayer.AddLast(gridIndex, cell);
                        }
                    }
                    profiler.rasterizationReading.Stop();


                    //searching empty space

                    int[] iterationStack = GenericPoolArray<int>.Take(16);
                    int iterationStackLength;

                    profiler.searchingInTree.Start();
                    
                    if (mapLayer.AnyEmptyBase()) {
                        if (rootInfoArrayLength == 0)
                            continue;

                        for (int x = 0; x < resolution; x++) {
                            for (int z = 0; z < resolution; z++) {
                                int index = (z * resolution) + x;

                                float targetX = chunkX + (x * pixelSize) + (pixelSize * 0.5f);
                                float targetZ = chunkZ + (z * pixelSize) + (pixelSize * 0.5f);

                                //Debuger_K.AddLabel(new Vector3(targetX, 0, targetZ), mapLayer.Count(index));
                        
                                if (mapLayer.Any(index) == false) {
                                    profiler.samples++;
                                    //copy-pasted GetNearestBoundsTree                                               

                                    RootInfo closestRoot = rootInfoArray[rootBoundsTreeRoot];
                                    float closestDistSqr = float.MaxValue;
                                    kDTreeBoundsBranch branch = rootInfoBoundBranches[rootBoundsTreeRoot];

                                    while (branch.splitAxis != TreeSplitAxis.END) {
                                        if (branch.splitAxis == TreeSplitAxis.X) {
                                            kDTreeBoundsBranch curBranchLow = rootInfoBoundBranches[branch.branchLow];
                                            kDTreeBoundsBranch curBranchHigh = rootInfoBoundBranches[branch.branchHigh];
                                            branch = (curBranchLow.maxX - targetX) * -1 < curBranchHigh.minX - targetX ? curBranchLow : curBranchHigh;
                                        }
                                        else {
                                            kDTreeBoundsBranch curBranchLow = rootInfoBoundBranches[branch.branchLow];
                                            kDTreeBoundsBranch curBranchHigh = rootInfoBoundBranches[branch.branchHigh];
                                            branch = (curBranchLow.maxZ - targetZ) * -1 < curBranchHigh.minZ - targetZ ? curBranchLow : curBranchHigh;
                                        }
                                    }
                                    for (int i = branch.start; i <= branch.end; i++) {
                                        RootInfo val = rootInfoArray[i];
                                        float curDistanceSqr =
                                            ((targetX - val.x) * (targetX - val.x)) +
                                            ((targetZ - val.z) * (targetZ - val.z));
                                            
                                        if (curDistanceSqr < closestDistSqr) {
                                            closestDistSqr = curDistanceSqr;
                                            closestRoot = val;
                                        }
                                    }


                                    profiler.sqrTime.Start();
                                    float closestDist = Mathf.Sqrt(closestDistSqr);
                                    profiler.sqrTime.Stop();

                                    iterationStack[0] = rootBoundsTreeRoot;
                                    iterationStackLength = 1;

                                    while (iterationStackLength != 0) {
                                        branch = rootInfoBoundBranches[iterationStack[--iterationStackLength]];
                                        if (branch.minX <= targetX + closestDist &&
                                            branch.maxX >= targetX - closestDist &&
                                            branch.minZ <= targetZ + closestDist &&
                                            branch.maxZ >= targetZ - closestDist) {


                                            if (branch.splitAxis == TreeSplitAxis.END) {
                                                for (int i = branch.start; i <= branch.end; i++) {
                                                    RootInfo val = rootInfoArray[i];
                                                    float curDistSqr = SomeMath.SqrDistance(val.x, val.z, targetX, targetZ);
                                                    if (curDistSqr < closestDistSqr) {
                                                        closestDistSqr = curDistSqr;
                                                        closestRoot = val;
                                                    }
                                                }
                                            }
                                            else {
                                                if (iterationStack.Length <= iterationStackLength + 2)
                                                    GenericPoolArray<int>.IncreaseSize(ref iterationStack);
                                                iterationStack[iterationStackLength++] = branch.branchHigh;
                                                iterationStack[iterationStackLength++] = branch.branchLow;
                                            }
                                        }
                                    }

                                    //Debuger_K.AddLine(new Vector3(closestRoot.x, 0, closestRoot.z), new Vector3(targetX, 0, targetZ), Color.magenta);
                                    mapLayer.AddLast(index, closestRoot.cell);
                                }
                            }
                        }                       
                    }

                    GenericPoolArray<int>.ReturnToPool(ref iterationStack);
                    profiler.searchingInTree.Stop();
                    //searching empty space

                    //merging result into total map
                    profiler.mergeTime.Start();
                    mapFinished.Merge(mapLayer);
                    profiler.mergeTime.Stop();
                    //merging result into total map                
                }
                profiler.test2.Stop();
            } 
            profiler.generationEmptySpaceMap.Stop();

            allContourEdgesList.Clear();
            GenericPool<List<CellContentConnectionData>>.ReturnToPool(ref allContourEdgesList);    
            StackedList<Cell>.PoolReturn(ref mapLayer);

            //string s2 = "Updated " + chunk.positionString + "\n";
            profiler.settingNewMap.Start();
            //UnityEngine.Debug.Log(finishedMapDictionary.Count);
            foreach (var pair in finishedMapDictionary) {
                var graph = pair.Key;
                var map = pair.Value;

                //s2 += string.Format("{0} \n", graph.chunk.positionString);

                Cell[] oData;
                IndexLengthInt[] oLayout;

                profiler.getOptimizedDataTime.Start();
                map.GetOptimizedData(out oData, out oLayout);
                profiler.getOptimizedDataTime.Stop();

                profiler.poolOperationsTime.Start();
                graph.ClearRichCellMap();
                profiler.poolOperationsTime.Stop();

                graph._cellRichMap = oData;
                graph._cellRiсhMapLayout = oLayout;
                graph._cellMapResolution = PathFinder.CELL_GRID_SIZE;
                //Vector3 chunkPosV3 = graph.chunk.realPositionV3;
                //float chunkX = chunkPosV3.x;
                //float chunkZ = chunkPosV3.z;
                //for (int x = 0; x < resolution; x++) {
                //    for (int z = 0; z < resolution; z++) {
                //        float tX = chunkX + (x * pixelSize) + (pixelSize * 0.5f);
                //        float tZ = chunkZ + (z * pixelSize) + (pixelSize * 0.5f);
                //        int index = (z * resolution) + x;
                //        IndexLengthInt IL = oLayout[index];
                //        for (int i = 0; i < IL.length; i++) {
                //            Debuger_K.AddLine(new Vector3(tX, 0, tZ), oData[IL.index + i].centerVector3, Color.blue);
                //        }
                //    }
                //}
                profiler.poolOperationsTime.Start();
                StackedList<Cell>.PoolReturn(ref map);
                profiler.poolOperationsTime.Stop();
            }

            if (_cellSimpleMapHaveEmptySpots == false) { //mean it itself was ignored in this whole process and only neighbours was updated (if was at all)
                _cellRiсhMapLayout = GenericPoolArray<IndexLengthInt>.Take(_cellSimpleMapLayout.Length);
                _cellRichMap = GenericPoolArray<Cell>.Take(_cellSimpleMap.Length);

                for (int i = 0; i < _cellSimpleMapLayout.Length; i++) {
                    _cellRiсhMapLayout[i] = _cellSimpleMapLayout[i];
                }

                for (int i = 0; i < _cellSimpleMap.Length; i++) {
                    _cellRichMap[i] = _cellSimpleMap[i];
                }
            }

            //Debug.LogFormat(s2);

#if UNITY_EDITOR
            Debuger_K.UpdateCellMap(graphsUpdated);
#endif

            profiler.settingNewMap.Stop();
            profiler.total.Stop();
            //profiler.TellMeWhatHappened();
        }
        
        void ClearRichCellMap() {
            if(_cellRichMap != null)
                GenericPoolArray<Cell>.ReturnToPool(ref _cellRichMap);
            if(_cellRiсhMapLayout != null)
                GenericPoolArray<IndexLengthInt>.ReturnToPool(ref _cellRiсhMapLayout);
        }

        #region get closest
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(float x, float y, float z, int layer, out Cell cell, out float resultY) {
            cell = null;
            resultY = 0;

            float scale = PathFinder.gridSize / PathFinder.CELL_GRID_SIZE;
            IndexLengthInt layout = _cellRiсhMapLayout[
                Mathf.Clamp((int)((z - chunk.realZ) / scale), 0, PathFinder.CELL_GRID_SIZE - 1) * _cellMapResolution +
                Mathf.Clamp((int)((x - chunk.realX) / scale), 0, PathFinder.CELL_GRID_SIZE - 1)];

            float diff = float.MaxValue;

            for (int la = 0; la < layout.length; la++) {
                //get current cell
                Cell curCell = _cellRichMap[layout.index + la];
                //check if it's inside target layer (it it's defined)
                if (layer != INVALID_VALUE && curCell.layer != layer)
                    continue;

                //take cell edges and center
                var cellOriginalEdges = curCell.originalEdges;
                int cellOriginalEdgesCount = curCell.originalEdgesCount;
                Vector3 cellCenter = curCell.centerVector3;

                for (int edgeIndex = 0; edgeIndex < cellOriginalEdgesCount; edgeIndex++) {
                    CellContentData edge = cellOriginalEdges[edgeIndex];

                    if (SomeMath.PointInTriangle(cellCenter.x, cellCenter.z, edge.xLeft, edge.zLeft, edge.xRight, edge.zRight, x, z)) {
                        float curY = SomeMath.CalculateHeight(edge.leftV3, edge.rightV3, cellCenter, x, z);
                        float curDiff = SomeMath.Difference(y, curY);
                        if (curDiff < diff) {
                            diff = curDiff;
                            cell = curCell;
                            resultY = curY;
                        }
                    }
                }
            }

            return cell != null;
        }
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(float x, float y, float z, int layer, out Cell cell, out Vector3 closestToCellPos) {
            float resultY;
            bool result = GetCell(x, y, z, layer, out cell, out resultY);
            closestToCellPos = new Vector3(x, resultY, z);
            return result;
        }
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(Vector3 position, int layer, out Cell cell, out float resultY) {
            return GetCell(position.x, position.y, position.z, layer, out cell, out resultY);
        }
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(Vector3 position, int layer, out Cell cell, out Vector3 closestToCellPos) {
            float Y;
            bool result = GetCell(position.x, position.y, position.z, layer, out cell, out Y);
            closestToCellPos = new Vector3(position.x, Y, position.z);
            return result;
        }
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(float x, float y, float z, out Cell cell, out float resultY) {
            return GetCell(x, y, z, INVALID_VALUE, out cell, out resultY);
        }
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(float x, float y, float z, out Cell cell, out Vector3 closestToCellPos) {
            float resultY;
            bool result = GetCell(x, y, z, out cell, out resultY);
            closestToCellPos = new Vector3(x, resultY, z);
            return result;
        }
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(Vector3 position, out Cell cell, out float resultY) {
            return GetCell(position.x, position.y, position.z, out cell, out resultY);
        }
        /// <summary>
        /// Return cell ONLY if it below target position. wount check nearest cell if it ouside this position.
        /// </summary>
        public bool GetCell(Vector3 position, out Cell cell, out Vector3 closestToCellPos) {
            float Y;
            bool result = GetCell(position.x, position.y, position.z, out cell, out Y);
            closestToCellPos = new Vector3(position.x, Y, position.z);
            return result;
        }
       
        /// <summary>
        /// semi-public function. instead of searching on full map it searches in simple map that contain cells only inside graph
        /// it userful to get graph cells while it generated. since simple map generated much earlier
        /// </summary>
        public bool GetCellSimpleMap(float x, float y, float z, int layer, out Cell cell, out Vector3 closestPoint, out bool outsideCell) {
            float scale = PathFinder.gridSize / PathFinder.CELL_GRID_SIZE;
            IndexLengthInt layout = _cellSimpleMapLayout[
                Mathf.Clamp((int)((z - chunk.realZ) / scale), 0, PathFinder.CELL_GRID_SIZE - 1) * _cellMapResolution +
                Mathf.Clamp((int)((x - chunk.realX) / scale), 0, PathFinder.CELL_GRID_SIZE - 1)];

            cell = null;
            closestPoint.x = x;
            closestPoint.y = y;
            closestPoint.z = z;  
            float sqrDist = float.MaxValue;
            
            //it this case only thiangles checked. cause cell map dont contain all possible results
            for (int i = 0; i < layout.length; i++) {
                Cell curCell = _cellSimpleMap[layout.index + i];           

                if (layer != INVALID_VALUE && curCell.layer != layer)
                    continue;

                //take cell edges and center
                var cellOriginalEdges = curCell.originalEdges;
                int cellOriginalEdgesCount = curCell.originalEdgesCount;
                Vector3 cellCenter = curCell.centerVector3;

                //if (debug) {
                //    Debuger_K.AddLine(new Vector3(x,y,z), cellCenter, Color.cyan);
                //}


                for (int edgeIndex = 0; edgeIndex < cellOriginalEdgesCount; edgeIndex++) {
                    CellContentData edge = cellOriginalEdges[edgeIndex];

                    //if point it triangle then calculate point on that triangle
                    if (SomeMath.PointInTriangleClockwise(cellCenter.x, cellCenter.z, edge.xLeft, edge.zLeft, edge.xRight, edge.zRight, x, z)) {
                        float curY = SomeMath.CalculateHeight(edge.leftV3, edge.rightV3, cellCenter, x, z);                        
                        float curDiff = y - curY;
                        if (curDiff < 0) curDiff = curDiff * -1f;
                        curDiff = curDiff * curDiff; //sqr it
                        
                        //if (debug) {
                        //    Debuger_K.AddLine(cellCenter, edge.leftV3, Color.magenta, addOnTop: 0.2f, width:0.005f);
                        //    Debuger_K.AddLine(cellCenter, edge.rightV3, Color.magenta, addOnTop: 0.2f, width: 0.005f);
                        //    Debuger_K.AddLine(edge.leftV3, edge.rightV3, Color.magenta, addOnTop: 0.2f, width: 0.005f);
                        //    Debuger_K.AddLabel(new Vector3(x, curY, z), curDiff);
                        //}

                        if (curDiff < sqrDist) {
                            sqrDist = curDiff;
                            cell = curCell;                    
                            closestPoint.y = curY;                                    
                        }
                    }
                }
            }   

            if (cell != null) {
                outsideCell = false;
                return true;
            }

            outsideCell = true;
            sqrDist = float.MaxValue;

            //no result in simple map at this point. is target position outside graph?
            //performing search over contour
            CellContentConnectionData[] pooledArray = GenericPoolArray<CellContentConnectionData>.Take(64);
            int pooledArrayLength = 0;
            borderData.Read(9, ref pooledArray, out pooledArrayLength);

            for (int i = 0; i < pooledArrayLength; i++) {
                CellContentConnectionData data = pooledArray[i];
                Cell curCell = _cellsArray[data.from];

                if (layer != INVALID_VALUE && curCell.layer != layer)
                    continue;

                Vector3 curClosestPoint = data.data.NearestPoint(x, y, z);
                float curSqrDist = SomeMath.SqrDistance(x, y, z, curClosestPoint);
                if (sqrDist > curSqrDist) {
                    sqrDist = curSqrDist;
                    cell = curCell;
                    closestPoint = curClosestPoint;
                }
            }   

            GenericPoolArray<CellContentConnectionData>.ReturnToPool(ref pooledArray);


            return cell != null;
        }


        /// <summary>
        /// test all outlines of Cells and return closest to target position
        /// right now less optimised than it realy should be
        /// </summary>
        public void GetClosestToHull(float x, float y, float z, int layer, out Cell cell, out Vector3 closestToOutlinePos) {
            cell = null;
            closestToOutlinePos = new Vector3();
            float scale = PathFinder.gridSize / PathFinder.CELL_GRID_SIZE;

            IndexLengthInt layout = _cellRiсhMapLayout[
                Mathf.Clamp((int)((z - chunk.realZ) / scale), 0, PathFinder.CELL_GRID_SIZE - 1) * PathFinder.CELL_GRID_SIZE +
                Mathf.Clamp((int)((x - chunk.realX) / scale), 0, PathFinder.CELL_GRID_SIZE - 1)];

            float sqrDist = float.MaxValue;

            for (int i = 0; i < layout.length; i++) {        
                Cell curCell = _cellRichMap[layout.index + i];  
                
                if (layer != INVALID_VALUE && curCell.layer != layer)
                    continue;
                
                var originalEdges = curCell.originalEdges;
                int originalEdgesCount = curCell.originalEdgesCount;

                for (int e = 0; e < originalEdgesCount; e++) {
                    Vector3 curNearest = originalEdges[e].NearestPoint(x, y, z);
                    float curSqrDist = SomeMath.SqrDistance(curNearest.x, curNearest.y, curNearest.z, x, y, z);
                    if (curSqrDist < sqrDist) {
                        sqrDist = curSqrDist;
                        cell = curCell;
                        closestToOutlinePos = curNearest;
                    }
                }
            }
        }
        /// <summary>
        /// test all outlines oc Cell and return closest to target position
        /// </summary>
        public void GetClosestToHull(Vector3 position, int layer, out Cell cell, out Vector3 closestToOutlinePos) {
            GetClosestToHull(position.x, position.y, position.z, layer, out cell, out closestToOutlinePos);
        }
        /// <summary>
        /// test all outlines oc Cell and return closest to target position
        /// </void>
        public void GetClosestToHull(float x, float y, float z, out Cell cell, out Vector3 closestToOutlinePos) {
            GetClosestToHull(x, y, z, INVALID_VALUE, out cell, out closestToOutlinePos);
        }
        /// <summary>
        /// test all outlines oc Cell and return closest to target position
        /// </summary>
        public void GetClosestToHull(Vector3 position, out Cell cell, out Vector3 closestToOutlinePos) {
            GetClosestToHull(position.x, position.y, position.z, out cell, out closestToOutlinePos);
        }

        /// <summary>
        /// most generic function to srarch point on navmesh
        /// </summary>
        /// <param name="x">world X position</param>
        /// <param name="y">world Y position</param>
        /// <param name="z">world Z position</param>
        /// <param name="navmeshLayer">internal navmesh value. navmesh sepparated to different 2D layers internaly and this is way to force search on different layers. set -1 to ignore this</param>
        /// <param name="layerMask">what flag should be used for search. set 1 to default layer</param>
        /// <param name="resultPos">nearest position on navmesh</param>
        /// <param name="resultCell">nearest cell on navmesh (not threadsafe)</param>
        /// <returns>type of result. is navmesh there / not there / not at all</returns>
        public NavmeshSampleResultType GetClosestCell(float x, float y, float z, out Vector3 resultPos, out Cell resultCell, int navmeshLayer = -1, int layerMask = 1) {
            NavmeshSampleResultType result = NavmeshSampleResultType.OutsideNavmesh;
            resultPos.x = x;
            resultPos.y = y;
            resultPos.z = z;
            resultCell = null;
            
            float sqrDist = float.MaxValue;   
            IndexLengthInt layout = _cellRiсhMapLayout[
                Mathf.Clamp((int)((z - chunk.realZ) / (PathFinder.gridSize / PathFinder.CELL_GRID_SIZE)), 0, PathFinder.CELL_GRID_SIZE - 1) * PathFinder.CELL_GRID_SIZE +
                Mathf.Clamp((int)((x - chunk.realX) / (PathFinder.gridSize / PathFinder.CELL_GRID_SIZE)), 0, PathFinder.CELL_GRID_SIZE - 1)];

            //UnityEngine.Debug.Log(layout);
            for (int i = 0; i < layout.length; i++) {
                Cell curCell = _cellRichMap[layout.index + i];

                if (navmeshLayer != -1 && curCell.layer != navmeshLayer)
                    continue;
                
                //take cell edges and center
                var cellOriginalEdges = curCell.originalEdges;
                int cellOriginalEdgesCount = curCell.originalEdgesCount;
                Vector3 cellCenter = curCell.centerVector3;

                //if point right in middle of cell
                if(x - cellCenter.x == 0f && z - cellCenter.z == 0f) {
                    float difY = cellCenter.y - y;
                    if (difY < 0) difY *= -1f;
                    difY = difY * difY; //sqr this value

                    if(difY < sqrDist) {
                        sqrDist = difY;
                        resultPos.x = x;
                        resultPos.y = cellCenter.y;
                        resultPos.z = z;
                        resultCell = curCell;
                        result = NavmeshSampleResultType.InsideNavmesh;                         
                    }
                    continue;
                }

                //check if target position inside cell
                bool inside = true;
                for (int edgeIndex = 0; edgeIndex < cellOriginalEdgesCount; edgeIndex++) {
                    CellContentData edge = cellOriginalEdges[edgeIndex];
                    if(SomeMath.V2Cross(edge.xLeft - edge.xRight, edge.zLeft - edge.zRight, x - edge.xRight, z - edge.zRight) > 0f) {
                        inside = false;
                        break;
                    }
                }

                if (inside) {
                    float dirX = x - cellCenter.x;
                    float dirZ = z - cellCenter.z;

                    for (int edgeIndex = 0; edgeIndex < cellOriginalEdgesCount; edgeIndex++) {
                        CellContentData edge = cellOriginalEdges[edgeIndex];

                        if (SomeMath.V2Cross(dirX, dirZ, edge.xLeft - cellCenter.x, edge.zLeft - cellCenter.z) <= 0f && 
                            SomeMath.V2Cross(dirX, dirZ, edge.xRight - cellCenter.x, edge.zRight - cellCenter.z) >= 0f) {
                            //i know this dont make sence. this is copy-paste SomeMath.CalculateHeight which calculate height inside gicen triangle at x, z
                            //this kinda perfomance critical part of code                             
                            float det = (edge.zRight - cellCenter.z) * (edge.xLeft - cellCenter.x) + (cellCenter.x - edge.xRight) * (edge.zLeft - cellCenter.z);
                            float l1 = ((edge.zRight - cellCenter.z) * (x - cellCenter.x) + (cellCenter.x - edge.xRight) * (z - cellCenter.z)) / det;
                            float l2 = ((cellCenter.z - edge.zLeft) * (x - cellCenter.x) + (edge.xLeft - cellCenter.x) * (z - cellCenter.z)) / det;
                            float l3 = 1.0f - l1 - l2;
                            float height = l1 * edge.yLeft + l2 * edge.yRight + l3 * cellCenter.y;

                            float difY = height - y;
                            if (difY < 0) difY *= -1f;
                            difY = difY * difY; //sqr this value

                            if (result == NavmeshSampleResultType.OutsideNavmesh)//if current best result is outside cell
                                difY -= 0.05f;//we decrease it slightly with HARDCODED VALUE. so all results inside cell slightly more valid than outside results

                            if (difY < sqrDist) {
                                sqrDist = difY;
                                resultPos.x = x;
                                resultPos.y = cellCenter.y;
                                resultPos.z = z;
                                resultCell = curCell;
                                result = NavmeshSampleResultType.InsideNavmesh;
                            }
                            break;
                        }
                    }
                }
                else {
                    //outside
                    for (int edgeIndex = 0; edgeIndex < cellOriginalEdgesCount; edgeIndex++) {                 
                        Vector3 curNearest = cellOriginalEdges[edgeIndex].NearestPoint(x, y, z);
                        //float curSqrDist = SomeMath.SqrDistance(curNearest.x, curNearest.y, curNearest.z, x, y, z);

                        float curSqrDist = 
                            ((x - curNearest.x) * (x - curNearest.x)) +//x
                            ((y - curNearest.y) * (y - curNearest.y)) +//y
                            ((z - curNearest.z) * (z - curNearest.z)); //z

                        if (curSqrDist < sqrDist) {
                            sqrDist = curSqrDist;
                            resultCell = curCell;
                            resultPos = curNearest;
                            result = NavmeshSampleResultType.OutsideNavmesh;
                        }
                    }           
                }
            }

            //result outside navmesh
            if (result == NavmeshSampleResultType.OutsideNavmesh) 
                return result;
            
            if ((1 << resultCell.bitMaskLayer & layerMask) == 0) {
                GetClosestPossibleToUsePoint(x, y, z, resultCell, out resultPos, out resultCell, layerMask);
                result = NavmeshSampleResultType.InvalidByLayerMask;
            }

            if (resultCell != null) {
                resultPos.x += (resultCell.centerVector3.x - resultPos.x) * 0.001f;
                resultPos.z += (resultCell.centerVector3.z - resultPos.z) * 0.001f;
            }
            return result;
        }
        
        private static void GetClosestPossibleToUsePoint(float x, float y, float z, Cell cell, out Vector3 resultPos, out Cell resultCell, int layerMask) {
            bool[] flags = GenericPoolArray<bool>.Take(PathFinderData.maxRegisteredCellID + 1, defaultValue: false);
            Cell[] usedCells = GenericPoolArray<Cell>.Take(64);
            Cell[] globalCells = PathFinderData.cells;
         
            int curStart = 0;
            int curEnd = 1;
            int usedCellsCount = 1;
            usedCells[0] = cell;
            flags[cell.globalID] = true;
 
            resultPos = new Vector3();
            resultCell = null;
            float sqrDist = float.MaxValue;

            int counter = 0;

            while (true) {
                counter++;
                if (counter > 100)
                    throw new System.Exception("counter > 100");

                for (int i = curStart; i < curEnd; i++) {
                    Cell curIteration = usedCells[i];

                    int cellConnectionsCount = curIteration.connectionsCount;
                    CellConnection[] cellConnections = curIteration.connections;
                    CellContentData[] cellConnectionsData = curIteration.connectionsDatas;

                    for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                        CellConnection con = cellConnections[connectionIndex];

                        if(con.type == CellConnectionType.Generic) {
                            Cell curConnection = globalCells[con.connection];

                            if ((1 << curConnection.bitMaskLayer & layerMask) != 0) {
                                Vector3 curP = cellConnectionsData[connectionIndex].NearestPoint(x, y, z);
                                float curSqrDist = SomeMath.SqrDistance(x, y, z, curP);
                                if(curSqrDist < sqrDist) {
                                    sqrDist = curSqrDist;
                                    resultPos = curP;
                                    resultCell = curConnection;
                                }
                            }
                            else {
                                if (flags[curConnection.globalID] == false) {
                                    flags[curConnection.globalID] = true;
                                    if (usedCells.Length == usedCellsCount)
                                        GenericPoolArray<Cell>.IncreaseSize(ref usedCells);
                                    usedCells[usedCellsCount++] = curConnection;                               
                                }
                            }
                        }
                    }
                }

                if (resultCell != null)
                    break;

                if (curEnd == usedCellsCount)//no new cells mean there is nothing to check
                    break;

                curStart = curEnd;
                curEnd = usedCellsCount;
            }

            GenericPoolArray<bool>.ReturnToPool(ref flags);
            GenericPoolArray<Cell>.ReturnToPool(ref usedCells);
        }

        /// <summary>
        /// return closest to target point cell
        /// </summary>
        public NavmeshSampleResult GetClosestCell(Vector3 pos, int layer = -1, int bitmaskLayer = 0) {
            NavmeshSampleResult result;
            result.originX = pos.x;
            result.originY = pos.y;
            result.originZ = pos.z;        
            result.type = GetClosestCell(pos.x, pos.y, pos.z, out pos, out result.cell, layer, bitmaskLayer);
            result.positionX = pos.x;
            result.positionY = pos.y;
            result.positionZ = pos.z;
            return result;
        }
        /// <summary>
        /// return closest to target point cell
        /// </summary>
        public NavmeshSampleResult GetClosestCell(float x, float y, float z) {
            NavmeshSampleResult result;
            result.originX = x;
            result.originY = y;
            result.originZ = z;
            Vector3 pos;
            result.type = GetClosestCell(x, y, z, out pos, out result.cell);
            result.positionX = pos.x;
            result.positionY = pos.y;
            result.positionZ = pos.z;
            return result;
        }
        /// <summary>
        /// return closest to target point cell
        /// </summary>
        public NavmeshSampleResult GetClosestCell(Vector3 pos) {
            NavmeshSampleResult result;
            result.originX = pos.x;
            result.originY = pos.y;
            result.originZ = pos.z;
            result.type = GetClosestCell(pos.x, pos.y, pos.z, out pos, out result.cell);
            result.positionX = pos.x;
            result.positionY = pos.y;
            result.positionZ = pos.z;
            return result;
        }
        #endregion
                
        public void GetCellMapToSerialization(out int cellMapResolution, 
            out Cell[] cellRichMap, out IndexLengthInt[] cellRichMapLayout,
            out Cell[] cellSimpleMap, out IndexLengthInt[] cellSimpleMapLayout, out bool cellSimpleMapHaveEmptySpots) {    
            cellRichMap = _cellRichMap;
            cellRichMapLayout = _cellRiсhMapLayout;
            cellMapResolution = _cellMapResolution;
            cellSimpleMap = _cellSimpleMap;
            cellSimpleMapLayout = _cellSimpleMapLayout;
            cellSimpleMapHaveEmptySpots = _cellSimpleMapHaveEmptySpots;
        }

        public void GetCellMapToDebug(out int cellMapResolution, out Cell[] cellRichMap, out IndexLengthInt[] cellRichMapLayout) {
            cellRichMap = _cellRichMap;
            cellRichMapLayout = _cellRiсhMapLayout;
            cellMapResolution = _cellMapResolution;
        }

        public void SetCellMapFromSerialization(int cellMapResolution, 
            Cell[] cellRichMap, IndexLengthInt[] cellRichMapLayout, 
            Cell[] cellSimpleMap, IndexLengthInt[] cellSimpleMapLayout, bool cellSimpleMapHaveEmptySpots) {
            ClearRichCellMap();
            _cellRichMap = cellRichMap;
            _cellRiсhMapLayout = cellRichMapLayout;
            _cellMapResolution = cellMapResolution;
            _cellSimpleMap = cellSimpleMap;
            _cellSimpleMapLayout = cellSimpleMapLayout;
            _cellSimpleMapHaveEmptySpots = cellSimpleMapHaveEmptySpots;
        }

        public bool anyCellMapData {
            get { return _cellRichMap != null && _cellRichMap.Length > 0; }
        }

        private void MarkRegionMask(bool[] map, int posX, int posZ) {
            map[((posZ - 1) * 5) + posX - 1] = true;
            map[((posZ - 1) * 5) + posX] = true;
            map[((posZ - 1) * 5) + posX + 1] = true;
            map[(posZ * 5) + posX - 1] = true;
            map[(posZ * 5) + posX] = true;
            map[(posZ * 5) + posX + 1] = true;
            map[((posZ + 1) * 5) + posX - 1] = true;
            map[((posZ + 1) * 5) + posX] = true;
            map[((posZ + 1) * 5) + posX + 1] = true;
        }

        #region bounds kd tree
        static void BuildRootInfoBoundsTree(int membersPerBranch) {
            if (rootInfoBoundBranches != null)
                GenericPoolArray<kDTreeBoundsBranch>.ReturnToPool(ref rootInfoBoundBranches);
            rootInfoBoundBranches = GenericPoolArray<kDTreeBoundsBranch>.Take(32);
            boundBranchesLength = 0;

            rootBoundsTreeRoot = BuildRecursiveFootInfoBoundsTree(0, rootInfoArrayLength - 1, membersPerBranch);
        }

        static int BuildRecursiveFootInfoBoundsTree(int leftStart, int rightStart, int membersPerBranch) {
            int count = rightStart - leftStart;    
            
            float minX, minZ, maxX, maxZ;
            RootInfo root = rootInfoArray[leftStart];
            minX = maxX = root.x;
            minZ = maxZ = root.z;

            for (int i = leftStart + 1; i <= rightStart; i++) {
                root = rootInfoArray[i];
                if (root.x < minX) minX = root.x;
                if (root.z < minZ) minZ = root.z;
                if (root.x > maxX) maxX = root.x;
                if (root.z > maxZ) maxZ = root.z;
            }
            
            if (count <= membersPerBranch) {
                if (rootInfoBoundBranches.Length == boundBranchesLength)
                    GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref rootInfoBoundBranches);
                rootInfoBoundBranches[boundBranchesLength++] = new kDTreeBoundsBranch(leftStart, rightStart, INVALID_VALUE, INVALID_VALUE, minX, minZ, maxX, maxZ, TreeSplitAxis.END);
                return boundBranchesLength - 1;
            }
            else {
                int left = leftStart;
                int right = rightStart;
                float pivot = 0f;
                TreeSplitAxis axis;

                if ((maxX - minX) > (maxZ - minZ)) {//deside how to split. if size X > size Y then X            
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += rootInfoArray[i].x;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (rootInfoArray[left].x < pivot) left++;
                        while (rootInfoArray[right].x > pivot) right--;
                        if (left <= right) {
                            RootInfo tempData = rootInfoArray[left];
                            rootInfoArray[left++] = rootInfoArray[right];
                            rootInfoArray[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.X;
                }
                else {//y
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += rootInfoArray[i].z;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (rootInfoArray[left].z < pivot) left++;
                        while (rootInfoArray[right].z > pivot) right--;
                        if (left <= right) {
                            RootInfo tempData = rootInfoArray[left];
                            rootInfoArray[left++] = rootInfoArray[right];
                            rootInfoArray[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.Z;
                }

                int L = BuildRecursiveFootInfoBoundsTree(leftStart, right, membersPerBranch);
                int H = BuildRecursiveFootInfoBoundsTree(left, rightStart, membersPerBranch);
                if (rootInfoBoundBranches.Length == boundBranchesLength)
                    GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref rootInfoBoundBranches);
                rootInfoBoundBranches[boundBranchesLength++] = new kDTreeBoundsBranch(leftStart, rightStart, L, H, minX, minZ, maxX, maxZ, axis);
                return boundBranchesLength - 1;
            }
        }
        
        static RootInfo GetNearestBoundsTree(float targetX, float targetZ, ref int[] iterationStack) {
            RootInfo result = rootInfoArray[rootBoundsTreeRoot];
            float closestDistSqr = float.MaxValue;
            kDTreeBoundsBranch branch = rootInfoBoundBranches[rootBoundsTreeRoot];

            while (branch.splitAxis != TreeSplitAxis.END) {
                if (branch.splitAxis == TreeSplitAxis.X) {
                    kDTreeBoundsBranch curBranchLow = rootInfoBoundBranches[branch.branchLow];
                    kDTreeBoundsBranch curBranchHigh = rootInfoBoundBranches[branch.branchHigh];
                    branch = (curBranchLow.maxX - targetX) * -1 < curBranchHigh.minX - targetX ? curBranchLow : curBranchHigh;
                }
                else {
                    kDTreeBoundsBranch curBranchLow = rootInfoBoundBranches[branch.branchLow];
                    kDTreeBoundsBranch curBranchHigh = rootInfoBoundBranches[branch.branchHigh];
                    branch = (curBranchLow.maxZ - targetZ) * -1 < curBranchHigh.minZ - targetZ ? curBranchLow : curBranchHigh;
                }
            }
            for (int i = branch.start; i < branch.end; i++) {
                RootInfo val = rootInfoArray[i];
                float curDistSqr = SomeMath.SqrDistance(val.x, val.z, targetX, targetZ);
                if (curDistSqr < closestDistSqr) {
                    closestDistSqr = curDistSqr;
                    result = val;
                }
            }

            float closestDist = Mathf.Sqrt(closestDistSqr);
      
            int iterationStackLength = 1;
            iterationStack[0] = rootBoundsTreeRoot;

            while (iterationStackLength != 0) {
                branch = rootInfoBoundBranches[iterationStack[--iterationStackLength]];
                if (branch.minX <= targetX + closestDist &&
                    branch.maxX >= targetX - closestDist &&
                    branch.minZ <= targetZ + closestDist &&
                    branch.maxZ >= targetZ - closestDist) {


                    if (branch.splitAxis == TreeSplitAxis.END) {
                        for (int i = branch.start; i < branch.end; i++) {
                            RootInfo val = rootInfoArray[i];
                            float curDistSqr = SomeMath.SqrDistance(val.x, val.z, targetX, targetZ);
                            if (curDistSqr < closestDistSqr) {
                                closestDistSqr = curDistSqr;
                                result = val;
                            }
                        }
                    }
                    else {
                        if (iterationStack.Length <= iterationStackLength + 2)
                            GenericPoolArray<int>.IncreaseSize(ref iterationStack);
                        iterationStack[iterationStackLength++] = branch.branchHigh;
                        iterationStack[iterationStackLength++] = branch.branchLow;
                    }
                }
            }
            return result;
        }

#if UNITY_EDITOR
        static void DebugBoundsTree() {
            DebugRecursive(rootBoundsTreeRoot, 0);
        }
        
        static void DebugRecursive(int target, int depth) {
            float height = depth;
            //float height = 0;
            var branch = rootInfoBoundBranches[target];

            branch.Draw(new Color(Random.value, Random.value, Random.value, 1f), height);

            if (branch.splitAxis == TreeSplitAxis.END) {
                Vector3 center = branch.center;
                center = new Vector3(center.x, height, center.z);
                Debuger_K.AddLabel(center, branch.end - branch.start + 1);
                for (int i = branch.start; i <= branch.end; i++) {
                    RootInfo memder = rootInfoArray[i];
                    Debuger_K.AddLine(center, new Vector3(memder.x, height, memder.z), Color.blue, 0.0025f);
                    Debuger_K.AddDot(new Vector3(memder.x, height, memder.z), Color.green, 0.1f);
                }
            }
            else {
                DebugRecursive(branch.branchLow, depth + 1);
                DebugRecursive(branch.branchHigh, depth + 1);
            }
        }
#endif
        #endregion
    }
}