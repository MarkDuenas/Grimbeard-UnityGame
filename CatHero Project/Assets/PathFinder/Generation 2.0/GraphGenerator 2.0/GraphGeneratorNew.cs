using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using K_PathFinder.VectorInt;

using System;
using K_PathFinder.Graphs;
using K_PathFinder.NodesNameSpace;
using K_PathFinder.EdgesNameSpace;
using K_PathFinder;
using K_PathFinder.CoverNamespace;
using K_PathFinder.CoolTools;
using K_PathFinder.Pool;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif


namespace K_PathFinder.GraphGeneration {
    public partial class GraphGeneratorNew {
        private NavMeshTemplateCreation template;
        public VolumeContainerNew volumeContainer;

        private int 
            nodesSizeX, 
            nodesSizeZ, 
            nodesSizeFlatten;

        private Vector3 chunkRealPosition; //to get node position

        private float 
            fragmentSize,     //to get node position 
            halfFragmentSize,//to get node position
            maxStepHeight;   //if nodes are closer than step height then they will be returned.
        
        private List<VolumeArea> volumeAreas;
        NavmeshProfiler profiler;
        
        public GraphGeneratorNew(VolumeContainerNew volumeContainer, NavMeshTemplateCreation template) {
            this.volumeContainer = volumeContainer;
            this.template = template;
            this.profiler = template.profiler;            
            
            chunkRealPosition = new Vector3(template.chunkData.realX, 0, template.chunkData.realZ);

            fragmentSize = template.voxelSize;
            halfFragmentSize = fragmentSize * 0.5f;
            maxStepHeight = template.properties.maxStepHeight;

            nodesSizeX = template.lengthX_extra * 2;
            nodesSizeZ = template.lengthZ_extra * 2;
            nodesSizeFlatten = nodesSizeX * nodesSizeZ;        

            volumeAreas = volumeContainer.volumeAreas;
            InitEdgeContainer();
        }

        public int GetNodeIndex(int x, int z) {
            return (z * nodesSizeX) + x;
        }

