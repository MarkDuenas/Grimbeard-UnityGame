using UnityEngine;
using System.Collections.Generic;
using K_PathFinder.CoverNamespace;
using K_PathFinder.Pool;

namespace K_PathFinder.Graphs {
    //convex mesh with some additional data
    public class Cell {
        public int globalID, graphID;
        public int layer { get; private set; }//cell navmesh layer (NOT BITMASK LAYER) this value is used in graph reconstruction. so all navmesh can sepparated into 2d sheets anytime
        public Area area { get; private set; }//cell area reference
        public Passability passability { get; private set; }
        public int bitMaskLayer;

        public bool advancedAreaCell;
        public List<CellPathContentAbstract> pathContent = null;
      
        public Graph graph { get; private set; }//graph it's belong. dont need outside generation but still can have some uses      
        public Vector3 centerVector3 { get; private set; }  //cell center are center of it's mesh area

        //public List<CellConnection> connections = new List<CellConnection>();

        public int connectionsCount;
        public CellConnection[] connections;
        public CellContentData[] connectionsDatas;

        public List<ICellContentValue> cellContentValues = new List<ICellContentValue>(); //dont use it outside reading 

        //original edges of this particular cell. right now it's here cause it's handy to have ell edges to describecell inside itself. later on should be moved to graph
        public CellContentData[] originalEdges;
        public int originalEdgesCount;

        public CellContentRaycastData[] raycastData;
        public int raycastDataCount;

        //public Cell(Area area, Passability passability, int layer, Graph graph, IEnumerable<CellContentData> originalEdges) {
        //    this.area = area;
        //    this.passability = passability;
        //    this.layer = layer;
        //    this.graph = graph;
        //    //this.originalEdges.AddRange(originalEdges);

        //    foreach (var oe in originalEdges) {
        //        cellDataDictionary.Add(oe, null);
        //    }

        //    advancedAreaCell = area is AreaAdvanced;
        //    if (advancedAreaCell) {
        //        pathContent = new List<CellPathContentAbstract>();
        //    }
        //}
        
        public Cell(Area area, Passability passability, int layer, Graph graph) {
            this.area = area;
            this.passability = passability;
            this.layer = layer;
            this.graph = graph;

            advancedAreaCell = area is AreaAdvanced;
            if (advancedAreaCell) 
                pathContent = new List<CellPathContentAbstract>();

            connectionsCount = 0;
            connections = GenericPoolArray<CellConnection>.Take(1);
            connectionsDatas = GenericPoolArray<CellContentData>.Take(1);
        }

        public void OnGraphDestruction() {
            lock (this) {
                if (advancedAreaCell) {
                    (area as AreaAdvanced).RemoveCell(this);
                }

                connectionsCount = 0;
                GenericPoolArray<CellConnection>.ReturnToPool(ref connections);
                GenericPoolArray<CellContentData>.ReturnToPool(ref connectionsDatas);

                //connections.Clear();      
                cellContentValues.Clear();          

                graph = null;
                area = null;
                if (pathContent != null) {
                    pathContent.Clear();
                    pathContent = null;
                }

                bitMaskLayer = 1; //default ignore layer

                GenericPoolArray<CellContentData>.ReturnToPool(ref originalEdges);
                originalEdgesCount = 0;

                GenericPoolArray<CellContentRaycastData>.ReturnToPool(ref raycastData);
                raycastDataCount = 0;
            }
        }

        public void OnGraphGenerationEnd() {
            lock (this) {
                if (advancedAreaCell) {
                    IEnumerable<CellPathContentAbstract> content;
                    (area as AreaAdvanced).AddCell(this, out content, out bitMaskLayer);
                    lock (content) {
                        pathContent.AddRange(content);
                    }
                }
                else {
                    bitMaskLayer = 0;
                }              
            }
        }

