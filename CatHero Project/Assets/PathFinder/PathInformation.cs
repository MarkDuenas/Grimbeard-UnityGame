using UnityEngine;
using System.Collections.Generic;
using System;
using K_PathFinder.PFTools;
using K_PathFinder.Pool;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif

namespace K_PathFinder {
    public enum PathResultType : int {
        Valid = 1,
        BestFit = 0,
        InvalidAgentOutsideNavmesh = -1,
        InvalidTargetOutsideNavmesh = -2,
        InvalidNoPath = -3,
        InvalidExceedTimeLimit = -4,
        InvalidInternalIssue = -10 //in case there is some errors. tell developer how you get this
    }

    public enum MoveState : int {
        crouch = 2,
        walk = 3
    }
    //numbers here /\  should be same as here \/
    public enum PathNodeType : int {
        Invalid = -1,
        MoveCrouch = 2,
        MoveWalk = 3,
        JumpUpFirstNode = 4,
        JumpUpSecondNode = 5,
        JumpDownFirstNode = 6,
        JumpDownSecondNode = 7
    }
    
    public struct PathNode {
        public float x, y, z;
        public PathNodeType type;

        public PathNode(float X, float Y, float Z, PathNodeType Type) {
            x = X;
            y = Y;
            z = Z;
            type = Type;
        }

        public PathNode(Vector3 pos, PathNodeType Type) : this(pos.x, pos.y, pos.z, Type) { }

        public Vector3 Vector3 {
            get { return new Vector3(x, y, z); }
        }
        public Vector2 Vector2 {
            get { return new Vector2(x, z); }
        }

        public static implicit operator Vector3(PathNode obj) {
            return obj.Vector3;
        }
        public static implicit operator Vector2(PathNode obj) {
            return obj.Vector2;
        }

        public override string ToString() {
            return type.ToString();
        }
    }

    /// <summary>
    /// class for storing path
    /// it have some additional data:
    /// a) it have resultType which now tell is path valid or not
    /// b) it have owner which tells what agent recieve this path last time
    /// 
    /// Path internaly stored as Queue of structs and list of vectors internaly.
    /// Externaly you can retrive information about next node by using PeekNextNode or DequeueNextNode
    /// Since there stored Path owner it also can return distance to next node and have some other userful stuff
    /// Also in case you want to reuse this object then you can return it to object pool by calling ReturnToPool() this will reduce garbage generation
    /// </summary>
    public class Path {
        public List<PathNode> pathNodes = new List<PathNode>();
        public PathResultType pathType = PathResultType.InvalidInternalIssue;
        public IPathOwner owner;  
        int _currentIndex = 0;
        public float pathNavmeshCost;

        public List<CellPathContentAbstract> pathContent = new List<CellPathContentAbstract>();
        
        public bool valid {
            get {
                lock (this) {
                    return pathType > 0 && owner != null;
                }
            }
        }

        public int currentIndex {
            get { return _currentIndex; }
        }
        public int count {
            get { return pathNodes.Count - _currentIndex; }
        }

        #region extra precautions with threads
        public void Init(IPathOwner owner, PathResultType pathType) {
            this.owner = owner;
            this.pathType = pathType;
        }
        public void Clear() {
            pathType = PathResultType.InvalidInternalIssue;
            pathNodes.Clear();
            pathContent.Clear();
            owner = null;
            _currentIndex = 0;
        }
        public Path Copy() {
            lock (this) {
                Path result = GenericPool<Path>.Take();
                for (int i = 0; i < pathNodes.Count; i++) {
                    result.pathNodes.Add(pathNodes[i]);
                }
                result._currentIndex = _currentIndex;
                result.owner = owner;
                return result;
            }          
        }

        public void ReturnToPool() {
            Clear();
            GenericPool<Path>.ReturnToPool(this);
        }
        public static Path PoolRent() {
            return GenericPool<Path>.Take();
        }
        public static void PoolReturn(Path path) {
            GenericPool<Path>.ReturnToPool(path);
        }

        public void AddMove(Vector3 position, MoveState state) {
            pathNodes.Add(new PathNode(position, (PathNodeType)(int)state));
        }
        public void AddJumpUp(Vector3 JumpUpFirstNode, Vector3 JumpUpSecondNode) {
            pathNodes.Add(new PathNode(JumpUpFirstNode, PathNodeType.JumpUpFirstNode));
            pathNodes.Add(new PathNode(JumpUpSecondNode, PathNodeType.JumpUpSecondNode));
        }
        public void AddJumpDown(Vector3 JumpDownFirstNode, Vector3 JumpDownSecondNode) {
            pathNodes.Add(new PathNode(JumpDownFirstNode, PathNodeType.JumpDownFirstNode));
            pathNodes.Add(new PathNode(JumpDownSecondNode, PathNodeType.JumpDownSecondNode));
        }
        #endregion
        
