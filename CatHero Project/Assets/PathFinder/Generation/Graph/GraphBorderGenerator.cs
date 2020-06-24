using K_PathFinder.PFDebuger;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Graphs {
    public partial class Graph {
        //bunch of static methods to generate graph border
        //todo:
        //calculate nearest point on two lines that about to sepparate

        const int INVALID_INDEX = -1;
        static Node defaultNode = new Node() { next = INVALID_INDEX };
        //static Vector3 debugUp = new Vector3(0, 0.1f, 0);
        static Axis debugAxis;
        static float debugOtherSide;

        struct Node {
            public int cell, connection;
            public int next;
            public float pos, height;
            public bool valid;

            public void Set(float pos, float height) {
                this.pos = pos;
                this.height = height;
                next = INVALID_INDEX;
                cell = connection = -1;
            }

            public void SetEdge(int cell, int next) {
                this.cell = cell;
                this.next = next;
            }
        }
        

        static void GenerateBorderEdges(
            List<CellContentConnectionData> left, List<CellContentConnectionData> right,
            Axis projectionAxis, float maxDiff,
            List<CellContentConnectionData> leftResult, List<CellContentConnectionData> rightResult) {
            Node[] sideLeft, sideRight;
            int sideLeftCount, sideRightCount;               

            //generate raw edges map
            GenerateSide(left, projectionAxis, out sideLeft, out sideLeftCount);
            GenerateSide(right, projectionAxis, out sideRight, out sideRightCount);

            var firstLeft = left[0].data;
            bool leftPositive;
            float otherAxisPosition; //all edges here are along this value amyway so i can pick any
            
            if (projectionAxis == Axis.x) {
                leftPositive = firstLeft.xLeft < firstLeft.xRight;
                otherAxisPosition = firstLeft.zLeft;
            }
            else {
                leftPositive = firstLeft.zLeft < firstLeft.zRight;
                otherAxisPosition = firstLeft.xLeft;
            }


            debugAxis = projectionAxis;
            debugOtherSide = otherAxisPosition;

            //project one side on another
            ProjectSide(ref sideLeft, ref sideLeftCount, sideRight, sideRightCount, leftPositive, maxDiff);
            ProjectSide(ref sideRight, ref sideRightCount, sideLeft, sideLeftCount, !leftPositive, maxDiff);

            PickConnections(sideLeft, sideLeftCount, sideRight, sideRightCount, maxDiff);
            PickConnections(sideRight, sideRightCount, sideLeft, sideLeftCount, maxDiff);

            for (int i = 0; i < sideLeftCount; i++) { sideLeft[i].valid = true; }
            for (int i = 0; i < sideRightCount; i++) { sideRight[i].valid = true; }


            //for (int targetIndex = 0; targetIndex < sideLeftCount; targetIndex++) {
            //    Node tNode1 = sideLeft[targetIndex];
            //    if (tNode1.next == INVALID_INDEX) continue;
            //    Node tNode2 = sideLeft[tNode1.next];

            //    Vector3 p1 = GetNodePos(tNode1);
            //    Vector3 p2 = GetNodePos(tNode2) + debugUp;
            //    Vector3 p3 = SomeMath.MidPoint(p1, p2);

            //    Debuger_K.AddLine(p1, p2, Color.blue);
            //    if (tNode1.cell != null)
            //        Debuger_K.AddLine(p3, tNode1.cell.centerVector3, Color.cyan);
            //    if (tNode1.connection != null)
            //        Debuger_K.AddLine(p3, tNode1.connection.centerVector3, Color.cyan);
            //}


            //remove repeated edges so all cells connected with one edge
            SimplifySide(sideLeft, sideLeftCount);
            SimplifySide(sideRight, sideRightCount);             

            //collect results into lists
            CollectSide(sideLeft, sideLeftCount, projectionAxis, otherAxisPosition, leftResult);
            CollectSide(sideRight, sideRightCount, projectionAxis, otherAxisPosition, rightResult);
        }

        private static void GenerateSide(List<CellContentConnectionData> content, Axis projectionAxis, out Node[] nodes, out int nodesCount) {
            nodes = GenericPoolArray<Node>.Take(8, defaultValue: defaultNode);
            nodesCount = 0;
            for (int i = 0; i < content.Count; i++) {
                CellContentConnectionData pair = content[i];
                CellContentData data = pair.data;
                float pos, height;
                int indexLeft = INVALID_INDEX, indexRight = INVALID_INDEX;

                //left node
                pos = projectionAxis == Axis.x ? data.xLeft : data.zLeft;
                height = data.yLeft;
                for (int nodeIndex = 0; nodeIndex < nodesCount; nodeIndex++) {
                    Node curNode = nodes[nodeIndex];
                    if (curNode.pos == pos && curNode.height == height) {
                        indexLeft = nodeIndex;
                        break;
                    }
                }

                if (indexLeft == INVALID_INDEX) {
                    if (nodes.Length == nodesCount)
                        GenericPoolArray<Node>.IncreaseSize(ref nodes);
                    indexLeft = nodesCount;
                    nodes[nodesCount++].Set(pos, height);
                }

                //right node
                pos = projectionAxis == Axis.x ? data.xRight : data.zRight;
                height = data.yRight;
                for (int nodeIndex = 0; nodeIndex < nodesCount; nodeIndex++) {
                    Node curNode = nodes[nodeIndex];
                    if (curNode.pos == pos && curNode.height == height) {
                        indexRight = nodeIndex;
                        break;
                    }
                }
                if (indexRight == INVALID_INDEX) {
                    if (nodes.Length == nodesCount)
                        GenericPoolArray<Node>.IncreaseSize(ref nodes);
                    indexRight = nodesCount;
                    nodes[nodesCount++].Set(pos, height);
                }
                nodes[indexLeft].SetEdge(pair.from, indexRight);
            }
        }

        private static void ProjectSide(ref Node[] target, ref int targetCount, Node[] projected, int projectedCount, bool projectedPositive, float maxDiff) {
            //iteration over 2 times
            //adding new nodes. only first projected node are used in this case
            for (int projectedIndex = 0; projectedIndex < projectedCount; projectedIndex++) {
                Node projected1 = projected[projectedIndex];

                for (int targetIndex = 0; targetIndex < targetCount; targetIndex++) {
                    Node tNode1 = target[targetIndex];
                    if (tNode1.next == INVALID_INDEX) continue;
                    Node tNode2 = target[tNode1.next];

                    bool inRange = projectedPositive ? //check if projected node in range
                        SomeMath.InRangeExclusive(projected1.pos, tNode1.pos, tNode2.pos) :
                        SomeMath.InRangeExclusive(projected1.pos, tNode2.pos, tNode1.pos);

                    if (inRange) {//divide
                        float projectedHeight = SomeMath.ClampLineToPlaneX(tNode1.pos, tNode1.height, tNode2.pos, tNode2.height, projected1.pos);

                        if (SomeMath.Difference(projected1.height, projectedHeight) < maxDiff) {
                            //adding new edge if in range
                            if (target.Length == targetCount)
                                GenericPoolArray<Node>.IncreaseSize(ref target);

                            //adding new node
                            target[targetCount].Set(projected1.pos, projectedHeight);
                            target[targetCount].SetEdge(tNode1.cell, tNode1.next);
                            target[targetIndex].next = targetCount;
                            targetCount++;
                        }
                    }
                }
            }   
        }

        private static void PickConnections(Node[] target, int targetCount, Node[] projected, int projectedCount, float maxDiff) {
            for (int projectedIndex = 0; projectedIndex < projectedCount; projectedIndex++) {
                Node projected1 = projected[projectedIndex];
                if (projected1.next == INVALID_INDEX) continue;
                Node projected2 = projected[projected1.next];

                //Vector3 p1 = GetNodePos(projected1);
                //Debuger_K.AddLabel(p1, projectedIndex);
                //Debuger_K.AddDot(p1, Color.red);

                for (int targetIndex = 0; targetIndex < targetCount; targetIndex++) {
                    Node tNode1 = target[targetIndex];
                    if (tNode1.next == INVALID_INDEX) continue;
                    Node tNode2 = target[tNode1.next];

                    if ((tNode1.pos == projected2.pos && SomeMath.Difference(tNode1.height, projected2.height) < maxDiff) |
                        (tNode2.pos == projected1.pos && SomeMath.Difference(tNode2.height, projected1.height) < maxDiff)) {
                        target[targetIndex].connection = projected1.cell;
                    }
                }
            }

            //for (int targetIndex = 0; targetIndex < targetCount; targetIndex++) {
            //    Node tNode1 = target[targetIndex];
            //    if (tNode1.next == INVALID_INDEX) continue;
            //    Node tNode2 = target[tNode1.next];

            //    Vector3 p1 = GetNodePos(tNode1);
            //    Vector3 p2 = GetNodePos(tNode2) + debugUp;
            //    Vector3 p3 = SomeMath.MidPoint(p1, p2);

            //    Debuger_K.AddLine(p1, p2, Color.blue);
            //    if (tNode1.cell != null)
            //        Debuger_K.AddLine(p3, tNode1.cell.centerVector3, Color.cyan);
            //    if (tNode1.connection != null)
            //        Debuger_K.AddLine(p3, tNode1.connection.centerVector3, Color.cyan);
            //}
        }

        private static void SimplifySide(Node[] values, int valuesCount) {
            for (int i = 0; i < valuesCount; i++) {
                Node n = values[i];
                if (n.valid && n.next != INVALID_INDEX) {
                    Node nn = values[n.next];
                    if (n.cell == nn.cell && n.connection == nn.connection) {
                        values[n.next].valid = false;
                        values[i].next = nn.next;
                    }
                }
            }
        }

        private static void CollectSide(Node[] values, int valuesCount, Axis axis, float otherSide, List<CellContentConnectionData> list) {
            for (int i = 0; i < valuesCount; i++) {
                Node nodeLeft = values[i];
                if (nodeLeft.valid && nodeLeft.next != INVALID_INDEX) {
                    Node nodeRight = values[nodeLeft.next];
                    Vector3 left, right;
                    if (axis == Axis.x) {
                        left = new Vector3(nodeLeft.pos, nodeLeft.height, otherSide);
                        right = new Vector3(nodeRight.pos, nodeRight.height, otherSide);
                    }
                    else {
                        left = new Vector3(otherSide, nodeLeft.height, nodeLeft.pos);
                        right = new Vector3(otherSide, nodeRight.height, nodeRight.pos);
                    }
                    list.Add(new CellContentConnectionData(nodeLeft.cell, nodeLeft.connection, left, right));
                }
            }
        }

        private static Vector3 GetNodePos(Node node) {
            if (debugAxis == Axis.x) 
                return new Vector3(node.pos, node.height, debugOtherSide);            
            else 
                return new Vector3(debugOtherSide, node.height, node.pos);            
        }
    }
}