        public CellConnection AddConnectionGeneric(CellContentData cellData, Cell connection, float costFrom, float costTo, Vector3 intersection) {
            CellConnection cc = new CellConnection(globalID, connection.globalID, costFrom, costTo, intersection, passability, connection.passability);
            cc.cellInternalIndex = connectionsCount;

            if (connections.Length == connectionsCount) {
                GenericPoolArray<CellConnection>.IncreaseSize(ref connections);
                GenericPoolArray<CellContentData>.IncreaseSize(ref connectionsDatas);
            }
            connections[connectionsCount] = cc;
            connectionsDatas[connectionsCount] = cellData;
            connectionsCount++;
            return cc;
        }
        
        public CellConnection AddConnectionJump(Vector3 EnterPoint, Vector3 ExitPoint, Vector3 Axis, ConnectionJumpState JumpState, Cell connection) {
            float costFrom = Vector3.Distance(centerVector3, EnterPoint);
            float costTo = Vector3.Distance(ExitPoint, connection.centerVector3);
            CellConnection cc = new CellConnection(Axis, JumpState, globalID, connection.globalID, costFrom, costTo, passability, connection.passability);
            cc.cellInternalIndex = connectionsCount;

            if (connections.Length == connectionsCount) {
                GenericPoolArray<CellConnection>.IncreaseSize(ref connections);
                GenericPoolArray<CellContentData>.IncreaseSize(ref connectionsDatas);
            }
            connections[connectionsCount] = cc;
            connectionsDatas[connectionsCount] = new CellContentData(EnterPoint, ExitPoint);

            connectionsCount++;
            return cc;
        }

        public CellConnection AddConnectionDeserialized(CellConnection connection, CellContentData data) {
            connection.cellInternalIndex = connectionsCount;
            if (connections.Length == connectionsCount) {
                GenericPoolArray<CellConnection>.IncreaseSize(ref connections);
                GenericPoolArray<CellContentData>.IncreaseSize(ref connectionsDatas);
            }
            connections[connectionsCount] = connection;
            connectionsDatas[connectionsCount] = data;
            connectionsCount++;
            return connection;
        }

        public void RemoveConnection(CellConnection connection) {
            int index = -1;
            for (int i = 0; i < connectionsCount; i++) {
                if(connections[i] == connection) {
                    index = i;
                    break;
                }
            }

            if(index == -1) {
                Debug.LogError("PathFinder: Cell dont have target connection to remove");
            }

            for (int i = index; i < connectionsCount - 1; i++) {
                connections[i] = connections[i + 1];
                connections[i].cellInternalIndex = i;
                connectionsDatas[i] = connectionsDatas[i + 1];           

            }

            connectionsCount--;      
        }


        public void SetBitMaskLayer(int value) {
            bitMaskLayer = value;
        }
        
        #region Cell Path Content
        public void AddPathContent(CellPathContentAbstract content) {
            lock (this) {
                pathContent.Add(content);
                content.OnAddingToCell(this);
            }
        }

        public void AddPathContent(IEnumerable<CellPathContentAbstract> content) {
            lock (this) {
                pathContent.AddRange(content);
                foreach (var item in content) {
                    item.OnAddingToCell(this);
                }
            }
        }

        public void RemovePathContent(CellPathContentAbstract content) {
            lock (this) {
                pathContent.Remove(content);
                content.OnRemovingFromCell(this);
            }
        }

        public void RemovePathContent(IEnumerable<CellPathContentAbstract> content) {
            lock (this) {
                foreach (var item in content) {
                    pathContent.Remove(item);
                    item.OnRemovingFromCell(this);
                }
            }
        }
        #endregion
        
        public void SetCenter(Vector3 center) {
            centerVector3 = center;
        }

        public void AddCellContentValue(ICellContentValue value) {
            cellContentValues.Add(value);
        }

        public void RemoveCellContentValue(ICellContentValue value) {
            cellContentValues.Add(value);
        }
        
        #region data
        //public void RemoveAllConnections(Cell target) {         
        //    for (int i = connections.Count - 1; i >= 0; i--) {
        //        if (connections[i].connection == target) {
        //            CellContent connection = connections[i];
        //            CellContentData data = connection.cellData;                
        //            connections.RemoveAt(i);
                   
        //            //if edge was presented before we add this connection then it will remain in dictionary
        //            //else we remove it
        //            if (originalEdges.Contains(data)) {
        //                cellDataDictionary[data] = null;
        //            }
        //            else {
        //                cellDataDictionary.Remove(data);
        //            }
               
