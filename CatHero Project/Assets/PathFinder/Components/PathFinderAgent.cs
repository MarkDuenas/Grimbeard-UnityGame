using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using K_PathFinder.Graphs;
using K_PathFinder.CoverNamespace;
using UnityEngine.Serialization;
//using K_PathFinder.RVOPF;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif

namespace K_PathFinder {
    public class PathFinderAgent : MonoBehaviour, IPathOwner {
        //general values
        [SerializeField][FormerlySerializedAs("properties")]
        private AgentProperties _properties;

        [LayerPF]
        public BitMaskPF PFlayerMask = 1;
        public BitMaskPF PFmodifierMask = 0;

        //Default Queries
        public NavMeshPathQueryGeneric queryPath;
        public NavMeshPointQuery<NodeCoverPoint> queryCover;
        public NavMeshPointQuery<CellSamplePoint> queryCellSample;

        //local avoidance agent
        [NonSerialized]
        public LocalAvoidanceAgent localAvoidanceAgent;
        private bool localAvoidanceRegistered = false;

        //general Pathfinder information
        private Vector3 _position;//since we cant call transform.position in thread we store it here

        //velocity obstacle variables
        //USED ONLY FOR INITIALIZATION
        [SerializeField][FormerlySerializedAs("velocityObstacle")]
        private bool _velocityObstacle = false;
        [SerializeField][FormerlySerializedAs("maxAgentVelocity")]  
        private float _maxAgentVelocity = 2f; //max distance agent should use for evasion
        [SerializeField][FormerlySerializedAs("avoidanceResponsibility")][Range(0f, 1f)]
        private float _avoidanceResponsibility = 0.5f; // bigger number - move inclined agent to evade
        [SerializeField][FormerlySerializedAs("careDistance")][Range(0.01f, 0.99f)]
        private float _careDistance = 0.75f; //how fast object should react to obstacle. 0 - instant, 1 - only if it collide/ range is in 0.01f - 0.99f
        [SerializeField][FormerlySerializedAs("maxNeighbors")]
        private int _maxNeighbors = 10; //The default maximum number of other agents a new agent takes into account in the navigation.
        [SerializeField][FormerlySerializedAs("maxNeighbourDistance")]
        private float _maxNeighbourDistance = 10f; //how far agent should check if object are suitable for avoidance

        //dead lock variables
        [SerializeField][FormerlySerializedAs("useDeadLockFailsafe")]
        private bool _useDeadLockFailsafe = false; //if true then agent will try to wiggle if standing for too long. In this case some "virtual" obstacles will be placed at random spots to throw agent from static state
        [SerializeField][FormerlySerializedAs("deadLockVelocityThreshold")]
        private float _deadLockVelocityThreshold = 0.025f;
        [SerializeField][FormerlySerializedAs("deadLockFailsafeVelocity")]
        private float _deadLockFailsafeVelocity = 0.3f;
        [SerializeField][FormerlySerializedAs("deadLockFailsafeTime")]
        private float _deadLockFailsafeTime = 2f;//in seconds

        //one side evasion
        [SerializeField][FormerlySerializedAs("preferOneSideEvasion")]
        private bool _preferOneSideEvasion = false;
        [SerializeField][FormerlySerializedAs("preferOneSideEvasionOffset")]
        private float _preferOneSideEvasionOffset = 2f;

        #region Unity API
        public virtual void Awake() {
            if(_properties != null) {
                _position = transform.position;
                if(queryPath == null)
                    queryPath = new NavMeshPathQueryGeneric(_properties, this);
                if (_properties.canCover && queryCover == null)
                    queryCover = new NavMeshPointQuery<NodeCoverPoint>(_properties);
                if (_properties.samplePoints && queryCellSample == null)
                    queryCellSample = new NavMeshPointQuery<CellSamplePoint>(_properties);

                if (_velocityObstacle && localAvoidanceAgent == null) {
                    localAvoidanceAgent = new LocalAvoidanceAgent(_properties);
                    SetLocalAvoidanceFields();
                    PathFinder.RegisterLocalAvoidanceAgent(localAvoidanceAgent);
                    localAvoidanceRegistered = true;
                }
            }
        }
        