        public Graph MakeGraph() {
            Graph graph = new Graph(template.chunkData, template.properties);
            //nothing to do just return empty graph
            //if (volumeContainer.volumesAmount == 0)
            //    return graph;

            //there is reason why it here. it's create captured areas in process so it always must be before next part
            if (template.doCover) {
                if (profiler != null) profiler.AddLog("agent can cover. start making cover graph");
                InitCoverGenerator();
                GenerateCoversNotNeat(); 

                if (profiler != null) profiler.AddLog("end making cover graph. start Ramer–Douglas–Peucker");
                RamerDouglasPeuckerCoverNew(template.voxelSize * 0.5f, template.voxelSize);           
                if (profiler != null) {
                    int coversCount = 0;
                    for (int i = 0; i < coverNodesCreated; i++) { if (coverNodesArray[i].ID != INVALID_VALUE) coversCount++; }
                    profiler.AddLogFormat("end Ramer–Douglas–Peucker. cover points: {0}", coversCount);
                }
                //DebugTempCovers();

                if (profiler != null) profiler.AddLog("start setuping cover points");
                SetupCoversData();
                if (profiler != null) profiler.AddLog("end setuping cover points");
            }

            if (template.doNavMesh) {
                //here is marching squares used   
                if (profiler != null) profiler.AddLog("creating contour for NavMesh");
                //var dataLayers = volumeContainer.dataLayers;

                GenerateNavMeshCountourNotNeatNew(template.canJump | template.doCover);
     
                //GenerateNavMeshCountourNotNeat(template.canJump | template.doCover);
                StackedList<VolumeArea>.PoolReturn(ref volumeContainer.areaSet);

                //DebugNewEdgeState();

                if (profiler != null) profiler.AddLog("end creating contour");

                //add border flags, shift near border to border nodes and insert corner nodes
                if (profiler != null) profiler.AddLog("start moving contour to chunk border");

                SetupBorders();

                if (profiler != null) profiler.AddLog("end moving contour to chunk border");

#if UNITY_EDITOR
                if (Debuger_K.doDebug && Debuger_K.debugOnlyNavMesh == false) {
                    if (profiler != null) profiler.AddLog("debug raw nodes");
                    Debuger_K.AddNodesPreRDP(template.gridPosX, template.gridPosZ, template.properties, this);
                    if (profiler != null) profiler.AddLog("end debug raw nodes");
                }
#endif

                //return new Graph(template.chunkData, template.properties);
                //Ramer-Douglas-Peucker
                if (profiler != null) profiler.AddLog("start Ramer–Douglas–Peucker");



                //SetupKeyMarkers();
                //bool ifResultFixed;
                //RamerDouglasPeuckerHullNew(template.voxelSize * 0.70f, out ifResultFixed);

                ////DebugNewEdgeState(true, 0.2f, 1.ToString());

                //if (ifResultFixed) {
                //    SetupKeyMarkers();
                //    RamerDouglasPeuckerHullNew(template.voxelSize * 0.70f, out ifResultFixed, true, true, false);
                //}

                //DebugNewEdgeState(true, 0, 0.ToString());

                //DebugNewEdgeState(true, 0.4f, 2.ToString());

                while (true) {
                    bool ifResultFixed;
                    SetupKeyMarkers();
                    RamerDouglasPeuckerHullNew(template.voxelSize * 0.70f, out ifResultFixed);

                    //Debug.Log("result fixed " + ifResultFixed);
                    if (ifResultFixed) {
                        if (profiler != null) profiler.AddLog("end Ramer–Douglas–Peucker");
                    }
                    else {
                        if (profiler != null) profiler.AddLog("RDP fixed its result so process should be repeated");
                        break;
                    }
                }

#if UNITY_EDITOR
                if (Debuger_K.doDebug && Debuger_K.debugOnlyNavMesh == false) {
                    if (profiler != null) profiler.AddLog("debug nodes");
                    Debuger_K.AddNodesAfterRDP(template.gridPosX, template.gridPosZ, template.properties, this);
                    if (profiler != null) profiler.AddLog("end debug nodes");
                }
#endif

                if (profiler != null) profiler.AddLog("start triangulating");
                GraphTriangulator triangulator = new GraphTriangulator(this, template);
                GraphCombiner combiner = new GraphCombiner(template, graph);
             
                triangulator.Triangulate(ref combiner, template);
                if (profiler != null) profiler.AddLog("end triangulating");

                ////////////
                if (profiler != null) profiler.AddLog("start reducing cell count");
                combiner.ReduceCells();                                            
                if (profiler != null) profiler.AddLog("end reducing cell count");  
                ////////////
                if (profiler != null) profiler.AddLog("start combine graph");
                combiner.CombineGraph();
                if (profiler != null) profiler.AddLog("end combine graph");
                ////////////

                if (profiler != null) profiler.AddLog("start setting graph edges");


                NodeEdgePair[] oData;
                IndexLengthInt[] oLayout;
                nodePairs.GetOptimizedData(out oData, out oLayout);

                for (int nodeID = 0; nodeID < nodesCreated; nodeID++) {
                    var node = nodesArray[nodeID];              
                    if (node.ID != INVALID_VALUE) {
                        if (node.GetFlag(NodeTempFlags.xMinusBorder) |
                            node.GetFlag(NodeTempFlags.xPlusBorder) |
                            node.GetFlag(NodeTempFlags.zMinusBorder) |
                            node.GetFlag(NodeTempFlags.zPlusBorder)) {

                            IndexLengthInt curLayout = oLayout[nodeID];

                            for (int layoutIndex = 0; layoutIndex < curLayout.length; layoutIndex++) {
                                NodeEdgePair pair = oData[curLayout.index + layoutIndex];
                                NodeEdgeValue edge = edgesArray[pair.index];
                                NodeStruct nextNode = nodesArray[edge.connection];

                                if (node.GetFlag(NodeTempFlags.xMinusBorder) & nextNode.GetFlag(NodeTempFlags.xMinusBorder))
                                    graph.SetBorderEdge(Directions.xMinus, new CellContentData(node.positionV3, nextNode.positionV3));
                                else if (node.GetFlag(NodeTempFlags.xPlusBorder) & nextNode.GetFlag(NodeTempFlags.xPlusBorder))
                                    graph.SetBorderEdge(Directions.xPlus, new CellContentData(node.positionV3, nextNode.positionV3));
                                else if (node.GetFlag(NodeTempFlags.zMinusBorder) & nextNode.GetFlag(NodeTempFlags.zMinusBorder))
                                    graph.SetBorderEdge(Directions.zMinus, new CellContentData(node.positionV3, nextNode.positionV3));
                                else if (node.GetFlag(NodeTempFlags.zPlusBorder) & nextNode.GetFlag(NodeTempFlags.zPlusBorder))
                                    graph.SetBorderEdge(Directions.zPlus, new CellContentData(node.positionV3, nextNode.positionV3));
                            }

                            
                        }
                    }
                }

                GenericPoolArray<NodeEdgePair>.ReturnToPool(ref oData);
                GenericPoolArray<IndexLengthInt>.ReturnToPool(ref oLayout);

                if (profiler != null) profiler.AddLog("end setting graph edges");

                if (template.canJump) {
                    if (profiler != null) profiler.AddLog("start adding jump portals to graph");
                    AddJumpSpots(graph);
                    if (profiler != null) profiler.AddLog("end adding jump portals");
                }
            }

            if (template.doCover) {
                if (profiler != null) profiler.AddLog("start adding covers to graph");
                AddCoversToGraph(graph);
                if (profiler != null) profiler.AddLog("end adding covers");
            }    

            //Debug.Log("Implement here connection of samples and Cells");
            //graph.battleGrid = volumeContainer.battleGrid;

            //generating simple map
            if (profiler != null) profiler.AddLog("start graph rasterization");
            graph.GenerateSimpleCellMap(PathFinder.CELL_GRID_SIZE);
            if (profiler != null) profiler.AddLog("end graph rasterization");

            //should be after generating simple map cause it uses its data
            if (template.doSamplePoints) {
                if (profiler != null) profiler.AddLog("agent sample. start adding points to graph");
                SetupSamples(graph);
                if (profiler != null) profiler.AddLog("end adding points");
            }

            ClearEdgesContainer();

            if (template.doCover) 
                ClearCoverGenerator();
            return graph;
        }