        //            connection.ReturnToPool();
        //        }
        //    }
        //}
        //public void TryAddData(CellContentData d) {
        //    if (!cellDataDictionary.ContainsKey(d))
        //        cellDataDictionary.Add(d, null);
        //}
 
        //public void AddConnection(CellContent cc) {
        //    //cellDataDictionary.Remove(cc.cellData); //make sure sides are on correct side
        //    //cellDataDictionary.Add(cc.cellData, cc);
        //    connections.Add(cc);
        //}

        #endregion

        //#region closest
        ///// <summary>
        ///// check all cell triangles and return Y position if point inside of any triangle
        ///// </summary>
        ///// <param name="y">result Y if point inside cell</param>
        ///// <returns>true if point inside cell</returns>
        //public bool GetPointInsideCell(float x, float z, out float y) {
        //    foreach (var edge in originalEdges) {
        //        if (SomeMath.PointInTriangle(centerVector2.x, centerVector2.y, edge.xLeft, edge.zLeft, edge.xRight, edge.zRight, x, z)) {
        //            y = SomeMath.CalculateHeight(edge.leftV3, edge.rightV3, centerVector3, x, z);
        //            return true;
        //        }
        //    }

        //    y = 0;
        //    return false;
        //}  


        public void ResetNeighbourRaycastData() {
            Cell[] globalCells = PathFinderData.cells;
            for (int connectionIndex = 0; connectionIndex < connectionsCount; connectionIndex++) {
                CellConnection con = connections[connectionIndex];
                if (con.type != CellConnectionType.Invalid && con.connection != -1) {
                    Cell c = globalCells[con.connection];
                    c.graph.ResetRaycastData(c.graphID);
                }
            }
        }



        public void GetClosestPointOnHull(float targetX, float targetZ, out Vector3 closest, out float sqrDistance) {
            sqrDistance = float.MaxValue;
            closest = Vector3.zero;

            for (int i = 0; i < originalEdgesCount; i++) {
                CellContentData edge = originalEdges[i];
                Vector3 curInte = edge.NearestPointXZ(targetX, targetZ);
                float curSqrDist = SomeMath.SqrDistance(targetX, targetZ, curInte.x, curInte.z);

                if (curSqrDist < sqrDistance) {
                    sqrDistance = curSqrDist;
                    closest = curInte;
                }
            }
        }

        public void GetClosestPointToCell(float targetX, float targetZ, out Vector3 closestPoint, out bool isOutsideCell) {
            float closestSqrDistance = float.MaxValue;
            closestPoint.x = targetX;
            closestPoint.y = 0f;
            closestPoint.z = targetZ;


            for (int i = 0; i < originalEdgesCount; i++) {
                CellContentData edge = originalEdges[i];

                if (SomeMath.PointInTriangle(edge.xLeft, edge.zLeft, edge.xRight, edge.zRight, centerVector3.x, centerVector3.z, targetX, targetZ)) {
                    closestPoint.y = SomeMath.CalculateHeight(edge.leftV3, edge.rightV3, centerVector3, targetX, targetZ); 
                    isOutsideCell = false;
                    return;
                }
            }


            for (int i = 0; i < originalEdgesCount; i++) {
                CellContentData edge = originalEdges[i];

                Vector3 curClosest = SomeMath.ClosestToSegmentTopProjection(edge.leftV3, edge.rightV3, new Vector2(targetX, targetZ));
                float curSqrDist = SomeMath.SqrDistance(targetX, targetZ, curClosest.x, curClosest.z);

                if (curSqrDist < closestSqrDistance) {
                    closestSqrDistance = curSqrDist;
                    closestPoint = curClosest;
                }
            }
            

            isOutsideCell = true;
            return;
        }


        ////Vector2

        ////vector3
        //public void GetClosestPointToCell(Vector3 targetPos, out Vector3 closestPoint, out bool isOutsideCell) {
        //    GetClosestPointToCell(new Vector2(targetPos.x, targetPos.z), out closestPoint, out isOutsideCell);
        //}     
        //#endregion
    }
}