        public PathNode this[int index] {
            get { return pathNodes[index]; }
        }
        
  

        /// <summary>
        /// return current node.
        /// return owner position if no nodes left
        /// </summary>
        public PathNode currentNode {
            get {
                if (_currentIndex >= pathNodes.Count) {
                    lock(this)
                        return new PathNode(owner.pathFallbackPosition, PathNodeType.Invalid);
                }
                else
                    return pathNodes[_currentIndex];
            }
        }
        public PathNode lastNode {
            get {
                if (pathNodes.Count == 0)
                    lock (this)
                        return new PathNode(owner.pathFallbackPosition, PathNodeType.Invalid);
                else
                    return pathNodes[pathNodes.Count - 1];
            }
        }
        public Vector2 lastV2 {
            get { return lastNode.Vector2; }
        }
        public Vector3 lastV3 {
            get { return lastNode.Vector3; }
        }
        public Vector2 currentV2 {
            get { return currentNode.Vector2; }
        }
        public Vector3 currentV3 {
            get { return currentNode.Vector3; }
        }

        public bool MoveToNextNode() {
            if (pathNodes == null || _currentIndex >= pathNodes.Count) {
                return false;
            }
            else {
                _currentIndex++;
                return true;
            }
        }
        public void SetCurrentIndex(int value) {
            _currentIndex = value;
        }