        void SetupKeyMarkers() {
            StackedList<int> nodeConnections = StackedList<int>.PoolTake(nodesSizeFlatten, 512);

            for (int i = 0; i < nodesCreated; i++) {
                nodesArray[i].SetFlag(NodeTempFlags.keyMarker, false);
            }

            for (int i = 0; i < edgesCreated; i++) {
                var curEdge = edgesArray[i];
                if (curEdge.ID != INVALID_VALUE) {
                    var nodeLeft = nodesArray[curEdge.nodeLeft];
                    var nodeRight = nodesArray[curEdge.nodeRight];

                    if (nodeLeft.ID != INVALID_VALUE & nodeRight.ID != INVALID_VALUE) {
                        nodeConnections.AddCheckDublicates(curEdge.nodeLeft, curEdge.nodeRight);
                        nodeConnections.AddCheckDublicates(curEdge.nodeRight, curEdge.nodeLeft);

                        if (nodeLeft.border == true & nodeRight.border == false) {
                            nodesArray[nodeLeft.ID].SetFlag(NodeTempFlags.keyMarker, true);

                            //Debuger_K.AddRay(nodeLeft.positionV3, Vector3.up, Color.red);
                            //Debuger_K.AddLabel(nodeLeft.positionV3 + Vector3.up, "Border");
                        }

                        if (nodeLeft.border == false & nodeRight.border == true) {
                            nodesArray[nodeRight.ID].SetFlag(NodeTempFlags.keyMarker, true);

                            //Debuger_K.AddRay(nodeRight.positionV3, Vector3.up, Color.red);
                            //Debuger_K.AddLabel(nodeRight.positionV3 + Vector3.up, "Border");
                        }
                    }
                }
            }

            for (int i = 0; i < nodesCreated; i++) {
                if (nodesArray[i].GetFlag(NodeTempFlags.corner)) {
                    nodesArray[i].SetFlag(NodeTempFlags.keyMarker, true);

                    //Debuger_K.AddRay(nodesArray[i].positionV3, Vector3.up, Color.red);
                    //Debuger_K.AddLabel(nodesArray[i].positionV3 + Vector3.up, "Corner");
                }
                else if (nodeConnections.Count(i) > 2) {
                    nodesArray[i].SetFlag(NodeTempFlags.keyMarker, true);

                    //Debuger_K.AddRay(nodesArray[i].positionV3, Vector3.up, Color.red);
                    //Debuger_K.AddLabel(nodesArray[i].positionV3 + Vector3.up, nodeConnections.Count(i));
                }
            }


            StackedList<int>.PoolReturn(ref nodeConnections);
        }


        void SetupSamples(Graph graph) {
            var samples = volumeContainer.voxelSamples;

            for (int i = 0; i < samples.Count; i++) {
                var sample = samples[i];
                var data = volumeContainer.GetData(sample.x, sample.z, sample.layer);
                var pointPos = volumeContainer.GetPos(sample.x, sample.z, data.y);

                //Debuger_K.AddLabel(pointPos, sample.layer);

                bool outside;
                Cell cell;                
                graph.GetCellSimpleMap(pointPos.x, pointPos.y, pointPos.z, sample.layer, out cell, out pointPos, out outside);

                if (cell != null) {
                    CellSamplePoint point = new CellSamplePoint(
                        pointPos,
                        sample.gridX,
                        sample.gridZ,
                        data.layer,
                        template.hashData.areaByIndex[data.area],
                        (Passability)data.pass);

                    cell.AddCellContentValue(point);
                }
                else
                    Debug.LogWarning("theoreticaly cell should not be null ");
            }
        }


