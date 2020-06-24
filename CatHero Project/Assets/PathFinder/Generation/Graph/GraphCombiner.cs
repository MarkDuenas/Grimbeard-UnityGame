using K_PathFinder.CoolTools;
using K_PathFinder.PFDebuger;
using K_PathFinder.Pool;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder.Graphs {


    //class dedicated to take data only about cells, edges and nodes and composte it to final graph
    public class GraphCombiner{
        public NavMeshTemplateCreation template;        
        Dictionary<CellContentData, GenerationEdgeInfo> edges = new Dictionary<CellContentData, GenerationEdgeInfo>();
        List<GenerationCellInfo> cells = new List<GenerationCellInfo>();
        Graph graph;
        public NavmeshProfiler profiler;

        public GraphCombiner(NavMeshTemplateCreation template, Graph graph) {
            this.graph = graph;
            this.template = template;
            this.profiler = template.profiler;
        }

        struct GeneratedConnection {
            public CellContentData data;
            public GenerationCellInfo connection;
            public GeneratedConnection(CellContentData Data, GenerationCellInfo Cell) {
                data = Data;
                connection = Cell;
            }
        }
        
        public void ReduceCells() {
            //Dictionary<Cell, GenerationCellInfo> cellDictionary = new Dictionary<Cell, GenerationCellInfo>();


            foreach (var item in cells) {
                item.CalculateAngles();
                //cellDictionary.Add(item.cell, item);
            }
            #region Debug
            //foreach (var item in cells) {
            //    foreach (var c in item.connections) {
            //        PFDebuger.Debuger_K.AddLine(c.from.centerVector3, c.connection.centerVector3);
            //    }
            //}

            //foreach (var item in cells) {
            //    foreach (var n in item.nodes) {
            //        PFDebuger.Debuger_K.AddLabel(n, item.Angle(n));
            //    }
            //}
            #endregion

            for (int simplificationIteration = 0; simplificationIteration < 50; simplificationIteration++) {
                bool anyChanges = false;

                for (int thisCellIndex = 0; thisCellIndex < cells.Count; thisCellIndex++) {
                    GenerationCellInfo thisCellInfo = cells[thisCellIndex];
                    //Cell thisCell = thisCellInfo.cell;
                    List<GeneratedConnection> connections = thisCellInfo.connections;

                    for (int connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++) {
                        GeneratedConnection connection = connections[connectionIndex];

                        //Cell otherCell = connection.connection;
                        GenerationCellInfo otherCellInfo = connection.connection;

                        //connect only of all important properties are equal
                        if (thisCellInfo.area != otherCellInfo.area ||
                            thisCellInfo.layer != otherCellInfo.layer ||
                            thisCellInfo.passability != otherCellInfo.passability)
                            continue;

                        var data = connection.data;
                        Vector3 left = data.leftV3;
                        Vector3 right = data.rightV3;

                        // merging cells
                        if ((thisCellInfo.Angle(left) + otherCellInfo.Angle(left)) < 180f &&
                            (thisCellInfo.Angle(right) + otherCellInfo.Angle(right)) < 180f) {

                            //collecting all edges
                            List<GenerationEdgeInfo> newEdges = new List<GenerationEdgeInfo>();
                            newEdges.AddRange(thisCellInfo.edges);
                            newEdges.AddRange(otherCellInfo.edges);

                            //Debuger_K.AddLine(connection.data, Color.red, addOnTop: 0.33f);

                            //we dont need edge where we connect cells so here we remove it
                            //GenerationEdgeInfo removeMe = GetEdge(connection.data);

                            for (int i = newEdges.Count - 1; i >= 0; i--) {
                                if (newEdges[i].data == connection.data)
                                    newEdges.RemoveAt(i);
                            }

                            HashSet<Vector3> newNodes = new HashSet<Vector3>();
                            newNodes.UnionWith(thisCellInfo.nodes);
                            newNodes.UnionWith(otherCellInfo.nodes);

                            //thisCell.RemoveAllConnections(otherCell);
                            //otherCell.RemoveAllConnections(thisCell);

                            //List<CellContentGenericConnection> curConnectionsList, otherConnectionsList;
                            //curConnectionsList = thisCellInfo.connections;
                            //for (int curIndex = 0; curIndex < curConnectionsList.Count; curIndex++) {
                            //    otherConnectionsList = cellDictionary[curConnectionsList[curIndex].connection].connections;
                            //    for (int otherIndex = otherConnectionsList.Count - 1; otherIndex >= 0; otherIndex--) {
                            //        if (otherConnectionsList[otherIndex].connection == thisCell) {
                            //            otherConnectionsList[otherIndex].ReturnToPool();
                            //            otherConnectionsList.RemoveAt(otherIndex);
                            //        }
                            //    }
                            //}


                            //curConnectionsList = otherCellInfo.connections;
                            //for (int curIndex = 0; curIndex < curConnectionsList.Count; curIndex++) {
                            //    otherConnectionsList = cellDictionary[curConnectionsList[curIndex].connection].connections;
                            //    for (int otherIndex = otherConnectionsList.Count - 1; otherIndex >= 0; otherIndex--) {
                            //        if (otherConnectionsList[otherIndex].connection == otherCell) {
                            //            otherConnectionsList[otherIndex].ReturnToPool();
                            //            otherConnectionsList.RemoveAt(otherIndex);
                            //        }
                            //    }
                            //}

                            //thisCellInfo.connections.ForEach(con => cellsDic[con.connection].connections.RemoveAll(nb => nb.connection == thisCell));
                            //otherCellInfo.connections.ForEach(con => cellsDic[con.connection].connections.RemoveAll(nb => nb.connection == otherCell));

                            thisCellInfo.RemoveItselfFromNeigbours();
                            otherCellInfo.RemoveItselfFromNeigbours();

                            cells.Remove(thisCellInfo);
                            cells.Remove(otherCellInfo);
                            //cellDictionary.Remove(thisCell);
                            //cellDictionary.Remove(otherCell);

                            GenerationCellInfo newInfo = new GenerationCellInfo(thisCellInfo.area, thisCellInfo.passability, thisCellInfo.layer, newNodes, newEdges);
                            newInfo.CalculateAngles();                       
                            cells.Add(newInfo);

                            foreach (var edge in newEdges) {
                                edge.SetCellToEdge(newInfo);
                            }

                            anyChanges = true;
                            break;
                        }
                    }

                    if (anyChanges)
                        break;
                }

                if (!anyChanges)
                    break;
            }
        }

        //public void DebugMe() {
        //    foreach (var cell in cells) {
        //        foreach (var edge in cell.edges) {
        //            PFDebuger.Debuger_K.AddLine(cell.centerV3, SomeMath.MidPoint(edge.data.leftV3, edge.data.centerV3), Color.blue);
        //            PFDebuger.Debuger_K.AddLine(cell.centerV3, SomeMath.MidPoint(edge.data.rightV3, edge.data.centerV3), Color.red);
        //        }
        //    }
        //}

        public void CombineGraph() {
            StackedListWithKeys<CellContentData, CellConnection> totalMap = StackedListWithKeys<CellContentData, CellConnection>.PoolTake(cells.Count, cells.Count * 4);
            Cell[] cellArray = GenericPoolArray<Cell>.Take(cells.Count);
                        
            for (int i = 0; i < cells.Count; i++) {
                //cell bodies
                GenerationCellInfo rawCell = cells[i];
                rawCell.index = i;
                Cell cell = new Cell(rawCell.area, rawCell.passability, rawCell.layer, graph);
                cell.graphID = i;
                cell.globalID = PathFinderData.GetFreeCellID();
                cell.SetCenter(rawCell.centerV3);
                cellArray[i] = cell;
            }
                        
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++) {
                GenerationCellInfo rawCell = cells[cellIndex];
                Cell cell = cellArray[cellIndex];
                Vector3 cellCenter = cell.centerVector3;

                var rawCellEdges = rawCell.originalEdges;
                var rawCellConnections = rawCell.connections;

                CellContentData[] originalEdges = GenericPoolArray<CellContentData>.Take(rawCellEdges.Count);

                //int totalMapCounterStart = totalMapCounter;
                
                for (int e = 0; e < rawCellEdges.Count; e++) {
                    CellContentData edge = rawCellEdges[e];
                    if (SomeMath.V2Cross(cellCenter.x - edge.xLeft, cellCenter.z - edge.zLeft, edge.xRight - edge.xLeft, edge.zRight - edge.zLeft) > 0f)
                        edge.SwapEdges();
                    originalEdges[e] = edge;

                    if (totalMap.AddKey(cellIndex, edge) == false)
                        Debug.LogError("dublicate edge in navmesh generation. this theoreticaly should not happen but it is");
                }
                
                for (int c = 0; c < rawCellConnections.Count; c++) {
                    GeneratedConnection curConnection = rawCellConnections[c];
                    CellContentData data = curConnection.data;
            
                    Vector3 intersection;
                    SomeMath.ClampedRayIntersectXZ(rawCell.centerV3, curConnection.connection.centerV3 - rawCell.centerV3, data.leftV3, data.rightV3, out intersection);
                    float fromCost = Vector3.Distance(rawCell.centerV3, intersection) * rawCell.area.cost;
                    float toCost = Vector3.Distance(curConnection.connection.centerV3, intersection) * curConnection.connection.area.cost;

                    CellConnection connection = cell.AddConnectionGeneric(data, cellArray[curConnection.connection.index], fromCost, toCost, intersection);
                    if(totalMap.SetValue(cellIndex, data, connection) == false)
                        Debug.LogError("added connection dont have relative edge in cell");
                }
                cell.originalEdges = originalEdges;
                cell.originalEdgesCount = rawCellEdges.Count;
            }

            CellContentData[] totalMapData;
            CellConnection[] totalMapValue;
            IndexLengthInt[] totalMapLayout;
            totalMap.GetOptimizedData(out totalMapData, out totalMapValue, out totalMapLayout);
            StackedListWithKeys<CellContentData, CellConnection>.PoolReturn(ref totalMap);

            if (profiler != null) profiler.AddLog("Setting bunch of data to graph");

            graph.SetGraphData(cellArray, cells.Count, totalMapData, totalMapValue, totalMapLayout);
            
            GenericPoolArray<Cell>.ReturnToPool(ref cellArray);
            GenericPoolArray<CellContentData>.ReturnToPool(ref totalMapData);
            GenericPoolArray<CellConnection>.ReturnToPool(ref totalMapValue);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref totalMapLayout);
        }
        
        public bool AddCell(List<Vector3> nodes, Area area, Passability passability, int layer) {
            CellContentData d = new CellContentData(nodes[0], nodes[1]);

            if (GetEdge(d).CellsContains(nodes) == false) {
                List<GenerationEdgeInfo> edges = new List<GenerationEdgeInfo>();

                for (int i = 0; i < nodes.Count - 1; i++) {
                    edges.Add(GetEdge(new CellContentData(nodes[i], nodes[i + 1])));
                }

                edges.Add(GetEdge(new CellContentData(nodes[nodes.Count - 1], nodes[0])));
                

                GenerationCellInfo newCell = new GenerationCellInfo(area, passability, layer, nodes, edges);
                cells.Add(newCell);

                foreach (var edge in edges) {
                    edge.SetCellToEdge(newCell);
                }
                return true;
            }
            else
                return false;
        }
        
        GenerationEdgeInfo GetEdge(CellContentData d) {
            GenerationEdgeInfo result;
            if (edges.TryGetValue(d, out result) == false) {
                result = new GenerationEdgeInfo(d);
                edges.Add(d, result);
            }
            return result;
        }

        public AgentProperties properties {
            get { return template.properties; }
        }

        class GenerationEdgeInfo {
            public CellContentData data;
            public GenerationCellInfo U_Cell, D_Cell; //up, down

            public GenerationEdgeInfo(CellContentData data) {
                this.data = data;
            }

            public bool CellsContains(IEnumerable<Vector3> input) {
                return (U_Cell != null && U_Cell.nodes.SetEquals(input)) | (D_Cell != null && D_Cell.nodes.SetEquals(input));
            }

            public void SetCellToEdge(GenerationCellInfo cell) {
                float dirX = data.xLeft - data.xRight;
                float dirZ = data.zLeft - data.zRight;

                Vector3 center = cell.centerV3;

                float dirCenterX = center.x - data.xRight;
                float dirCenterZ = center.z - data.zRight;

                if (SomeMath.V2Cross(dirX, dirZ, dirCenterX, dirCenterZ) > 0) 
                    U_Cell = cell;
                else 
                    D_Cell = cell;
                

                if (U_Cell != null & D_Cell != null) {
                    D_Cell.connections.Add(new GeneratedConnection(new CellContentData(data.leftV3, data.rightV3), U_Cell));
                    U_Cell.connections.Add(new GeneratedConnection(new CellContentData(data.rightV3, data.leftV3), D_Cell));

                    //Vector3 intersection;
                    //SomeMath.ClampedRayIntersectXZ(U_Cell.centerV3, D_Cell.centerV3 - U_Cell.centerV3, data.leftV3, data.rightV3, out intersection);
                    //float upCellCost = Vector3.Distance(U_Cell.centerV3, intersection) * U_Cell.area.cost;
                    //float downCellCost = Vector3.Distance(D_Cell.centerV3, intersection) * D_Cell.area.cost;
                    //D_Cell.connections.Add(CellContentGenericConnection.GetFromPool(new CellContentData(data.leftV3, data.rightV3), D_Cell.cell, U_Cell.cell, downCellCost, upCellCost, intersection));
                    //U_Cell.connections.Add(CellContentGenericConnection.GetFromPool(new CellContentData(data.rightV3, data.leftV3), U_Cell.cell, D_Cell.cell, upCellCost, downCellCost, intersection));
                }
            }
        }

        class GenerationCellInfo {
            public int index;

            //public Cell cell;

            public Area area;
            public Passability passability;
            public int layer;
            public Vector3 centerV3;
            public Vector2 centerV2;

            public HashSet<Vector3> nodes;
            public List<GenerationEdgeInfo> edges;
            //public List<CellContentGenericConnection> connections = new List<CellContentGenericConnection>();
            Dictionary<Vector3, float> angles = new Dictionary<Vector3, float>();
            public List<CellContentData> originalEdges = new List<CellContentData>();
            public List<GeneratedConnection> connections = new List<GeneratedConnection>();

            public GenerationCellInfo(Area area, Passability passability, int layer, IEnumerable<Vector3> nodes, List<GenerationEdgeInfo> edges) {      
                for (int i = 0; i < edges.Count; i++) {
                    originalEdges.Add(edges[i].data);
                }

                //cell = new Cell(area, passability, layer, graph);

                this.area = area;
                this.passability = passability;
                this.layer = layer;

                this.nodes = new HashSet<Vector3>(nodes);
                this.edges = edges;

                Vector3 cellCenter = SomeMath.MidPoint(nodes);

                //note: edges.Count > 3
                //cause center for triangle is easy and center for everything else is not
                if (edges.Count > 3){
                    Dictionary<GenerationEdgeInfo, float> triangleArea = new Dictionary<GenerationEdgeInfo, float>();
                    Dictionary<GenerationEdgeInfo, Vector3> centers = new Dictionary<GenerationEdgeInfo, Vector3>();

                    float areaSum = 0;
                    foreach (var item in edges) {
                        Vector3 curTriangleCenter = SomeMath.MidPoint(cellCenter, item.data.leftV3, item.data.rightV3);
                        centers.Add(item, curTriangleCenter);
                        float curArea = Vector3.Cross(item.data.leftV3 - curTriangleCenter, item.data.rightV3 - curTriangleCenter).magnitude * 0.5f;
                        areaSum += curArea;
                        triangleArea.Add(item, curArea);
                    }

                    Vector3 actualCenter = Vector3.zero;

                    foreach (var item in edges) {
                        actualCenter += (centers[item] * (triangleArea[item] / areaSum));
                    }

                    cellCenter = actualCenter;
                } 

                centerV3 = cellCenter;
                centerV2 = new Vector2(cellCenter.x, cellCenter.z);
                //cell.SetCenter(cellCenter);
            }

            public void RemoveItselfFromNeigbours() {
                foreach (var con in connections) {
                    var neighbourConnections = con.connection.connections;

                    for (int i = neighbourConnections.Count - 1; i >= 0; i--) {
                        if (neighbourConnections[i].connection == this)
                            neighbourConnections.RemoveAt(i);
                    }
                }
            }

            //public Vector3 centerV3 {
            //    get { return cell.centerVector3; }
            //}
            //public Vector2 centerV2 {
            //    get { return cell.centerVector2; }
            //}

            //public void SetConnection(CellContentData data, Cell connectTo, float costFrom, float costTo, Vector3 intersection) {
            //    connections.Add(CellContentGenericConnection.GetFromPool(data, cell, connectTo, costFrom, costTo, intersection));
            //}
                 
            public void CalculateAngles() {
                foreach (var node in nodes) {
                    CellContentData? data1 = null, data2 = null;
                    bool data1Full = false;

                    foreach (var edge in edges) {
                        CellContentData curData = edge.data;
                        if (curData.Contains(node)) {
                            if (!data1Full) {
                                data1 = curData;
                                data1Full = true;
                            }
                            else {
                                data2 = curData;
                                break;//bouth data full
                            }
                        }
                    }               
                
                    Vector3 a = data1.Value.leftV3 == node ? data1.Value.rightV3 : data1.Value.leftV3;
                    Vector3 b = data2.Value.leftV3 == node ? data2.Value.rightV3 : data2.Value.leftV3;
                    
                    angles[node] = Vector2.Angle(new Vector2(a.x - node.x, a.z - node.z), new Vector2(b.x - node.x, b.z - node.z));

                    //Vector3 u = new Vector3(0, 0.1f, 0);
                    //Debuger_K.AddDot(node, Color.red);
                    //Debuger_K.AddLine(node + u, a, Color.blue);
                    //Debuger_K.AddLine(node + u, b, Color.red);
                    //Debuger_K.AddLabel(node, angles[node]);
                }
            }  

            public float Angle(Vector3 node) {
                return angles[node];
            }
        }
    }
}