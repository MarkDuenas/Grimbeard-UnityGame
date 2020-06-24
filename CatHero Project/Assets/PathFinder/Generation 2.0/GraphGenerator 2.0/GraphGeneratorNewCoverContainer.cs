using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using K_PathFinder.Pool;
using K_PathFinder.VectorInt;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace K_PathFinder.GraphGeneration {
    public partial class GraphGeneratorNew {
        struct NodeCoverStruct {
            public int ID, 
                connection, layer, size, coverPointsStart, coverPointsCount; //connection data
            public float x, y, z;
            public bool RPD_flag;

            public NodeCoverStruct(int id, float X, float Y, float Z) {
                ID = id;
                x = X;
                y = Y;
                z = Z;
                connection = size = layer = coverPointsStart = coverPointsCount = INVALID_VALUE;
                RPD_flag = false;
            }

            public void SetConnection(int Connection, int Layer, int Size) {
                connection = Connection;
                layer = Layer;
                size = Size;
            }
            
            public Vector3 positionV3 {
                get { return new Vector3(x, y, z); }
            }
            public Vector2 positionV2 {
                get { return new Vector2(x, z); }
            }

            public override int GetHashCode() {
                return ID;
            }
            
            public override bool Equals(object obj) {
                if(obj is NodeCoverStruct == false)
                    return false;

                return ID == ((NodeCoverStruct)obj).ID;
            }
        }
        
        Dictionary<Vector3, int> coverNodeDictionary;
        NodeCoverStruct[] coverNodesArray;
        int coverNodesCreated = 0;

        VolumeArea[] coverSpotsArray;
        int coverSpotsArrayCount = 0;

        void InitCoverGenerator() {
            coverNodesArray = GenericPoolArray<NodeCoverStruct>.Take(32);
            coverNodeDictionary = GenericPool<Dictionary<Vector3, int>>.Take();
            coverSpotsArray = GenericPoolArray<VolumeArea>.Take(32);    
        }

        void ClearCoverGenerator() {
            GenericPoolArray<NodeCoverStruct>.ReturnToPool(ref coverNodesArray);
            coverNodesCreated = 0;
            coverNodeDictionary.Clear();
            GenericPool<Dictionary<Vector3, int>>.ReturnToPool(ref coverNodeDictionary);
            GenericPoolArray<VolumeArea>.ReturnToPool(ref coverSpotsArray);
            coverSpotsArrayCount = 0;
        }

        void SetCoverEdgeNew(Vector2Int1Float left, Vector2Int1Float right, int layer, int size) {
            if (size == 0)
                return;

            Vector3 pos = NodePos(left);
            int nodeLeft;
            if (coverNodeDictionary.TryGetValue(pos, out nodeLeft) == false) {
                nodeLeft = coverNodesCreated;
                NodeCoverStruct newNode = new NodeCoverStruct(nodeLeft, pos.x, pos.y, pos.z);
   
                if (coverNodesArray.Length <= nodeLeft)
                    GenericPoolArray<NodeCoverStruct>.IncreaseSizeTo(ref coverNodesArray, nodeLeft + 1);

                coverNodesArray[nodeLeft] = newNode;
                coverNodesCreated++;
                coverNodeDictionary.Add(pos, nodeLeft);
            }

            pos = NodePos(right);
            int nodeRight;
            if (coverNodeDictionary.TryGetValue(pos, out nodeRight) == false) {
                nodeRight = coverNodesCreated;
                NodeCoverStruct newNode = new NodeCoverStruct(nodeRight, pos.x, pos.y, pos.z);

                if (coverNodesArray.Length <= nodeRight)
                    GenericPoolArray<NodeCoverStruct>.IncreaseSizeTo(ref coverNodesArray, nodeRight + 1);

                coverNodesArray[nodeRight] = newNode;
                coverNodesCreated++;
                coverNodeDictionary.Add(pos, nodeRight);
            }

            coverNodesArray[nodeLeft].SetConnection(nodeRight, layer, size);
        }
        
        //generate segments, than loops to simplify them
        private void RamerDouglasPeuckerCoverNew(float epsilonFirst, float epsilonSecond) {
            bool[] dpWasHere = GenericPoolArray<bool>.Take(coverNodesCreated, makeDefault: true); //where RDP already as to get loops later on
            int[] currentLine = GenericPoolArray<int>.Take(coverNodesCreated); //current line for RDP made out of indexes
            int currentLineCount = 0;       
            bool[] validStarts = GenericPoolArray<bool>.Take(coverNodesCreated, defaultValue: true); //segments

            #region segments
            for (int i = 0; i < coverNodesCreated; i++) {
                NodeCoverStruct coverNodeCur = coverNodesArray[i];
                if(coverNodeCur.connection != INVALID_VALUE) {
                    NodeCoverStruct coverNodeConnection = coverNodesArray[coverNodeCur.connection];
                    if (coverNodeCur.size == coverNodeConnection.size)
                        validStarts[coverNodeCur.connection] = false;
                }
                else {
                    validStarts[i] = false;
                }
            }

            for (int i = 0; i < coverNodesCreated; i++) {
                if (validStarts[i]) {
                    NodeCoverStruct coverNodeCur = coverNodesArray[i];                           

                    currentLineCount = 0;
                    for (int lineIndex = 0; lineIndex < coverNodesCreated; lineIndex++) {
                        currentLine[currentLineCount++] = coverNodeCur.ID;                 

                        if (coverNodeCur.connection == INVALID_VALUE) 
                            break;

                        if (coverNodeCur.size != coverNodesArray[coverNodeCur.connection].size) {
                            currentLine[currentLineCount++] = coverNodeCur.connection;
                            break;
                        }

                        coverNodeCur = coverNodesArray[coverNodeCur.connection];
                    }
        
                    SetupDouglasPeuckerCoverNew(currentLine, currentLineCount, epsilonFirst, epsilonSecond);
                  
                }        
            }
            #endregion
       
            #region Loops
            for (int i = 0; i < coverNodesCreated; i++) {
                if (coverNodesArray[i].RPD_flag == false) {
                    NodeCoverStruct coverNodeCur = coverNodesArray[i];
                    int startID = coverNodeCur.ID;

                    currentLineCount = 0;
                    for (int lineIndex = 0; lineIndex < coverNodesCreated; lineIndex++) {
                        currentLine[currentLineCount++] = coverNodeCur.ID;

                        coverNodeCur = coverNodesArray[coverNodeCur.connection];
                        if (coverNodeCur.ID == startID) {
                            currentLine[currentLineCount++] = coverNodeCur.ID;
                            break;
                        }
                    }

                    SetupDouglasPeuckerCoverNew(currentLine, currentLineCount, epsilonFirst, epsilonSecond);
                }
            }
            #endregion

            GenericPoolArray<bool>.ReturnToPool(ref validStarts);
            GenericPoolArray<bool>.ReturnToPool(ref dpWasHere);
            GenericPoolArray<int>.ReturnToPool(ref currentLine);
        }

        private void SetupDouglasPeuckerCoverNew(int[] targetNodeIDs, int targetNodeIDsCount, float firstEpsilon, float secondEpsilon) {
            Vector3[] vectors = GenericPoolArray<Vector3>.Take(targetNodeIDsCount);
            bool[] mask = GenericPoolArray<bool>.Take(targetNodeIDsCount, defaultValue: true);   

            for (int i = 0; i < targetNodeIDsCount; i++) {
                int index = targetNodeIDs[i];
                vectors[i] = coverNodesArray[index].positionV3;
                coverNodesArray[index].RPD_flag = true;

                if (mask[i] == false) {
                    coverNodesArray[index].ID = INVALID_VALUE;
                }
            } 

            DouglasPeucker(vectors, mask, targetNodeIDsCount, firstEpsilon * firstEpsilon);
            mask[0] = mask[targetNodeIDsCount - 1] = true;     
            DouglasPeucker(vectors, mask, targetNodeIDsCount, secondEpsilon * secondEpsilon);
            mask[0] = mask[targetNodeIDsCount - 1] = true;

            for (int i = 0; i < targetNodeIDsCount; i++) {
                if (mask[i] == false)      
                    coverNodesArray[targetNodeIDs[i]].ID = INVALID_VALUE;                
            }            

            for (int currentStart = 0; currentStart < targetNodeIDsCount - 1; currentStart++) {
                if (mask[currentStart]) {
                    for (int currentEnd = currentStart + 1; currentEnd < targetNodeIDsCount; currentEnd++) {
                        if (mask[currentEnd]) {  
                            coverNodesArray[targetNodeIDs[currentStart]].connection = targetNodeIDs[currentEnd];
                            break;
                        }
                    }
                }
            }

            GenericPoolArray<Vector3>.ReturnToPool(ref vectors);
            GenericPoolArray<bool>.ReturnToPool(ref mask);
        }

#if UNITY_EDITOR
        void DebugTempCovers() {
            float heightLow = template.properties.halfCover;
            float heightHigh = template.properties.fullCover;

            for (int i = 0; i < coverNodesCreated; i++) {
                NodeCoverStruct coverNodeCur = coverNodesArray[i];
                if (coverNodeCur.ID == INVALID_VALUE)
                    continue;

                if (coverNodeCur.connection != INVALID_VALUE) {
                    NodeCoverStruct coverNodeConnection = coverNodesArray[coverNodeCur.connection];

                    Vector3 p_cur = coverNodeCur.positionV3;
                    Vector3 p_con = coverNodeConnection.positionV3;

                    Vector3 p_cur_h = coverNodeCur.positionV3;
                    Vector3 p_con_h = coverNodeConnection.positionV3;

                    switch (coverNodeCur.size) {
                        case 1:
                            p_cur_h += Vector3.up * heightLow;
                            p_con_h += Vector3.up * heightLow;
                            break;
                        case 2:
                            p_cur_h += Vector3.up * heightHigh;
                            p_con_h += Vector3.up * heightHigh;
                            break;
                    }

                    Debuger_K.AddLine(p_cur, p_con, Color.magenta, 0f, 0.003f);
                    Debuger_K.AddLine(p_cur_h, p_con_h, Color.magenta, 0f, 0.003f);
                    Debuger_K.AddLine(p_cur, p_cur_h, Color.magenta);
                    Debuger_K.AddLine(p_con, p_con_h, Color.magenta);
                    //PFDebuger.Debuger_K.AddLabel(SomeMath.MidPoint(p_cur, p_con), coverNodeCur.size);

                }
            }
        }
#endif

        void SetupCoversData() {
            Vector3[] points = GenericPoolArray<Vector3>.Take(32);
            int pointsLength;

            for (int i = 0; i < coverNodesCreated; i++) {
                var coverCurrent = coverNodesArray[i];

                if (coverCurrent.ID == INVALID_VALUE | coverCurrent.connection == INVALID_VALUE)
                    continue;
                
                var coverConnection = coverNodesArray[coverCurrent.connection];

                pointsLength = 0;
                
                float agentRadius = template.agentRadiusReal;
                float agentDiameter = agentRadius * 2;
                int sqrAgentRadiusOnVolumeMap = template.agentRagius * template.agentRagius + 2;

                Vector2 leftV2 = coverCurrent.positionV2;
                Vector2 rightV2 = coverConnection.positionV2;

                Vector3 leftV3 = coverCurrent.positionV3;
                Vector3 rightV3 = coverConnection.positionV3;

                Vector2 dir = (rightV2 - leftV2).normalized;
                //Vector3 normal = new Vector3(dir.y, 0, -dir.x);

                float distance = Vector2.Distance(leftV2, rightV2);
                int mountPoints = (int)(distance / agentDiameter); // aprox how much agents are fit in that length of cover
             
                if (mountPoints < 2) {//single point in middle
                    points[pointsLength++] = (leftV3 + rightV3) * 0.5f;
                }
                else if (mountPoints == 2) {//two points on agent radius distance near ends
                    points[pointsLength++] = Vector3.Lerp(leftV3, rightV3, agentRadius / distance);
                    points[pointsLength++] = Vector3.Lerp(leftV3, rightV3, (distance - agentRadius) / distance);        
                }
                else {//whole bunch of points
                    points[pointsLength++] = Vector3.Lerp(leftV3, rightV3, agentRadius / distance);
                    points[pointsLength++] = Vector3.Lerp(leftV3, rightV3, (distance - agentRadius) / distance);
     
                    float startVal = agentDiameter / distance;
                    float step = (distance - agentDiameter - agentDiameter) / distance / (mountPoints - 2);

                    for (int i2 = 0; i2 < mountPoints - 2; i2++) {       
                        points[pointsLength++] = Vector3.Lerp(leftV3, rightV3, startVal + (step * 0.5f) + (step * i2));
                        if(pointsLength == points.Length) {
                            GenericPoolArray<Vector3>.IncreaseSize(ref points);
                        }
                    }
                }
                
                //find closest point and capture area around it to pass it throu all next parts
                int coverPointsStart = coverSpotsArrayCount;
                for (int pointIndex = 0; pointIndex < pointsLength; pointIndex++) {
                    Vector3 curPoint = points[pointIndex];
                    //Debuger_K.AddRay(curPoint, Vector3.up, Color.magenta);

                    VolumeContainerNew.DataPos_DirectIndex pos;
                    if (volumeContainer.GetClosestPos(curPoint, out pos)) {
                        VolumeArea coverArea = volumeContainer.CaptureArea(
                            pos.x, pos.z, pos.index,
                            AreaType.Cover,
                            sqrAgentRadiusOnVolumeMap, VoxelState.CheckForVoxelAreaFlag,
                            sqrAgentRadiusOnVolumeMap, VoxelState.CheckForVoxelAreaFlag,
                            false, true);
                        coverArea.position = curPoint;

                        if (coverSpotsArray.Length <= coverSpotsArrayCount)
                            GenericPoolArray<VolumeArea>.IncreaseSizeTo(ref coverSpotsArray, coverSpotsArrayCount + 1);

                       coverSpotsArray[coverSpotsArrayCount++] = coverArea;            
                    }
                }

                coverNodesArray[i].coverPointsStart = coverPointsStart;
                coverNodesArray[i].coverPointsCount = coverSpotsArrayCount - coverPointsStart;
            }

            GenericPoolArray<Vector3>.ReturnToPool(ref points);
        }

        void AddCoversToGraph(Graph graph) {
            for (int i = 0; i < coverSpotsArrayCount; i++) {
                var curSpot = coverSpotsArray[i];
                foreach (var edge in curSpot.edgesNew) {
                    curSpot.cellContentDatas.Add(new CellContentData(
                        nodesArray[edge.nodeLeft].positionV3, 
                        nodesArray[edge.nodeRight].positionV3));
                }
                //Debuger_K.AddLabel(curSpot.position, curSpot.edgesNew.Count());
            }

            for (int i = 0; i < coverNodesCreated; i++) {
                NodeCoverStruct coverCurrent = coverNodesArray[i];

                if (coverCurrent.ID != INVALID_VALUE && coverCurrent.connection != INVALID_VALUE) {
                    NodeCoverStruct coverConnection = coverNodesArray[coverCurrent.connection];

                    Vector2 leftV2 = coverCurrent.positionV2;
                    Vector2 rightV2 = coverConnection.positionV2;

                    Vector3 leftV3 = coverCurrent.positionV3;
                    Vector3 rightV3 = coverConnection.positionV3;

                    Vector2 dir = (rightV2 - leftV2).normalized;
                    Vector3 normal = new Vector3(dir.y, 0, -dir.x);

                    //Debuger_K.AddLabel(SomeMath.MidPoint(leftV3, rightV3), coverCurrent.coverPointsStart + " : " + coverCurrent.coverPointsCount);

                    //for (int pointIndex = 0; pointIndex < coverCurrent.coverPointsCount; pointIndex++) {
                    //    var point = coverSpotsArray[coverCurrent.coverPointsStart + pointIndex];
                    //    Debuger_K.AddLabel(point.position, "point");

                    //    foreach (var newEdge in point.edgesNew) {
                    //        point.cellContentDatas.Add(new CellContentData(nodesArray[newEdge.nodeLeft].positionV3, nodesArray[newEdge.nodeRight].positionV3));
                    //        Debuger_K.AddLine(point.position, nodesArray[newEdge.nodeLeft].positionV3, Color.red);
                    //        Debuger_K.AddLine(point.position, nodesArray[newEdge.nodeRight].positionV3, Color.red);
                    //    }
                    //}

                    graph.AddCover(
                        leftV3,
                        rightV3,
                        coverCurrent.size,
                        normal,
                        coverSpotsArray,
                        coverCurrent.coverPointsStart,
                        coverCurrent.coverPointsCount);
                }
            }
        }
    }
}