        void SetupBorders() {
            //welp
            //i hate do manual indexes but since all grid are shifted there is only one way
            int borderStartX = template.extraOffset * 2 - 1;
            int borderStartZ = template.extraOffset * 2 - 1;

            int borderEndX = nodesSizeX - (template.extraOffset * 2) - 1;
            int borderEndZ = nodesSizeZ - (template.extraOffset * 2) - 1;

            //temporaty array to read data
            int[] tempArray = GenericPoolArray<int>.Take(1);
            int tempArrayLength;

            //first row
            //string formatZMinus = "x {0} z {1} (z-)";
            //string formatZPlus = "x {0} z {1} (z-)";
            //string formatXMinus = "x {0} z {1} (x-)";
            //string formatXPlus = "x {0} z {1} (x-)";
            for (int x = borderStartX; x < borderEndX; x++) {
                nodeList.Read(GetNodeIndex(x, borderStartZ), ref tempArray, out tempArrayLength);

                for (int i = 0; i < tempArrayLength; i++) {
                    nodesArray[tempArray[i]].SetFlag(NodeTempFlags.zMinusBorder, true);
                    //Debuger_K.AddLabel(nodesArray[tempArray[i]].positionV3, string.Format(formatZMinus, x, borderStartZ));
                }        

                nodeList.Read(GetNodeIndex(x, borderEndZ), ref tempArray, out tempArrayLength);
                for (int i = 0; i < tempArrayLength; i++) {
                    nodesArray[tempArray[i]].SetFlag(NodeTempFlags.zPlusBorder, true);
                    //Debuger_K.AddLabel(nodesArray[tempArray[i]].positionV3, string.Format(formatZPlus, x, borderEndZ));
                }
            }

            for (int z = borderStartZ; z < borderEndZ; z++) {
                nodeList.Read(GetNodeIndex(borderStartX, z), ref tempArray, out tempArrayLength);
                for (int i = 0; i < tempArrayLength; i++) {
                    nodesArray[tempArray[i]].SetFlag(NodeTempFlags.xMinusBorder, true);
                    //Debuger_K.AddLabel(nodesArray[tempArray[i]].positionV3, string.Format(formatXMinus, borderStartX, z));
                }

                nodeList.Read(GetNodeIndex(borderEndX, z), ref tempArray, out tempArrayLength);
                for (int i = 0; i < tempArrayLength; i++) {
                    nodesArray[tempArray[i]].SetFlag(NodeTempFlags.xPlusBorder, true);
                    //Debuger_K.AddLabel(nodesArray[tempArray[i]].positionV3, string.Format(formatXPlus, borderEndX, z));
                }
            }

            //second row
            //also it move second row to border since it cant be possible to have there another list on that position
            for (int x = borderStartX + 1; x < borderEndX - 1; x++) {
                int oldPositionIndex;

                oldPositionIndex = GetNodeIndex(x, borderStartZ + 1);
                nodeList.Read(oldPositionIndex, ref tempArray, out tempArrayLength);
                if (tempArrayLength > 0) {
                    Vector3 newPos = NodePos(new Vector2Int1Float(x, 0, borderStartZ));//with 0 height
                    nodeList.Swap(oldPositionIndex, GetNodeIndex(x, borderStartZ)); //swaping collums
                    for (int i = 0; i < tempArrayLength; i++) {
                        int nodeID = tempArray[i];         
                        nodesArray[nodeID].SetPosition(newPos.x, newPos.z);
                        nodesArray[nodeID].SetFlag(NodeTempFlags.zMinusBorder, true);
                        //Debuger_K.AddLabel(nodesArray[nodeID].positionV3, "z-");
                    }
                }

                oldPositionIndex = GetNodeIndex(x, borderEndZ - 1);
                nodeList.Read(oldPositionIndex, ref tempArray, out tempArrayLength);
                if (tempArrayLength > 0) {
                    Vector3 newPos = NodePos(new Vector2Int1Float(x, 0, borderEndZ));//with 0 height
                    nodeList.Swap(oldPositionIndex, GetNodeIndex(x, borderEndZ)); //swaping collums
                    for (int i = 0; i < tempArrayLength; i++) {
                        int nodeID = tempArray[i];
                        nodesArray[nodeID].SetPosition(newPos.x, newPos.z);
                        nodesArray[nodeID].SetFlag(NodeTempFlags.zPlusBorder, true);
                        //Debuger_K.AddLabel(nodesArray[nodeID].positionV3, "z+");
                    }
                }
            }

            for (int z = borderStartZ + 1; z < borderEndZ - 1; z++) {
                int oldPositionIndex;

                oldPositionIndex = GetNodeIndex(borderStartX + 1, z);
                nodeList.Read(oldPositionIndex, ref tempArray, out tempArrayLength);
                if (tempArrayLength > 0) {
                    Vector3 newPos = NodePos(new Vector2Int1Float(borderStartX, 0, z));//with 0 height
                    nodeList.Swap(oldPositionIndex, GetNodeIndex(borderStartX, z)); //swaping collums
                    for (int i = 0; i < tempArrayLength; i++) {
                        int nodeID = tempArray[i];
                        nodesArray[nodeID].SetPosition(newPos.x, newPos.z);
                        nodesArray[nodeID].SetFlag(NodeTempFlags.xMinusBorder, true);
                        //Debuger_K.AddLabel(nodesArray[nodeID].positionV3, "x-");
                    }
                }

                oldPositionIndex = GetNodeIndex(borderEndX - 1, z);
                nodeList.Read(oldPositionIndex, ref tempArray, out tempArrayLength);
                if (tempArrayLength > 0) {
                    Vector3 newPos = NodePos(new Vector2Int1Float(borderEndX, 0, z));//with 0 height
                    nodeList.Swap(oldPositionIndex, GetNodeIndex(borderEndX, z)); //swaping collums
                    for (int i = 0; i < tempArrayLength; i++) {
                        int nodeID = tempArray[i];
                        nodesArray[nodeID].SetPosition(newPos.x, newPos.z);
                        nodesArray[nodeID].SetFlag(NodeTempFlags.xPlusBorder, true);
                        //Debuger_K.AddLabel(nodesArray[nodeID].positionV3, "x+");
                    }
                }
            }

            //corners
            //X- Z+
            //Debug.Log(nodeList.Count(GetNodeIndex(borderStartX, borderEndZ - 1)) + "X- Z+");
            nodeList.Read(GetNodeIndex(borderStartX, borderEndZ - 1), ref tempArray, out tempArrayLength);
            for (int i = 0; i < tempArrayLength; i++) {
                int curNodeID = tempArray[i];    

                NodeEdgePair pair;
                if (nodePairs.First(curNodeID, out pair) == false)
                    Debug.LogError("Pathfinder: somehow target node dont have any edge while inserting corner");
                //since all connections on corners are clockwise we can take any edge
                int nextNodeID = edgesArray[pair.index].connection;       

                var curNode = nodesArray[curNodeID];
                var nextNode = nodesArray[nextNodeID];           

                int cornerNodeID = GetNodeNew(new Vector2Int1Float(borderStartX, (curNode.y + nextNode.y) * 0.5f, borderEndZ));

                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.xMinusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.zPlusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.corner, true);
                InsertNodeBetweenNew(curNodeID, nextNodeID, cornerNodeID);
            }