        public virtual void Update() {            
            _position = transform.position; //updating agent position so it it is cached
            if (localAvoidanceAgent != null)
                localAvoidanceAgent.position = _position;
        }

        public virtual void OnEnable() {
            if (localAvoidanceAgent != null && localAvoidanceRegistered == false) {
                PathFinder.RegisterLocalAvoidanceAgent(localAvoidanceAgent);
                localAvoidanceRegistered = true;
            }
        }

        public virtual void OnDisable() {
            if (localAvoidanceAgent != null && localAvoidanceRegistered) {
                PathFinder.UnregisterLocalAvoidanceAgent(localAvoidanceAgent);
                localAvoidanceRegistered = false;
            }
        }

        public virtual void OnDestroy() {
            if (localAvoidanceAgent != null)
                PathFinder.UnregisterLocalAvoidanceAgent(localAvoidanceAgent);
        }
        #endregion

        #region local avoidance
        private void SetLocalAvoidanceFields() {
            localAvoidanceAgent.SetBasicSettings(
                _maxAgentVelocity, 
                _avoidanceResponsibility,
                _careDistance, 
                _maxNeighbors, 
                _maxNeighbourDistance,
                PFlayerMask);

            localAvoidanceAgent.SetDeadLockSettings(
                _useDeadLockFailsafe, 
                _deadLockVelocityThreshold, 
                _deadLockFailsafeVelocity, 
                _deadLockFailsafeTime);

            localAvoidanceAgent.SetSidePreference(
                _preferOneSideEvasion, 
                _preferOneSideEvasionOffset);
        }

        public bool velocityObstacle {
            get { return _velocityObstacle; }
            set {
                if(_properties == null) {
                    Debug.LogError("Set AgentProperties before you set local avoidance flag");
                    return;
                }

                if (value) {
                    if(localAvoidanceAgent == null) {
                        localAvoidanceAgent = new LocalAvoidanceAgent(_properties);
                        PathFinder.RegisterLocalAvoidanceAgent(localAvoidanceAgent);
                        localAvoidanceRegistered = true;
                    }                    
                    SetLocalAvoidanceFields();    
                }
                else {
                    if(localAvoidanceAgent != null) {                  
                        PathFinder.UnregisterLocalAvoidanceAgent(localAvoidanceAgent);
                        localAvoidanceRegistered = false;
                    }
                }
                _velocityObstacle = value;
            }
        }


        public Vector2 velocity {
            get { return localAvoidanceAgent.velocity; }
            set { localAvoidanceAgent.velocity = value; }
        }
        public Vector2 preferableVelocity {
            get { return localAvoidanceAgent.preferableVelocity; }
            set { localAvoidanceAgent.preferableVelocity = value; }
        }
        public Vector2 safeVelocity {
            get { return localAvoidanceAgent.safeVelocity; }
            set { localAvoidanceAgent.safeVelocity = value; }
        }

        public float maxAgentVelocity {
            get { return localAvoidanceAgent.maxAgentVelocity; }
            set { localAvoidanceAgent.maxAgentVelocity = value; }
        }

        public float avoidanceResponsibility {
            get { return localAvoidanceAgent.avoidanceResponsibility; }
            set { localAvoidanceAgent.avoidanceResponsibility = value; }
        }

        public float careDistance {
            get { return localAvoidanceAgent.careDistance; }
            set { localAvoidanceAgent.careDistance = value; }
        }

