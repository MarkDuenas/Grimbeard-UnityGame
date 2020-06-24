using K_PathFinder.CoolTools;
using K_PathFinder.PFDebuger;
using K_PathFinder.Pool;
using K_PathFinder.VectorInt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace K_PathFinder.GraphGeneration {
    public partial class GraphGeneratorNew {
        public struct NodeStruct {
            public int ID;    
            private int _flags;
            public float x { get; private set; }
            public float y { get; private set; }
            public float z { get; private set; }

            public NodeStruct(int NodeID, float X, float Y, float Z) {
                ID = NodeID;
                x = X;
                y = Y;
                z = Z;    
                _flags = 0;
            }

            //use it wisley
            public void SetPosition(float newX, float newZ) {
                x = newX;
                z = newZ;     
            }

            public Vector2 positionV2 {
                get { return new Vector2(x, z); }
            }
            public Vector3 positionV3 {
                get { return new Vector3(x, y, z); }
            }


            #region position equals XZ
            public bool PositionEqualXZ(Vector3 position, float yError) {
                return PositionEqualXZ(this, position, yError);
            }
            public bool PositionEqualXZ(NodeStruct node, float yError) {
                return PositionEqualXZ(this, node, yError);
            }

            public static bool PositionEqualXZ(NodeStruct a, NodeStruct b, float maxDifY) {
                return a.positionV2 == b.positionV2 && Math.Abs(a.y - b.y) < maxDifY;
            }
            public static bool PositionEqualXZ(NodeStruct a, Vector3 b, float maxDifY) {
                return a.positionV2 == new Vector2(b.x, b.z) && Math.Abs(a.y - b.y) < maxDifY;
            }
            #endregion

            #region flags  
            public bool GetFlag(NodeTempFlags flag) {
                return (_flags & (int)flag) != 0;
            }
            public void SetFlag(NodeTempFlags flag, bool value) {
                _flags = value ? (_flags | (int)flag) : (_flags & ~(int)flag);
            }
            public bool border {
                get { return (_flags & 960) != 0; }
            }
            #endregion

            public override bool Equals(object obj) {
                if (obj == null || obj is NodeStruct == false)
                    return false;

                NodeStruct p = (NodeStruct)obj;
                return (x == p.x) && (y == p.y) && (z == p.z);
            }

            public override int GetHashCode() {
                return ID;
            }
        }
        public struct NodeEdgePair : IEquatable<NodeEdgePair> {
            public int layer;
            public int hash;
            public int index;

            public NodeEdgePair(int Layer, int Hash, int Index) {
                layer = Layer;
                hash = Hash;
                index = Index;
            }

            public override bool Equals(object other) {
                if (other == null || other is NodeEdgePair == false)
                    return false;

                NodeEdgePair p = (NodeEdgePair)other;
                return (hash == p.hash) && (index == p.index);
            }

            public bool Equals(NodeEdgePair other) {
                return (hash == other.hash) && (index == other.index);
            }

            public override int GetHashCode() {
                return hash + index;
            }
        }
        public struct NodeEdgeValue {
            public int ID;
            public int nodeLeft, nodeRight, layer, hash;
            private int _flags;

            #region flags
            public bool GetFlag(EdgeTempFlags flag) {
                return (_flags & (int)flag) != 0;
            }
            public void SetFlag(EdgeTempFlags flag, bool value) {
                _flags = value ? (_flags | (int)flag) : (_flags & ~(int)flag);
            }
            #endregion

            public int origin {
                get { return nodeLeft; }
                set { nodeLeft = value; }
            }
            public int connection {
                get { return nodeRight; }
                set { nodeRight = value; }
            }
        }

        public const int INVALID_VALUE = -1;
        
        public NodeStruct[] nodesArray;
        public int nodesCreated = 0;
        public StackedList<int> nodeList;

        public StackedList<VolumeArea> capturedVolumeAreaList;//ID of node is index here
        public StackedList<NodeEdgePair> nodePairs;

        //public NodeEdgePair[] nodePairs;
        public NodeEdgeValue[] edgesArray;
        public int edgesCreated;

        public int nodeChunkSize;
        public int nodeLayerCount;
        public int nodeHashCount;   
        
        void InitEdgeContainer() {
            var allHashes = template.hashData.GetAllHashes();

            nodeLayerCount = volumeContainer.layersCount;
            nodeHashCount = allHashes.Length;
            nodeChunkSize = nodeLayerCount * nodeHashCount;

            nodesArray = GenericPoolArray<NodeStruct>.Take(nodesSizeFlatten);
            nodePairs = StackedList<NodeEdgePair>.PoolTake(256, 256);

            //nodePairs = GenericPoolArray<NodeEdgePair>.Take(256 *` nodeChunkSize, defaultValue: DEFAULT_EDGE_PAIR_VALUE);
            edgesArray = GenericPoolArray<NodeEdgeValue>.Take(256);

            capturedVolumeAreaList = StackedList<VolumeArea>.PoolTake(nodesSizeFlatten, 512);
            nodeList = StackedList<int>.PoolTake(nodesSizeFlatten, 512);
        }


        public void ClearEdgesContainer() {
            //GenericPoolArray<NodeEdgePair>.ReturnToPool(ref nodePairs);
            //GenericPoolArray<NodeEdgeValue>.ReturnToPool(ref edgesArray);
            StackedList<NodeEdgePair>.PoolReturn(ref nodePairs);

            GenericPoolArray<NodeStruct>.ReturnToPool(ref nodesArray);

            GenericPoolArray<NodeStruct>.ReturnToPool(ref nodesArray);
            GenericPoolArray<NodeStruct>.ReturnToPool(ref nodesArray);

            StackedList<VolumeArea>.PoolReturn(ref capturedVolumeAreaList);
            StackedList<int>.PoolReturn(ref nodeList);
        }

        //example
        //void GetEdgeRange(int node, int layer, out int start, out int end) {
        //    start = (node * nodeChunkSize) + (layer * nodeHashCount);
        //    end = start + nodeHashCount;
        //}
        //example
        //private int GetFreeEdgeIndex() {
        //    int result = edgesCreated++;
        //    if (result == edgesArray.Length)
        //        GenericPoolArray<NodeEdgeValue>.IncreaseSize(ref edgesArray);
        //    return result;
        //}

        List<NodeEdgePair> tmpPairsList = new List<NodeEdgePair>();

        //return index if edge in nodeEdgeValues
        int SetEdgeNew(int nodeLeft, int nodeRight, int layer, int hash, IEnumerable<VolumeArea> volumeAreas, bool debug = false) {
            if (debug) {
                Debug.Log(string.Format("Connect {0} to {1} on layer {2}, hash {3}", nodeLeft, nodeRight, layer, hash));
            }

            if (volumeAreas != null) {
                foreach (var item in volumeAreas) {
                    capturedVolumeAreaList.AddCheckDublicates(nodeLeft, item);
                    capturedVolumeAreaList.AddCheckDublicates(nodeRight, item);
                }
            }

            nodePairs.Read(nodeLeft, tmpPairsList);

            foreach (var pair in tmpPairsList) {
                if (pair.hash == hash && pair.layer == layer) {
                    edgesArray[pair.index].nodeRight = nodeRight;
                    return pair.index;
                }
            }
            
            //int freeEdgeIndex = edgesCreated++;
            if (edgesCreated == edgesArray.Length)
                GenericPoolArray<NodeEdgeValue>.IncreaseSize(ref edgesArray);

            nodePairs.AddLast(nodeLeft, new NodeEdgePair(layer, hash, edgesCreated));
            edgesArray[edgesCreated] = new NodeEdgeValue() {
                ID = edgesCreated,
                nodeLeft = nodeLeft,
                nodeRight = nodeRight,
                layer = layer,
                hash = hash
            };

      
            return edgesCreated++;
        }

        int SetEdgeNew(Vector2Int1Float left, Vector2Int1Float right, int layer, int hash, IEnumerable<VolumeArea> volumeAreas) {     
            return SetEdgeNew(GetNodeNew(left), GetNodeNew(right), layer, hash, volumeAreas);
        }
        int GetNodeNew(Vector2Int1Float complexPos) {
            int index = GetNodeIndex(complexPos.x, complexPos.z);
            int[] tempArray = GenericPoolArray<int>.Take(1);
            int tempArrayLength;

            nodeList.Read(index, ref tempArray, out tempArrayLength);
            for (int i = 0; i < tempArrayLength; i++) {
                int nodeIndex = tempArray[i];
                if (Mathf.Abs(nodesArray[nodeIndex].y - complexPos.y) <= maxStepHeight) { //return value within step               
                    GenericPoolArray<int>.ReturnToPool(ref tempArray);
                    return nodeIndex;//if exist then return it
                }
            }

            int nodeID = nodesCreated++;
            if (nodesArray.Length == nodeID)
                GenericPoolArray<NodeStruct>.IncreaseSize(ref nodesArray);
            Vector3 nodePos = NodePos(complexPos);
            NodeStruct newNode = new NodeStruct(nodeID, nodePos.x, nodePos.y, nodePos.z);

            nodesArray[nodeID] = newNode;
            nodeList.AddLast(index, nodeID);

            if (nodePairs.baseSize <= nodeID)
                nodePairs.ExpandBaseSize(nodePairs.baseSize);

            //Debuger_K.AddLabel(nodePos, nodeID);
            //Debug.DrawRay(nodePos, Vector3.up, Color.cyan, 30f);
            GenericPoolArray<int>.ReturnToPool(ref tempArray);
            return nodeID;
        }
        void InsertNodeBetweenNew(int nodeA, int nodeB, int insertedNode) {
            NodeEdgeValue[] AB = GenericPoolArray<NodeEdgeValue>.Take(nodeChunkSize);
            NodeEdgeValue[] BA = GenericPoolArray<NodeEdgeValue>.Take(nodeChunkSize);
            int ABcount, BAcount;

            GetConnectionsToNode(nodeA, nodeB, ref AB, out ABcount);
            GetConnectionsToNode(nodeB, nodeA, ref BA, out BAcount);

            for (int i = 0; i < ABcount; i++) {
                NodeEdgeValue item = AB[i];
                SetEdgeNew(nodeA, insertedNode, item.layer, item.hash, null);
                SetEdgeNew(insertedNode, nodeB, item.layer, item.hash, null);
            }

            for (int i = 0; i < BAcount; i++) {
                NodeEdgeValue item = BA[i];
                SetEdgeNew(nodeB, insertedNode, item.layer, item.hash, null);
                SetEdgeNew(insertedNode, nodeA, item.layer, item.hash, null);
            }

            //Debuger_K.AddRay(nodesArray[insertedNode].positionV3, Vector3.up, Color.green);

            GenericPoolArray<NodeEdgeValue>.ReturnToPool(ref AB);
            GenericPoolArray<NodeEdgeValue>.ReturnToPool(ref BA);
        }
        
        int GetEdgeIndexNew(int nodeID, int targetLayer, int targetHash) {
            nodePairs.Read(nodeID, tmpPairsList);
            foreach (var pair in tmpPairsList) {
                if (pair.layer == targetLayer & pair.hash == targetHash) {
                    return pair.index;
                }
            }
            
#if UNITY_EDITOR
            Debuger_K.AddRay(nodesArray[nodeID].positionV3, Vector3.up, Color.red);
            Debuger_K.AddLabelFormat(nodesArray[nodeID].positionV3, "N {0} L {1} H {2}", nodeID, targetLayer, targetHash);
            Debug.LogFormat("N {0} L {1} H {2}", nodeID, targetLayer, targetHash);
#endif
            throw new Exception("Somehow this function did not find connection");
        }

        public int GetConnectedNodeNew(int nodeFromID, int targetLayer, int targetHash) {
            return edgesArray[GetEdgeIndexNew(nodeFromID, targetLayer, targetHash)].connection;
        }

        void RemoveEdgeNew(int ID) {
            var edge = edgesArray[ID];
            if (edge.ID == INVALID_VALUE)
                return;

            edgesArray[ID].ID = INVALID_VALUE;
            if (nodePairs.Remove(edge.nodeLeft, new NodeEdgePair(edge.layer, edge.hash, edge.ID)) == false)
                Debug.LogError("PathFinder for some reason was not able to remove target edge");
        }

        void RemoveUnusedNodes() {
            bool[] flagArray = GenericPoolArray<bool>.Take(nodesCreated, defaultValue: false);

            NodeEdgePair[] oData;
            IndexLengthInt[] oLayout;
            nodePairs.GetOptimizedData(out oData, out oLayout);

            for (int nodeID = 0; nodeID < nodesCreated; nodeID++) {
                var curNode = nodesArray[nodeID];
                if(curNode.ID != INVALID_VALUE) {
                    IndexLengthInt curLayout = oLayout[nodeID];

                    for (int i = 0; i < curLayout.length; i++) {
                        NodeEdgePair pair = oData[curLayout.index + i];
                        flagArray[edgesArray[pair.index].connection] = true;
                    }
                }
            }
            
            for (int nodeID = 0; nodeID < nodesCreated; nodeID++) {
                if(flagArray[nodeID] == false) {
                    nodesArray[nodeID].ID = INVALID_VALUE;
                }
            }

            GenericPoolArray<NodeEdgePair>.ReturnToPool(ref oData);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref oLayout);
        }
        void GetConnectionsToNode(int nodeFrom, int nodeTo, ref NodeEdgeValue[] array, out int resultsCount) {
            resultsCount = 0;
            nodePairs.Read(nodeFrom, tmpPairsList);

            foreach (var pair in tmpPairsList) {
                NodeEdgeValue edge = edgesArray[pair.index];
                if (edge.connection == nodeTo)
                    array[resultsCount++] = edge;
            }
        }

#if UNITY_EDITOR
        void DebugNewEdgeState(bool labels = true, float deltaHeight = 0f, string prefix = "") {
            //Debug.Log("Nodes: " + nodesCreated);
            Vector3 delta = new Vector3(0, deltaHeight, 0);


            NodeEdgePair[] oData;
            IndexLengthInt[] oLayout;
            nodePairs.GetOptimizedData(out oData, out oLayout);

            StringBuilder sb = new StringBuilder();
            for (int nodeID = 0; nodeID < nodesCreated; nodeID++) {
                var node = nodesArray[nodeID];

                Vector3 nodePos = node.positionV3;
                Debuger_K.AddDot(nodePos + delta, Color.blue);
                sb.AppendFormat("G{1} ID:{0}\n", node.ID, prefix);

                if (node.ID != INVALID_VALUE) {
                    IndexLengthInt curLayout = oLayout[nodeID];

                    for (int i = 0; i < curLayout.length; i++) {
                        NodeEdgePair pair = oData[curLayout.index + i];
                        NodeEdgeValue edge = edgesArray[pair.index];
                        
                        sb.AppendFormat("ID:{3}, Layer:{0}, Hash:{1}, Con:{2}\n", edge.layer, edge.hash, edge.connection, edge.ID);

                        Vector3 otherNodePos = nodesArray[edge.connection].positionV3;

                        Vector3 mid = SomeMath.MidPoint(nodePos, otherNodePos);
                        Debuger_K.AddLine(nodePos, mid, Color.blue, deltaHeight);
                        Debuger_K.AddLine(mid, otherNodePos, Color.red, deltaHeight);
                    }

                    if (labels)
                        Debuger_K.AddLabel(nodePos + delta, sb.ToString());
                    sb.Length = 0;
                }
            }

            GenericPoolArray<NodeEdgePair>.ReturnToPool(ref oData);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref oLayout);
        }
#endif

    }
}