            //Z+ X+
            //Debug.Log(nodeList.Count(GetNodeIndex(borderEndX - 1, borderEndZ)) + "Z+ X+");
            nodeList.Read(GetNodeIndex(borderEndX - 1, borderEndZ), ref tempArray, out tempArrayLength);
            for (int i = 0; i < tempArrayLength; i++) {
                int curNodeID = tempArray[i];

                NodeEdgePair pair;
                if (nodePairs.First(curNodeID, out pair) == false)
                    Debug.LogError("Pathfinder: somehow target node dont have any edge while inserting corner");
                //since all connections on corners are clockwise we can take any edge
                int nextNodeID = edgesArray[pair.index].connection;

                var curNode = nodesArray[curNodeID];
                var nextNode = nodesArray[nextNodeID];
                int cornerNodeID = GetNodeNew(new Vector2Int1Float(borderEndX, (curNode.y + nextNode.y) * 0.5f, borderEndZ));

                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.zPlusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.xPlusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.corner, true);
                InsertNodeBetweenNew(curNodeID, nextNodeID, cornerNodeID);
            }

            //Z- X+
            //Debug.Log(nodeList.Count(GetNodeIndex(borderEndX, borderStartZ + 1)) + "Z- X+");
            nodeList.Read(GetNodeIndex(borderEndX, borderStartZ + 1), ref tempArray, out tempArrayLength);
            for (int i = 0; i < tempArrayLength; i++) {
                int curNodeID = tempArray[i];

                NodeEdgePair pair;
                if (nodePairs.First(curNodeID, out pair) == false)
                    Debug.LogError("Pathfinder: somehow target node dont have any edge while inserting corner");
                //since all connections on corners are clockwise we can take any edge
                int nextNodeID = edgesArray[pair.index].connection;

                var curNode = nodesArray[curNodeID];
                var nextNode = nodesArray[nextNodeID];
                int cornerNodeID = GetNodeNew(new Vector2Int1Float(borderEndX, (curNode.y + nextNode.y) * 0.5f, borderStartZ));

                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.zMinusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.xPlusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.corner, true);
                InsertNodeBetweenNew(curNodeID, nextNodeID, cornerNodeID);
            }

            //Z- X-
            //Debug.Log(nodeList.Count(GetNodeIndex(borderStartX + 1, borderStartZ)) + "Z- X-");
            nodeList.Read(GetNodeIndex(borderStartX + 1, borderStartZ), ref tempArray, out tempArrayLength);
            for (int i = 0; i < tempArrayLength; i++) {
                int curNodeID = tempArray[i];

                NodeEdgePair pair;
                if (nodePairs.First(curNodeID, out pair) == false)
                    Debug.LogError("Pathfinder: somehow target node dont have any edge while inserting corner");
                //since all connections on corners are clockwise we can take any edge
                int nextNodeID = edgesArray[pair.index].connection;

                var curNode = nodesArray[curNodeID];
                var nextNode = nodesArray[nextNodeID];
                int cornerNodeID = GetNodeNew(new Vector2Int1Float(borderStartX, (curNode.y + nextNode.y) * 0.5f, borderStartZ));

                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.xMinusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.zMinusBorder, true);
                nodesArray[cornerNodeID].SetFlag(NodeTempFlags.corner, true);
                InsertNodeBetweenNew(curNodeID, nextNodeID, cornerNodeID);
            }

            GenericPoolArray<int>.ReturnToPool(ref tempArray);
        }

        //generate segments, than loops to simplify them then fix them
        private void RamerDouglasPeuckerHullNew(float epsilon, out bool resultWasFixed, bool segments = true, bool loops = true, bool fixes = true) {
            resultWasFixed = false;

            //reset flags
            for (int i = 0; i < edgesCreated; i++) {
                var edge = edgesArray[i];
                if (edge.ID != INVALID_VALUE) {
                    edgesArray[i].SetFlag(EdgeTempFlags.DouglasPeukerMarker, false);
                }
            }

            int[] currentLine = GenericPoolArray<int>.Take(nodesCreated);
            int currentLineCount = 0;

            NodeEdgePair[] oData;
            IndexLengthInt[] oLayout;
            nodePairs.GetOptimizedData(out oData, out oLayout);

            #region segments
            if (segments) {
                for (int nodeID = 0; nodeID < nodesCreated; nodeID++) {
                    NodeStruct node = nodesArray[nodeID];

                    if (node.ID == INVALID_VALUE || node.GetFlag(NodeTempFlags.keyMarker) == false)
                        continue;

                    IndexLengthInt curLayout = oLayout[nodeID];

                    for (int i = 0; i < curLayout.length; i++) {
                        NodeEdgePair pair = oData[curLayout.index + i];
                        NodeEdgeValue edge = edgesArray[pair.index];
                        if (edge.ID != INVALID_VALUE) {
                            currentLineCount = 1;
                            currentLine[0] = nodeID;

                            int curNode = nodeID;

                            while (true) {
                                int targetEdgeIndex = GetEdgeIndexNew(curNode, edge.layer, edge.hash);
                                NodeEdgeValue targetEdge = edgesArray[targetEdgeIndex];
                                int targetNodeID = targetEdge.connection;
                                currentLine[currentLineCount++] = targetNodeID;
                                curNode = targetNodeID;

                                if (nodesArray[targetNodeID].GetFlag(NodeTempFlags.keyMarker) | targetEdge.GetFlag(EdgeTempFlags.DouglasPeukerMarker))
                                    break;
                            }

                            if (currentLineCount < 3) {
                                edgesArray[pair.index].SetFlag(EdgeTempFlags.DouglasPeukerMarker, true);
                            }
                            else {
                                SetupDouglasPeuckerHullNew(currentLine, currentLineCount, epsilon * 0.25f, epsilon);
                            }
                        }
                    }
                }
            }
            #endregion

            #region loops
            if (loops) {
                for (int nodeID = 0; nodeID < nodesCreated; nodeID++) {
                    NodeStruct node = nodesArray[nodeID];

                    if (node.ID == INVALID_VALUE)
                        continue;

                    IndexLengthInt curLayout = oLayout[nodeID];

                    for (int layoutIndex = 0; layoutIndex < curLayout.length; layoutIndex++) {
                        NodeEdgePair pair = oData[curLayout.index + layoutIndex];
                        NodeEdgeValue startEdge = edgesArray[pair.index];

                        if (startEdge.GetFlag(EdgeTempFlags.DouglasPeukerMarker) == false) {
                            currentLineCount = 0;
                            currentLine[currentLineCount++] = startEdge.origin;

                            NodeEdgeValue curEdge = startEdge;
                            for (int i = 0; i < edgesCreated; i++) {
                                NodeEdgeValue nextEdge = edgesArray[GetEdgeIndexNew(curEdge.connection, startEdge.layer, startEdge.hash)];
                                currentLine[currentLineCount++] = nextEdge.origin;

                                curEdge = nextEdge;
                                if (nextEdge.ID == startEdge.ID)
                                    break;
                            }

                            SetupDouglasPeuckerHullNew(currentLine, currentLineCount, epsilon * 0.25f, epsilon);
                        }
                    }       
                }
            }
            #endregion
            
            GenericPoolArray<int>.ReturnToPool(ref currentLine);

            #region fixing result
            if (fixes) {
                //since nodes after RDP can have loops around too small gaps there is thing to fix it.
                //cause other things even worse
                //i tried to rearange some code three times but this is work all time better so ther code are work around that

                int[] removedEdges = GenericPoolArray<int>.Take(edgesCreated);
                int removedEdgesCount = 0;
                for (int edgeIndex = 0; edgeIndex < edgesCreated; edgeIndex++) {
                    NodeEdgeValue startEdge = edgesArray[edgeIndex];

                    if (startEdge.ID == INVALID_VALUE ||
                        nodesArray[startEdge.nodeLeft].ID == INVALID_VALUE ||
                        nodesArray[startEdge.nodeRight].ID == INVALID_VALUE)
                        continue;

                    NodeEdgeValue curEdge = startEdge;

                    if (curEdge.nodeLeft == curEdge.nodeRight) {   //edge connected to itself
                        removedEdges[removedEdgesCount++] = curEdge.ID;
                    }
                    int nextEdgeIndex = GetEdgeIndexNew(curEdge.connection, curEdge.layer, curEdge.hash);
                    NodeEdgeValue nextEdge = edgesArray[nextEdgeIndex];

                    if (nextEdge.connection == curEdge.origin) {     //loop of 2 nodes
                        removedEdges[removedEdgesCount++] = curEdge.ID;
                        removedEdges[removedEdgesCount++] = nextEdge.ID;
                    }

                }

                if (removedEdgesCount > 0) {
                    for (int i = 0; i < removedEdgesCount; i++) {
                        RemoveEdgeNew(removedEdges[i]);
                    }
                    RemoveUnusedNodes();
                    resultWasFixed = true;
                }
                GenericPoolArray<int>.ReturnToPool(ref removedEdges);
            }
            #endregion

            GenericPoolArray<NodeEdgePair>.ReturnToPool(ref oData);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref oLayout);
        }

        //used for segments and then for loops
        private void SetupDouglasPeuckerHullNew(int[] targetNodeIDs, int targetNodeIDsCount, float firstEpsilon, float secondEpsilon) {
            Vector3[] vectors = GenericPoolArray<Vector3>.Take(targetNodeIDsCount);
            bool[] mask = GenericPoolArray<bool>.Take(targetNodeIDsCount, makeDefault: true);
            NodeEdgeValue[] AB = GenericPoolArray<NodeEdgeValue>.Take(nodeChunkSize);
            NodeEdgeValue[] BA = GenericPoolArray<NodeEdgeValue>.Take(nodeChunkSize);

            for (int i = 0; i < targetNodeIDsCount; i++) {
                vectors[i] = nodesArray[targetNodeIDs[i]].positionV3;
            }

            for (int i = 0; i < targetNodeIDsCount; i++) {
                mask[i] = true;
            }

            int nodeA = targetNodeIDs[0];
            int nodeB = targetNodeIDs[1];
            int ABcount, BAcount;
            GetConnectionsToNode(nodeA, nodeB, ref AB, out ABcount);
            GetConnectionsToNode(nodeB, nodeA, ref BA, out BAcount);
            
            //Debuger_K.AddLabel(vectors[0] + (Vector3.up * rdp_i * 0.1f), rdp_i);
            //if (rdp_i == 9)
            //    DebugDouglasPeucker(vectors, mask, targetNodeIDsCount, Color.red, rdp_i * 0.1f);

            DouglasPeucker(vectors, mask, targetNodeIDsCount, firstEpsilon * firstEpsilon);
            mask[0] = true;
            mask[targetNodeIDsCount - 1] = true;

            //if (rdp_i == 9)
            //    DebugDouglasPeucker(vectors, mask, targetNodeIDsCount, Color.green, (rdp_i * 0.1f) + 0.001f);     

            DouglasPeucker(vectors, mask, targetNodeIDsCount, secondEpsilon * secondEpsilon);
            mask[0] = true;
            mask[targetNodeIDsCount - 1] = true;

            //if (rdp_i == 9)
            //    DebugDouglasPeucker(vectors, mask, targetNodeIDsCount, Color.blue, (rdp_i * 0.1f) + 0.002f);


            //string s = "";
            //for (int i = 0; i < targetNodeIDsCount; i++) {
            //    Debuger_K.AddLabel(vectors[i] + (Vector3.up * i * 0.05f), i);
            //    if (mask[i]) {
            //        s += targetNodeIDs[i] + " ";
            //    }
            //}
            //Debug.Log(s);

            for (int i = 0; i < targetNodeIDsCount; i++) {
                if (mask[i] == false) {
                    nodesArray[targetNodeIDs[i]].ID = INVALID_VALUE;
                }
            }

            HashSet<VolumeArea> tempVolumeAreaCollection = GenericPool<HashSet<VolumeArea>>.Take();

            for (int currentStart = 0; currentStart < targetNodeIDsCount - 1; currentStart++) {
                if (mask[currentStart]) {
                    for (int currentEnd = currentStart + 1; currentEnd < targetNodeIDsCount; currentEnd++) {
                        if (mask[currentEnd]) {
                            for (int i = currentStart; i < currentEnd; i++) {
                                capturedVolumeAreaList.Read(targetNodeIDs[i], tempVolumeAreaCollection, false);
                            }

                            for (int i = 0; i < ABcount; i++) {
                                NodeEdgeValue item = AB[i];
                                int edgeIndex = SetEdgeNew(targetNodeIDs[currentStart], targetNodeIDs[currentEnd], item.layer, item.hash, null);

                                edgesArray[edgeIndex].SetFlag(EdgeTempFlags.DouglasPeukerMarker, true);

                                foreach (var VA in tempVolumeAreaCollection) {
                                    VA.AddEdge(edgesArray[edgeIndex]);
                                }
                            }

                            for (int i = 0; i < BAcount; i++) {
                                NodeEdgeValue item = BA[i];
                                int edgeIndex = SetEdgeNew(targetNodeIDs[currentEnd], targetNodeIDs[currentStart], item.layer, item.hash, null);

                                edgesArray[edgeIndex].SetFlag(EdgeTempFlags.DouglasPeukerMarker, true);

                                foreach (var VA in tempVolumeAreaCollection) {
                                    VA.AddEdge(edgesArray[edgeIndex]);
                                }
                            }
                            break;
                        }
                    }
                    tempVolumeAreaCollection.Clear();
                }
            }
            tempVolumeAreaCollection.Clear();
            GenericPool<HashSet<VolumeArea>>.ReturnToPool(ref tempVolumeAreaCollection);
            GenericPoolArray<Vector3>.ReturnToPool(ref vectors);
            GenericPoolArray<bool>.ReturnToPool(ref mask);
            GenericPoolArray<NodeEdgeValue>.ReturnToPool(ref AB);
            GenericPoolArray<NodeEdgeValue>.ReturnToPool(ref BA);
        }