        public int maxNeighbors {
            get { return localAvoidanceAgent.maxNeighbors; }
            set { localAvoidanceAgent.maxNeighbors = value; }
        }
        public float maxNeighbourDistance {
            get { return localAvoidanceAgent.maxNeighbourDistance; }
            set { localAvoidanceAgent.maxNeighbourDistance = value; }
        }        
        public bool useDeadLockFailsafe {
            get { return localAvoidanceAgent.useDeadLockFailsafe; }
            set { localAvoidanceAgent.useDeadLockFailsafe = value; }
        }
        public float deadLockVelocityThreshold {
            get { return localAvoidanceAgent.deadLockVelocityThreshold; }
            set { localAvoidanceAgent.deadLockVelocityThreshold = value; }
        }
        public float deadLockFailsafeVelocity {
            get { return localAvoidanceAgent.deadLockFailsafeVelocity; }
            set { localAvoidanceAgent.deadLockFailsafeVelocity = value; }
        }
        public float deadLockFailsafeTime {
            get { return localAvoidanceAgent.deadLockFailsafeTime; }
            set { localAvoidanceAgent.deadLockFailsafeTime = value; }
        }


        public bool preferOneSideEvasion {
            get { return localAvoidanceAgent.preferOneSideEvasion; }
            set { localAvoidanceAgent.preferOneSideEvasion = value; }
        }
        public float preferOneSideEvasionOffset {
            get { return localAvoidanceAgent.preferOneSideEvasionOffset; }
            set { localAvoidanceAgent.preferOneSideEvasionOffset = value; }
        }
        #endregion


        /// <summary>
        /// acessor to agent properties. You can init agent and it's queries just by setting AgentProperties to this acessor. If you creating Agent from code then set it properties first
        /// </summary>
        public AgentProperties properties {
            get { return _properties; }
            set {
                _properties = value;
                _position = transform.position;
                queryPath = new NavMeshPathQueryGeneric(_properties, this);

                if (_properties.canCover)
                    queryCover = new NavMeshPointQuery<NodeCoverPoint>(_properties);
                if (_properties.samplePoints)
                    queryCellSample = new NavMeshPointQuery<CellSamplePoint>(_properties);
                if (_velocityObstacle) {
                    localAvoidanceAgent = new LocalAvoidanceAgent(_properties);
                    SetLocalAvoidanceFields();
                    PathFinder.RegisterLocalAvoidanceAgent(localAvoidanceAgent);
                    localAvoidanceRegistered = true;
                }
            }
        }  
        


        public virtual Vector3 positionVector3 {
            get { return _position; }
        }
        public virtual Vector2 positionVector2 {
            get { return ToVector2(positionVector3); }
        }   
        public float radius {
            get { return _properties.radius; }
        }
        
        public List<PointQueryResult<NodeCoverPoint>> covers {
            get { return queryCover.threadSafeResult; }
        }

        public List<PointQueryResult<CellSamplePoint>> sampledPoints {
            get { return queryCellSample.threadSafeResult; }
        }

        public virtual Vector3 pathFallbackPosition {
            get { return transform.position; }
        }

        #region Path
        /// <summary>
        /// accessor to Path from path query
        /// </summary>
        public Path path {
            get { return queryPath.threadSafeResult; }
        }
        /// <summary>
        /// accessor that return if there left nodes in path. return false if path null or count > 0. Handy to check if there any waypoints remain in path
        /// </summary>
        public bool haveNextNode {
            get {
                if (queryPath.threadSafeResult == null)
                    return false;
                else
                    return queryPath.threadSafeResult.count > 0;
            }
        }
        /// <summary>
        /// return next node that left in path. If there is no nodes left in path it returns agent position with Invalid state
        /// </summary>
        public PathNode nextNode {
            get {return path.currentNode;}
        }
        /// <summary>
        /// returns next node position - agent position
        /// </summary>
        public Vector3 nextNodeDirectionVector3 {
            get {return path.currentNode - positionVector3; } //implicit convertation to Vector3
        }
        /// <summary>
        /// return next node position - agent position on (x, z) axis
        /// </summary>
        public Vector2 nextNodeDirectionVector2 {
            get {return path.currentNode - positionVector2; } //implicit convertation to Vector2
        }
        //shifts index of current node in Path
        public bool RemoveNextNode(bool retainLastNode = false) {
            if (path == null)
                return false;

            if (retainLastNode) {
                if (path.count < 2)// 0 or 1
                    return false;
            }
            else if (path.count == 0)
                return false;


            path.MoveToNextNode();
            return true;
        }