        /// <summary>
        /// iterate through nodes and return if there is node other than move node. movable mean it's only when you move. not jump. so you can tell if agent about to jump
        /// </summary>
        public bool MovableDistanceLesserThan(Vector3 ownerPosition, float targetDistance, out float distance, out PathNode node, out bool reachLastPoint) {
            if (valid == false) {
                Debug.LogWarning("path are invalid");
                node = new PathNode(0, 0, 0, PathNodeType.Invalid);
                distance = 0;
                reachLastPoint = true;
                return false;
            }

            if(pathNodes.Count == _currentIndex) {
                node = new PathNode(0, 0, 0, PathNodeType.Invalid);
                distance = 0;
                reachLastPoint = true;
                return true;
            }

            int remainNodes = count;   

            node = pathNodes[currentIndex];
            distance = SomeMath.Distance(ownerPosition, node.Vector3);

            if ((int)node.type >= 4) {//4, 5, 6, 7 are jumps right now
                reachLastPoint = remainNodes == 1;
                return distance < targetDistance;
            }

            if(remainNodes == 1) {
                reachLastPoint = true;
                return distance < targetDistance;
            }

            for (int i = currentIndex + 1; i < pathNodes.Count; i++) {
                node = pathNodes[i];
                distance += SomeMath.Distance(node.Vector3, pathNodes[i - 1].Vector3);
                node = pathNodes[i];
            
                if (distance > targetDistance) {    
                    reachLastPoint = i == pathNodes.Count - 1;
                    return false;
                }

                if ((int)node.type >= 4) {
                    reachLastPoint = i == pathNodes.Count - 1;
                    return distance < targetDistance;
                }
            }

            node = pathNodes[pathNodes.Count - 1];
            reachLastPoint = true;
            return true;
        }
        public bool MovableDistanceLesserThan(Vector3 ownerPosition, float targetDistance, out float distance, out bool reachLastPoint) {
            PathNode node;
            return MovableDistanceLesserThan(ownerPosition, targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(Vector3 ownerPosition, float targetDistance, out bool reachLastPoint) {
            float distance;
            PathNode node;
            return MovableDistanceLesserThan(ownerPosition, targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(Vector3 ownerPosition, float targetDistance, out PathNode node) {
            float distance;
            bool reachLastPoint;
            return MovableDistanceLesserThan(ownerPosition, targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(Vector3 ownerPosition, float targetDistance, out float distance) {
            PathNode node;
            bool reachLastPoint;
            return MovableDistanceLesserThan(ownerPosition, targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(Vector3 ownerPosition, float targetDistance) {
            float distance;
            PathNode node;
            bool reachLastPoint;
            return MovableDistanceLesserThan(ownerPosition, targetDistance, out distance, out node, out reachLastPoint);
        }

        private Vector2 ToVector2(Vector3 vector) {
            return new Vector2(vector.x, vector.z);
        }

        public override string ToString() {
            return string.Format("nodes {0}, index {1}", pathNodes.Count, _currentIndex);
        }

#if UNITY_EDITOR
        public void DebugByDebuger(float deltaHeight = 0f, bool onlyRemained = false, bool labels = true, float width = 0.001f) {
            DebugByDebuger(Color.red, deltaHeight, onlyRemained, labels, width);
        }
        public void DebugByDebuger(Color color, float deltaHeight = 0f, bool onlyRemained = false, bool labels = true, float width = 0.001f) {
            int start = 0;
            if (onlyRemained) 
                start = _currentIndex;            

            Vector3 delta = new Vector3(0, deltaHeight, 0);
            for (int i = start; i < pathNodes.Count; i++) {
                Debuger_K.AddDot(pathNodes[i] + delta, color);
                if(labels)
                    Debuger_K.AddLabelFormat(pathNodes[i] + delta, "{0} {1}", i, pathNodes[i]);
            }
            for (int i = start; i < pathNodes.Count - 1; i++) {
                Debuger_K.AddLine(pathNodes[i], pathNodes[i + 1], color, deltaHeight, width);
            }
        }
#endif
    }

    //its more of the example code. smooth Path slightly if your agent follow waypoints to strictly
    //public class PathSmoother {
    //    Path path;
    //    public List<PathNode> pathNodes = new List<PathNode>();
    //    int _currentIndex = 0;

    //    public void SetPath(Path path) {
    //        this.path = path;
    //    }

    //    public void Smooth(Vector3 curPos, int nodeIndex, int points, float turnDistance) {
    //        List<PathNode> pathPathNodes = path.pathNodes;      

    //        if(pathPathNodes.Count == 0) {
    //            pathNodes.Add(new PathNode(curPos, PathNodeType.Invalid));
    //            return;
    //        }

    //        if(pathPathNodes.Count == 1) {
    //            pathNodes.Add(pathPathNodes[0]);
    //            return;
    //        }

    //        PathNode curNode = pathPathNodes[nodeIndex];   
    //        if (nodeIndex == 1 | nodeIndex == pathPathNodes.Count - 1) {
    //            Vector3 leg;
    //            if (nodeIndex == 1) {          
    //                leg = GetLeg(1, false, turnDistance) + curNode;
    //            }
    //            else {
    //                PathNode otherNode = pathPathNodes[nodeIndex - 1];
    //                leg = GetLeg(nodeIndex - 1, true, turnDistance) + otherNode;
    //            }

    //            Debuger_K.AddLine(curPos, leg, Color.red);
    //            Debuger_K.AddLine(leg, curNode, Color.green);

    //            for (int i = 1; i < points; i++) {
    //                pathNodes.Add(new PathNode(SomeMath.BezierQuadratic((float)i / points, curPos, leg, curNode), curNode.type));
    //            }
    //            pathNodes.Add(curNode);
    //            return;
    //        }

    //        PathNode prevNode = pathPathNodes[nodeIndex - 1];
    //        Vector3 leg1 = GetLeg(nodeIndex - 1, true, turnDistance) + prevNode; 
    //        Vector3 leg2 = GetLeg(nodeIndex, false, turnDistance) + curNode;

    //        Debuger_K.AddLine(prevNode, leg1, Color.red);
    //        Debuger_K.AddLine(leg1, leg2, Color.green);
    //        Debuger_K.AddLine(leg2, curNode, Color.blue);

    //        for (int i = 0; i < points; i++) {
    //            pathNodes.Add(new PathNode(SomeMath.BezierCubic((float)i / points, prevNode, leg1, leg2, curNode), curNode.type));
    //        }
    //        pathNodes.Add(curNode);
    //    }

    //    public void Clear() {
    //        pathNodes.Clear();
    //    }

    //    private Vector3 GetLeg(int index, bool firstLeg, float maxLength) {
    //        List<PathNode> pathPathNodes = path.pathNodes;
    //        if (index == 0 | index == pathPathNodes.Count - 1)
    //            return Vector3.zero;
            
    //        Vector3 posCur = pathPathNodes[index];
    //        Vector3 posPrev = pathPathNodes[index - 1];
    //        Vector3 posNext = pathPathNodes[index + 1];

    //        Vector3 dirPrev = posCur - posPrev;
    //        Vector3 dirNext = posCur - posNext;
    //        float dirPrevLength = dirPrev.magnitude;
    //        float dirNextLength = dirNext.magnitude;

    //        dirPrev /= dirPrevLength;
    //        dirNext /= dirNextLength;

    //        Vector3 midPoint = ((dirPrev + dirNext) * 0.5f).normalized;  

    //        if(SomeMath.V2Cross(dirPrev.x, dirPrev.z, dirNext.x, dirNext.z) > 0) 
    //            midPoint *= -1;            

    //        if (firstLeg) {
    //            dirNextLength *= 0.5f;
    //            if (maxLength > dirNextLength)
    //                maxLength = dirNextLength;

    //            maxLength = dirNextLength;
    //            return new Vector3(midPoint.z * maxLength, 0, -midPoint.x * maxLength);
 
    //        }
    //        else {
    //            dirPrevLength *= 0.5f;
    //            if (maxLength > dirPrevLength)
    //                maxLength = dirPrevLength;

    //            maxLength = dirPrevLength;
    //            return new Vector3(-midPoint.z * maxLength, 0, midPoint.x * maxLength);
    //        }
    //    }
    //}
}