#if UNITY_EDITOR
        private void DebugDouglasPeucker(Vector3[] points, bool[] mask, int pointsCount, Color color, float addOnTop = 0f) {
            for (int currentStart = 0; currentStart < pointsCount - 1; currentStart++) {
                if (mask[currentStart]) {
                    for (int currentEnd = currentStart + 1; currentEnd < pointsCount; currentEnd++) {
                        if (mask[currentEnd]) {
                            Debuger_K.AddLine(points[currentStart], points[currentEnd], color, addOnTop);
                            break;
                        }
                    }
                }
            }
        }
#endif

        //iterate over array points and set false to mask where points no longer exist
        private void DouglasPeucker(Vector3[] points, bool[] mask, int count, float sqrEsilon) {
            IndexLengthInt[] order = GenericPoolArray<IndexLengthInt>.Take(64);
            int orderCount = 0;
            order[orderCount++] = new IndexLengthInt(0, count - 1); //in this context length is last index. no offsets. just indexes
            
            while (true) {
                if (orderCount == 0)
                    break;

                IndexLengthInt cur = order[--orderCount];     
                Vector3 a = points[cur.index];
                Vector3 b = points[cur.length];           

                float sqrMaxDistance = 0f;
                int index = cur.index;

                if (SomeMath.SqrDistance(a, b) < 0.0001f) {   //this is search in loop causethisistooshortto beconsidered as line
                    for (int i = cur.index; i < cur.length + 1; i++) {
                        if (mask[i]) {
                            float sqrDistane = SomeMath.SqrDistance(a, points[i]);
                            if (sqrDistane > sqrMaxDistance) {
                                index = i;
                                sqrMaxDistance = sqrDistane;
                            }
                        }
                    }
                }
                else {
                    for (int i = cur.index; i < cur.length + 1; i++) {
                        if (mask[i]) {
                            Vector3 p = points[i];
                            float sqrDistane = SomeMath.SqrDistance(SomeMath.NearestPointToLine(a, b, p), p);

                            //Vector3 d = SomeMath.NearestPointToLine(a, b, p);
                            //Debuger_K.AddLine(p, d, Color.cyan);
                            //Debuger_K.AddLabel(d, SomeMath.SqrDistance(d, p));

                            if (sqrDistane > sqrMaxDistance) {
                                index = i;
                                sqrMaxDistance = sqrDistane;
                            }
                        }
                    }
                }

                if (sqrMaxDistance > sqrEsilon) {
                    order[orderCount++] = new IndexLengthInt(index, cur.length);
                    order[orderCount++] = new IndexLengthInt(cur.index, index);
                }
                else {
                    for (int i = cur.index + 1; i < cur.length; i++) {
                        mask[i] = false;
                    }
                }

                //Debug.Log(cur);
                //Debuger_K.AddDot(points[index], Color.red, size: 0.05f);
                //for (int i = cur.index; i < cur.length; i++) {
                //    Vector3 p = points[i];
                //    Debuger_K.AddRay(p, Vector3.up, mask[i] ? Color.green : Color.red);
                //    Debuger_K.AddLabel(p, i);
                //}

                //Debuger_K.AddLine(a, b, Color.yellow);
            }

            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref order);
        }


        private void AddJumpSpots(Graph graph) {
            if (volumeAreas != null) {
                int jumpSpotsCount = 0;
                for (int i = 0; i < volumeAreas.Count; i++) {
                    VolumeArea volumeArea = volumeAreas[i];
                    if (volumeArea.areaType == AreaType.Jump) {
                        foreach (var edge in volumeArea.edgesNew) {
                            volumeArea.cellContentDatas.Add(new CellContentData(
                                nodesArray[edge.nodeLeft].positionV3, 
                                nodesArray[edge.nodeRight].positionV3));
                        }
                        jumpSpotsCount++;
                    }
                }

                graph.InitPortalsArray(jumpSpotsCount);

                foreach (var area in volumeAreas) {
                    if (area.areaType == AreaType.Jump)
                        graph.AddPortal(area);
                }
            }
            else {
                Debug.LogWarning("jump spots are null");
            }
        }

        private Vector3 NodeLocalPosition(Vector2Int1Float pos) {
            return new Vector3(
                (pos.x * 0.5f * fragmentSize) - (fragmentSize * template.extraOffset),
                pos.y,
                (pos.z * 0.5f * fragmentSize) - (fragmentSize * template.extraOffset));
        }

        public Vector3 GetGraphRealPosition(int x, float y, int z) {
            return chunkRealPosition + new Vector3(x * 0.5f * fragmentSize, y, z * 0.5f * fragmentSize);
        }
        public Vector3 GetGraphRealPosition(Vector2Int1Float pos) {
            return GetGraphRealPosition(pos.x, pos.y, pos.z);
        }




        private static Vector3 SmallV3(float value) {
            return Vector3.up * value;
        }
    }
}