        /// <summary>
        /// return true if node were removed
        /// sqrDistance is normal distance * distance to simplify math
        /// distance measured by Vector3
        /// </summary>
        public bool RemoveNextNodeIfCloserSqr(float sqrDistance, bool retainLastNode = false) {
            if (path == null)
                return false;

            if (retainLastNode) {
                if (path.count < 2)// 0 or 1
                    return false;
            }
            else if (path.count == 0)
                return false;


            Vector3 agentPos = positionVector3;
            PathNode node = path.currentNode;
            if (SomeMath.SqrDistance(node.x, node.y, node.z, agentPos.x, agentPos.y, agentPos.z) < sqrDistance) {
                path.MoveToNextNode();
                return true;
            }
            else {
                return false;
            }
        }
        /// <summary>
        /// return true if node were removed
        /// sqrDistance is normal distance * distance to simplify math
        /// distance measured by Vector2
        /// </summary>
        public bool RemoveNextNodeIfCloserSqrVector2(float sqrDistance, bool retainLastNode = false) {
            if (path == null)
                return false;

            if (retainLastNode) {
                if (path.count < 2)// 0 or 1
                    return false;
            }
            else if (path.count == 0)
                return false;

            Vector3 agentPos = positionVector3;
            PathNode node = path.currentNode;

            if (SomeMath.SqrDistance(node.x, node.z, agentPos.x, agentPos.z) < sqrDistance) {
                path.MoveToNextNode();
                return true;
            }
            else {
                return false;
            }
        }
        /// <summary>
        /// return true if node were removed
        /// distance measured by Vector3
        /// </summary>
        public bool RemoveNextNodeIfCloser(float distance, bool retainLastNode = false) {
            return RemoveNextNodeIfCloserSqr(distance * distance, retainLastNode);
        }
        /// <summary>
        /// return true if node were removed
        /// distance measured by Vector2
        /// </summary>
        public bool RemoveNextNodeIfCloserVector2(float distance, bool retainLastNode = false) {
            return RemoveNextNodeIfCloserSqrVector2(distance * distance, retainLastNode);
        }
        /// <summary>
        /// remove next node if it closer than agent radius
        /// return true if node were removed
        /// distance measured by Vector3
        /// </summary>
        public bool RemoveNextNodeIfCloserThanRadius(bool retainLastNode = false) {
            return RemoveNextNodeIfCloserSqr(radius * radius, retainLastNode);
        }
        /// <summary>
        /// remove next node if it closer than agent radius
        /// return true if node were removed
        /// distance measured by Vector2
        /// </summary>
        public bool RemoveNextNodeIfCloserThanRadiusVector2(bool retainLastNode = false) {
            return RemoveNextNodeIfCloserSqrVector2(radius * radius, retainLastNode);
        }

        /// <summary>
        /// iterate through nodes and return if there is node other than move node. movable mean it's only when you move. not jump. so you can tell if agent about to jump
        /// </summary>
        public bool MovableDistanceLesserThan(float targetDistance, out float distance, out PathNode node, out bool reachLastPoint) {
            if (path == null || path.count == 0) {
                node = nextNode;
                distance = 0;
                reachLastPoint = true;
                return false;
            }

            return path.MovableDistanceLesserThan(positionVector3, targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(float targetDistance, out float distance, out bool reachLastPoint) {
            PathNode node;
            return MovableDistanceLesserThan(targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(float targetDistance, out bool reachLastPoint) {
            float distance;
            PathNode node;
            return MovableDistanceLesserThan(targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(float targetDistance, out PathNode node) {
            float distance;
            bool reachLastPoint;
            return MovableDistanceLesserThan(targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(float targetDistance, out float distance) {
            PathNode node;
            bool reachLastPoint;
            return MovableDistanceLesserThan(targetDistance, out distance, out node, out reachLastPoint);
        }
        public bool MovableDistanceLesserThan(float targetDistance) {
            float distance;
            PathNode node;
            bool reachLastPoint;
            return MovableDistanceLesserThan(targetDistance, out distance, out node, out reachLastPoint);
        }
        #endregion
        
        /// <summary>
        /// This will add execution of delegate after path was recieved in case you want do add execution of something when it happen
        /// </summary>
        /// <param name="mode">delegates can be:
        /// thread safe. in this case they will be executed in next update. 
        /// not thread safe. in this case they will be executed imideadly after result was recieved </param>
        public void SetRecievePathDelegate(Action<Path> pathDelegate, AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            if (queryPath == null)
                queryPath = new NavMeshPathQueryGeneric(_properties, this);
            queryPath.SetOnFinishedDelegate(pathDelegate, mode); 
        }
        /// <summary>
        /// This will clear target delegate when path was recieved
        /// </summary>   
        public void RemoveRecievePathDelegate(AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            if (queryPath == null)
                queryPath = new NavMeshPathQueryGeneric(_properties, this);
            queryPath.RemoveOnFinishedDelegate(mode);
        }
        
        /// <summary>
        /// This will add execution of delegate after sample points was recieved in case you want do add execution of something when it happen
        /// a bit to much generic :)
        /// </summary>
        /// <param name="mode">delegates can be:
        /// thread safe. in this case they will be executed in next update. 
        /// not thread safe. in this case they will be executed imideadly after result was recieved </param>
        public void SetRecieveSampleDelegate(Action<List<PointQueryResult<CellSamplePoint>>> gridDelegate, AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            if (queryCellSample == null)
                queryCellSample = new NavMeshPointQuery<CellSamplePoint>(_properties);
            queryCellSample.SetOnFinishedDelegate(gridDelegate, mode);    
        }
        /// <summary>
        /// This will clear target delegate when path was recieved
        /// </summary>   
        public void RemoveRecieveSampleDelegate(AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            if (queryCellSample == null)
                queryCellSample = new NavMeshPointQuery<CellSamplePoint>(_properties);
            queryCellSample.RemoveOnFinishedDelegate(mode);
        }
        
        /// <summary>
        /// This will add execution of delegate after cover points was recieved in case you want do add execution of something when it happen
        /// a bit to much generic :)
        /// </summary>
        /// <param name="mode">delegates can be:
        /// thread safe. in this case they will be executed in next update. 
        /// not thread safe. in this case they will be executed imideadly after result was recieved </param>
        public void SetRecieveCoverDelegate(Action<List<PointQueryResult<NodeCoverPoint>>> coverDelegate, AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            if (queryCover == null)
                queryCover = new NavMeshPointQuery<NodeCoverPoint>(_properties);
            queryCover.SetOnFinishedDelegate(coverDelegate, mode);
        }
        /// <summary>
        /// This will clear target delegate when path was recieved
        /// </summary>   
        public void RemoveRecieveCoverDelegate(AgentDelegateMode mode = AgentDelegateMode.NotThreadSafe) {
            if (queryCover == null)
                queryCover = new NavMeshPointQuery<NodeCoverPoint>(_properties);
            queryCover.RemoveOnFinishedDelegate(mode);
        }

        public void SetGoalMoveHere(Vector3 start, Vector3 destination, BestFitOptions bestFitSearch = BestFitOptions.DontSearch, bool applyRaycast = true, bool collectPathContent = false, bool ignoreCrouchCost = false) {
            SetGoalMoveHere(start, destination, PFlayerMask.value, PFmodifierMask.value, bestFitSearch, applyRaycast, collectPathContent, ignoreCrouchCost);
        }
        public void SetGoalMoveHere(Vector3 destination, BestFitOptions bestFitSearch = BestFitOptions.DontSearch, bool applyRaycast = true, bool collectPathContent = false, bool ignoreCrouchCost = false) {
            SetGoalMoveHere(positionVector3, destination, PFlayerMask.value, PFmodifierMask.value, bestFitSearch, applyRaycast, collectPathContent, ignoreCrouchCost);
        }
        public void SetGoalMoveHere(Vector3 start, Vector3 destination, int layerMask, int costModifierMask, BestFitOptions bestFitSearch = BestFitOptions.DontSearch, bool applyRaycast = true, bool collectPathContent = false, bool ignoreCrouchCost = false) {
            if (_properties == null) {
                Debug.LogError("Agent dont have assigned Properties");
                return;
            }
            queryPath.QueueWork(start, destination, layerMask, costModifierMask, bestFitSearch, applyRaycast, collectPathContent, ignoreCrouchCost);
        }
        public void SetGoalMoveHere(Vector3 destination, int layerMask, int costModifierMask, BestFitOptions bestFitSearch = BestFitOptions.DontSearch, bool applyRaycast = true, bool collectPathContent = false, bool ignoreCrouchCost = false) {
            SetGoalMoveHere(positionVector3, destination, layerMask, costModifierMask, bestFitSearch, applyRaycast, collectPathContent, ignoreCrouchCost);
        }
        
        public void SetGoalGetSamplePoints(float maxCost, bool richCost = false, bool ignoreCrouchCost = false) {
            SetGoalGetSamplePoints(maxCost, PFlayerMask.value, PFmodifierMask.value, richCost, richCost);
        }
        public void SetGoalGetSamplePoints(float maxCost, bool richCost = false, bool ignoreCrouchCost = false, params Vector3[] positions) {
            SetGoalGetSamplePoints(maxCost, PFlayerMask.value, PFmodifierMask.value, richCost, ignoreCrouchCost, positions);
        }
        public void SetGoalGetSamplePoints(float maxCost, int layerMask, int costModifierMask, bool richCost = false, bool ignoreCrouchCost = false) {
            if (_properties == null) {
                Debug.LogError("Agent dont have assigned Properties");
                return;
            }
            queryCellSample.QueueWork(positionVector3, maxCost, layerMask, costModifierMask, null, richCost, ignoreCrouchCost);
        }
        public void SetGoalGetSamplePoints(float maxCost, int layerMask, int costModifierMask, bool richCost = false, bool ignoreCrouchCost = false, params Vector3[] positions) {
            if (_properties == null) {
                Debug.LogError("Agent dont have assigned Properties");
                return;
            }
            queryCellSample.QueueWork(maxCost, layerMask, costModifierMask, null, richCost, ignoreCrouchCost, positions: positions);
        }
        
        public void SetGoalFindCover(float maxMoveCost, bool richCost = true, bool ignoreCrouchCost = false) {
            SetGoalFindCover(maxMoveCost, PFlayerMask.value, PFmodifierMask.value, richCost, ignoreCrouchCost);
        }
        public void SetGoalFindCover(float maxMoveCost, int layerMask, int costModifierMask, bool richCost = true, bool ignoreCrouchCost = false) {
            if (_properties == null) {
                Debug.LogError("Agent dont have assigned Properties");
                return;
            }
            queryCover.QueueWork(positionVector3, maxMoveCost, layerMask, costModifierMask, null, richCost, ignoreCrouchCost);
        }
        
        //shortcuts
        private static Vector2 ToVector2(Vector3 v3) {
            return new Vector2(v3.x, v3.z);
        }
        private static Vector3 ToVector3(Vector2 v2) {
            return new Vector3(v2.x, 0, v2.y);
        } 
    }
}