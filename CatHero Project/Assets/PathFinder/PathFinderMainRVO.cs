using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Threading;
using K_PathFinder.CoolTools;
using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using System.Reflection;
using UnityEditor;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif
namespace K_PathFinder {
    //TODO:
    //plane intersection case with 1 and 2 VO
    //priority agents

    //public static class Utils {
    //    static MethodInfo _clearConsoleMethod;
    //    static MethodInfo clearConsoleMethod {
    //        get {
    //            if (_clearConsoleMethod == null) {
    //                Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
    //                Type logEntries = assembly.GetType("UnityEditor.LogEntries");
    //                _clearConsoleMethod = logEntries.GetMethod("Clear");
    //            }
    //            return _clearConsoleMethod;
    //        }
    //    }

    //    public static void ClearLogConsole() {
    //        clearConsoleMethod.Invoke(new object(), null);
    //    }
    //}

    public static partial class PathFinder {
        const float SOME_SMALL_NUMBER = 0.0025f;
        const float NEAR_BORDER_OFFSET = 0.05f;
        const int INVALID_VALUE = -1;

        static bool _localAvoidanceInit = false;
     
        static List<LocalAvoidanceAgent> LAagents = new List<LocalAvoidanceAgent>();
        static List<LocalAvoidanceAgent> LAagentsAdd = new List<LocalAvoidanceAgent>();
        static List<LocalAvoidanceAgent> LAagentsRemove = new List<LocalAvoidanceAgent>();
              
        static LocalAvoidanceAgentStruct[] LAWorkAgents;
        static int LAWorkAgentsCount;

        static LocalAvoidanceAgentDeadlock[] LAWorkAgentDeadlocks;
        static int LAWorkAgentDeadlocksCount;

        //static List<AgentTreeValue> treesList = new List<AgentTreeValue>();
        //static Generic2dBoundsTreePureIndexes<AgentTreeValue> tree = new Generic2dBoundsTreePureIndexes<AgentTreeValue>();

        //things
        struct ORCAline {
            public float positionX, positionY;
            public float normalX, normalY;
            public float responcibility; //represent responcibility of current agent. formula is: responcibility of current agent / summ of this and other agent responcbility

            public ORCAline(float posX, float posY, float normX, float normY, float resp) {
                positionX = posX;
                positionY = posY;
                normalX = normX;
                normalY = normY;
                responcibility = resp;

                if (normX == 0f & normY == 0f)
                    throw new ArgumentException("normal cant have 0 length");
                if (float.IsNaN(normX) | float.IsInfinity(normX))
                    throw new ArgumentException("Parameter invalid", "normX");
                if (float.IsNaN(normY) | float.IsInfinity(normY))
                    throw new ArgumentException("Parameter invalid", "normY");
            }
            public ORCAline(Vector2 pos, Vector2 norm, float resp) : this(pos.x, pos.y, norm.x, norm.y, resp) { }

            public Vector2 position { get { return new Vector2(positionX, positionY); } }
            public Vector2 normal { get { return new Vector2(normalX, normalY); } }

            public float Dot() {
                return SomeMath.Dot(positionX, positionY, normalX, normalY);
            }

            public override string ToString() {
                return string.Format("x: {0}, y: {1}, nx: {2}, ny {3}", positionX, positionY, normalX, normalY);
            }
        }
        
        //if value > 0 then it's siutable for truncation
        //some logic based on x > 0 and x < 0 so dont change numbers befor checking logic
        enum VectorValueState : int {
            border = 3,           //node on border of external quad    
            connection = 2,       //connection from node border to other node. in contain 2 dots on line that passed through center. so it's just excluded from other checks     
            normal = 1,           //normal node
            invalid = 0,          //invalid value
            invalidEdge = -1,     //in this case edge excluded from further iterations
            obstructed = -2,      //node visibility is obstructed by other lines        
        }

        struct VectorValue {
            public float x, y;
            public int next;
            public VectorValueState state;

            public void Set(float newX, float newY, int newNext, VectorValueState s) {
                x = newX;
                y = newY;
                next = newNext;
                state = s;
            }

            public void Set(float newX, float newY, VectorValueState s) {
                x = newX;
                y = newY;
                state = s;
            }

            //should be removed later on
            public Vector2 vector {
                get { return new Vector2(x, y); }
                set {
                    x = value.x;
                    y = value.y;
                }
            }

            //for debug
            public Vector3 vector3 {
                get { return vector; }
                set { vector = value; }
            }

            public float SqrDistance(float posX, float posY) {
                return SomeMath.SqrDistance(x, y, posX, posY);
            }

            //reduce array cals slightly
            public void SetNormalIfNextValid() {
                if (next != INVALID_VALUE)
                    state = VectorValueState.normal;
            }
        }

        struct AgentTreeValue {
            public float x, y, z;
            public int agentIndex;
            public AgentTreeValue(Vector3 pos, int index) {
                x = pos.x;
                y = pos.y;
                z = pos.z;
                agentIndex = index;
            }
        }

        //shape
        const int SHAPE_EDGES_COUNT = 8; //default shape edges count;   
        static Vector2[] defaultShape;

        public struct LocalAvoidanceAgentStruct {
            public int navmeshPositionSample;
            public int layerMask;
            public int navmeshDeadlock;

            public float radius, height, maxVelocity, responcibility, careDistance;
            public Vector2 velocity, prefVelocity; 

            public int maxNeighbors;
            public float maxNeighbourDistance;
         
            public bool preferOneSideEvasion;
            public float preferOneSideEvasionOffset;

            public LocalAvoidanceAgentStruct(int layerMask, float radius, float height, float responcibility, float careDistance, Vector2 velocity, Vector2 prefVelocity, float maxVelocity, int maxNeighbors, float maxNeighbourDistance, bool preferOneSideEvasion, float preferOneSideEvasionOffset) {
                navmeshPositionSample = 0;
                navmeshDeadlock = 0;

                this.layerMask = layerMask;
                this.radius = radius;
                this.height = height;
                this.responcibility = responcibility;
                this.careDistance = careDistance;

                this.velocity = velocity;
                this.prefVelocity = prefVelocity;
                this.maxVelocity = maxVelocity;
                this.maxNeighbors = maxNeighbors;
                this.maxNeighbourDistance = maxNeighbourDistance;
                this.preferOneSideEvasion = preferOneSideEvasion;
                this.preferOneSideEvasionOffset = preferOneSideEvasionOffset;   
            }
        }

        public struct LocalAvoidanceAgentDeadlock {          
            public float deadLockVelocityThreshold;
            public float deadLockFailsafeVelocity;
            public float deadLockFailsafeTime;
            public DateTime deadLockTriggeredTime;
            public LocalAvoidanceAgentDeadlock(float deadLockVelocityThreshold, float deadLockFailsafeVelocity, float deadLockFailsafeTime, DateTime deadLockTriggeredTime) {
                this.deadLockVelocityThreshold = deadLockVelocityThreshold;
                this.deadLockFailsafeVelocity = deadLockFailsafeVelocity;
                this.deadLockFailsafeTime = deadLockFailsafeTime;
                this.deadLockTriggeredTime = deadLockTriggeredTime;
            }
        }


        static void InitLocalAvoidance() {
            try {
                //create static default shape
                defaultShape = new Vector2[SHAPE_EDGES_COUNT];
                for (int p = 0; p < SHAPE_EDGES_COUNT; ++p) {
                    defaultShape[p] = new Vector2(
                        Mathf.Cos(p * 2f * Mathf.PI / SHAPE_EDGES_COUNT),
                        Mathf.Sin(p * 2f * Mathf.PI / SHAPE_EDGES_COUNT));
                }      
            }
            catch (Exception e) {
                Debug.LogErrorFormat("Error occurred while initializing PathFinder local avoidance: {0}", e);
                throw;
            }   
        }
        
        private static void UpdateLocalAvoidanceBeforePositionSample() {
            if (_localAvoidanceInit == false) {
                InitLocalAvoidance();
                _localAvoidanceInit = true;
            }

            lock (LAagentsAdd) {
                LAagents.AddRange(LAagentsAdd);
                LAagentsAdd.Clear();
            }
            lock (LAagentsRemove) {
                foreach (var agent in LAagentsRemove) {
                    LAagents.Remove(agent);
                }
                LAagentsRemove.Clear();
            }

            try {
                LAWorkAgentDeadlocksCount = 0;
                LAWorkAgentsCount = LAagents.Count;
                LAWorkAgents = GenericPoolArray<LocalAvoidanceAgentStruct>.Take(LAWorkAgentsCount);
                LAWorkAgentDeadlocks = GenericPoolArray<LocalAvoidanceAgentDeadlock>.Take(LAWorkAgentsCount / 4);
                          
                for (int i = 0; i < LAWorkAgentsCount; i++) {
                    var agent = LAagents[i];
                    Vector3 pos;
                    AgentProperties pr;
                    var s = agent.GetStruct(out pos, out pr);
                 
                    s.navmeshPositionSample = RegisterNavmeshSample(pr, pos, s.layerMask, true);             

                    if (agent.useDeadLockFailsafe) {
                        if (LAWorkAgentDeadlocks.Length == LAWorkAgentDeadlocksCount)
                            GenericPoolArray<LocalAvoidanceAgentDeadlock>.IncreaseSize(ref LAWorkAgentDeadlocks);
                        LAWorkAgentDeadlocks[LAWorkAgentDeadlocksCount] = agent.GetDeadlock();
                        s.navmeshDeadlock = LAWorkAgentDeadlocksCount;
                        LAWorkAgentDeadlocksCount++;
                    }
                    else
                        s.navmeshDeadlock = -1;

                    LAWorkAgents[i] = s;
                }
            }
            catch (Exception e) {
                Debug.LogErrorFormat("Error occurred while updating agent position requests in local avoidance: {0}", e);
                throw;
            }
        }
                
        static void UpdateLocalAvoidance() {
            //make kd tree
            try {
                sortableAgents = GenericPoolArray<AgentTreeValue>.Take(LAWorkAgentsCount);
                sortableAgentsCount = 0;

                for (int agentIndex = 0; agentIndex < LAWorkAgentsCount; agentIndex++) {            
                    NavmeshSampleResult_Internal agentNavmeshSample = navmeshPositionResults[LAWorkAgents[agentIndex].navmeshPositionSample];
                    if (agentNavmeshSample.cellGlobalID != -1)
                        sortableAgents[sortableAgentsCount++] = new AgentTreeValue(agentNavmeshSample.position, agentIndex);                   
                }

                BuildTree(7);

#if UNITY_EDITOR
                Debuger_K.debugSetLocalAvoidance.Clear();
                if (Debuger_K.debugRVO) {           
                    if (Debuger_K.debugRVOKDTree)
                        DebugBoundsTree(Debuger_K.debugSetLocalAvoidance);
                }
#endif
            }
            catch (Exception e) {
                Debug.LogErrorFormat("Error occurred while building agent bounds tree: {0}", e);
                throw;
            }

            //get navmesh position
            try {
                CalculateSafeVelocity();
            }
            catch (Exception e) {
                Debug.LogErrorFormat("Error occurred while calculating safe velocity for agents: {0}", e);
                throw;
            }
        }

        public static void RegisterLocalAvoidanceAgent(LocalAvoidanceAgent agent) {
            lock (LAagentsAdd)
                LAagentsAdd.Add(agent);
        }
        public static void UnregisterLocalAvoidanceAgent(LocalAvoidanceAgent agent) {
            lock (LAagentsRemove)
                LAagentsRemove.Add(agent);
        }

        static void ClearLocalAvoidanceAgents() {
            lock (LAagentsAdd)
                LAagentsAdd.Clear();
            lock (LAagentsRemove)
                LAagentsRemove.Clear();
        }
        
        private static void CalculateSafeVelocity() {
            Cell[] globalCells = PathFinderData.cells;

            ORCAline[] ORCAlines = GenericPoolArray<ORCAline>.Take(16);
            int ORCAlinesCount;
            int[] stackArray = GenericPoolArray<int>.Take(16);
            int[] treeResultArray = GenericPoolArray<int>.Take(16);

            LocalAvoidanceAgentStruct[] neighboursArray = GenericPoolArray<LocalAvoidanceAgentStruct>.Take(16);
            float[] neightboursDistanceArray = GenericPoolArray<float>.Take(16);
            int neighboursCount;

            //for avoiding borders
            bool[] globalCellsFlags = GenericPoolArray<bool>.Take(PathFinderData.maxRegisteredCellID + 1, defaultValue: false);
            int[] globalCellsUsed = GenericPoolArray<int>.Take(64);//cell id's        
            int[] cellQueue = GenericPoolArray<int>.Take(64);

            Line2D[] obstacleLines = GenericPoolArray<Line2D>.Take(16);    

            for (int agentIndex = 0; agentIndex < LAWorkAgentsCount; agentIndex++) {
                LocalAvoidanceAgentStruct agent = LAWorkAgents[agentIndex];
                NavmeshSampleResult_Internal agentNavmeshSample = navmeshPositionResults[agent.navmeshPositionSample];
                //Debuger_K.AddLabel(agentNavmeshSample.position, agentNavmeshSample.resultType);
                if (agentNavmeshSample.cellGlobalID == -1) {
                    LAagents[agentIndex].safeVelocity = Vector2.zero;
                    continue;
                }

                if (agentNavmeshSample.type == NavmeshSampleResultType.OutsideNavmesh | agentNavmeshSample.type == NavmeshSampleResultType.InvalidByLayerMask) {
                    LAagents[agentIndex].safeVelocity = SomeMath.ToVector2(agentNavmeshSample.position - agentNavmeshSample.origin).normalized * agent.maxVelocity;
                    continue;
                }

                neighboursCount = ORCAlinesCount = 0;
                Vector3 agentPosition = agentNavmeshSample.position;
                Vector2 agentVelocity = agent.velocity;
                float neighbourSqrDist = agent.maxNeighbourDistance * agent.maxNeighbourDistance;

                float preferableVelocityX = agent.prefVelocity.x;
                float preferableVelocityY = agent.prefVelocity.y;   

                #region debug basic
#if UNITY_EDITOR
                if (Debuger_K.debugRVO && Debuger_K.debugRVObasic) {
                    Debuger_K.debugSetLocalAvoidance.AddLine(DrawCircle(50, agentPosition, agent.radius), Color.black, true);
                    Debuger_K.debugSetLocalAvoidance.AddRay(agentPosition, ToV3(agentVelocity), Color.white);
                    Debuger_K.debugSetLocalAvoidance.AddRay(agentPosition, ToV3(agent.prefVelocity), Color.yellow);
                }
#endif
                #endregion

                //get neighbours from tree
                int treeResultCount;
                TreeSearch(
                    agentPosition.x - agent.maxNeighbourDistance,
                    agentPosition.x + agent.maxNeighbourDistance,
                    agentPosition.z - agent.maxNeighbourDistance,
                    agentPosition.z + agent.maxNeighbourDistance,
                    ref treeResultArray, out treeResultCount, ref stackArray);

                //increase (if need) array to max neighbours size;
                if (neighboursArray.Length <= agent.maxNeighbors) {
                    GenericPoolArray<LocalAvoidanceAgentStruct>.IncreaseSizeTo(ref neighboursArray, agent.maxNeighbors);
                    GenericPoolArray<float>.IncreaseSizeTo(ref neightboursDistanceArray, agent.maxNeighbors);
                }

                //fill neighbours array with target size
                for (int i = 0; i < treeResultCount; i++) {
                    var neighbourSortable = sortableAgents[treeResultArray[i]];

                    if (agentIndex == neighbourSortable.agentIndex)
                        continue;

                    float yDiff = neighbourSortable.y - agentPosition.y;

                    if (yDiff < 0f)
                        yDiff *= -1f;

                    if (yDiff > agent.height)
                        continue;

                    float curSqrDist = SomeMath.SqrDistance(
                        agentNavmeshSample.positionX, agentNavmeshSample.positionY, agentNavmeshSample.positionZ,
                        neighbourSortable.x, neighbourSortable.y, neighbourSortable.z);

                    if (curSqrDist <= neighbourSqrDist) {
                        if (neighboursCount < agent.maxNeighbors) {
                            neighboursArray[neighboursCount] = LAWorkAgents[neighbourSortable.agentIndex];
                            neightboursDistanceArray[neighboursCount] = curSqrDist;
                            neighboursCount++;
                        }
                        else {
                            for (int n = 0; n < neighboursCount; n++) {
                                if (neightboursDistanceArray[n] > curSqrDist) {
                                    neighboursArray[n] = LAWorkAgents[neighbourSortable.agentIndex];
                                    neightboursDistanceArray[n] = curSqrDist;
                                    break;
                                }
                            }
                        }
                    }
                }

                //iterate over neighbours to avoid them
                for (int i = 0; i < neighboursCount; i++) {
                    LocalAvoidanceAgentStruct neighbour = neighboursArray[i];
                    NavmeshSampleResult_Internal neighbourNavmeshSample = navmeshPositionResults[neighbour.navmeshPositionSample];

#if UNITY_EDITOR
                    if (Debuger_K.debugRVO && Debuger_K.debugRVONeighbours) {
                        Debuger_K.debugSetLocalAvoidance.AddLine(agentPosition, neighbourNavmeshSample.position, Color.cyan);
                    }
#endif

                    float responcibility = agent.responcibility / (agent.responcibility + neighbour.responcibility);
                    if (responcibility <= 0f) continue;
                    if (responcibility > 1f) responcibility = 1f;

                    Vector2 localPos = new Vector2(neighbourNavmeshSample.position.x - agentPosition.x, neighbourNavmeshSample.position.z - agentPosition.z);
                    float localPosSqrMagnitude = localPos.sqrMagnitude;
                    float radiusSum = agent.radius + neighbour.radius;

                    #region agents collide
                    //agents have overlaping radius
                    if (localPosSqrMagnitude < radiusSum * radiusSum) {
                        if (localPosSqrMagnitude < 0.01f) {//agents literaly inside each others so we give it some random vector
                            System.Random random = new System.Random(agent.GetHashCode());
                            float rX = random.Next(1, 100) * 0.01f;
                            float rY = random.Next(1, 100) * 0.01f;
                            if (random.Next(1) == 1) rX *= -1;
                            if (random.Next(1) == 1) rY *= -1;
                            Vector2 randomVector = new Vector2(rX, rY).normalized;

                            if (ORCAlines.Length == ORCAlinesCount)
                                GenericPoolArray<ORCAline>.IncreaseSize(ref ORCAlines);
                            ORCAlines[ORCAlinesCount++] = new ORCAline(randomVector * -agent.radius, -randomVector, responcibility);
                        }
                        else {
                            if (ORCAlines.Length == ORCAlinesCount)
                                GenericPoolArray<ORCAline>.IncreaseSize(ref ORCAlines);
                            ORCAlines[ORCAlinesCount++] = new ORCAline(-(radiusSum - Mathf.Sqrt(localPosSqrMagnitude)) * localPos.normalized, -localPos, responcibility);
                        }
                        continue;
                    }
                    #endregion

                    Vector2 localVelocity = agentVelocity - neighbour.velocity;
                    //if object local position and local velocity are in opposite direction then it's dont matter at all
                    if (SomeMath.Dot(localVelocity, localPos) < 0f)
                        continue;

                    float localPosMagnitude = localPos.magnitude;
                    float localPosRadian = Mathf.Atan2(localPos.y, localPos.x); //local position in radians   

                    #region prefer one side offsets
                    if (agent.preferOneSideEvasion) {
                        float offsetFactor = agent.preferOneSideEvasionOffset * ((localPosMagnitude - radiusSum) / agent.maxNeighbourDistance);
                        float radOffset = (neighbour.radius * offsetFactor) / (localPosMagnitude * 2 * Mathf.PI) * 2 * Mathf.PI;
                        localPosRadian += radOffset;
                        localPos = RadToVector(localPosRadian, localPosMagnitude);
                        radiusSum += offsetFactor * 0.5f;
                    }
                    #endregion

                    Vector2 localPosNormalized = localPos.normalized;
                    float localVelMagnitude = localVelocity.magnitude;
                    float angleRad = Mathf.Asin(radiusSum / localPosMagnitude); //offset to side in radians
                    float angleDeg = angleRad * Mathf.Rad2Deg;

                    //trunk
                    float truncatedRadius = radiusSum * neighbour.careDistance;
                    //float truncatedRadiusSqr = SomeMath.Sqr(truncatedRadius);
                    float truncatedBoundryDistance = truncatedRadius / (radiusSum / localPosMagnitude);
                    float truncatedBoundryStart = truncatedBoundryDistance * Mathf.Cos(Mathf.Asin(truncatedRadius / truncatedBoundryDistance));
                    Vector2 truncatedBoundryCenter = localPosNormalized * truncatedBoundryDistance;

                    #region debug
#if UNITY_EDITOR
                    if (Debuger_K.debugRVO && Debuger_K.debugRVOvelocityObstacles) {
                        //Debug radius sum before it is changed
                        if (agent.preferOneSideEvasion)
                            Debuger_K.debugSetLocalAvoidance.AddLine(DrawCircle(50, neighbourNavmeshSample.position, agent.radius + neighbour.radius), Color.red, true);

                        //Debug radius sum
                        Debuger_K.debugSetLocalAvoidance.AddLine(DrawCircle(50, agentNavmeshSample.position + new Vector3(localPos.x, 0, localPos.y), radiusSum), Color.black, true);

                        //debug cut off circle
                        Debuger_K.debugSetLocalAvoidance.AddLine(DrawCircle(50, agentNavmeshSample.position + ToV3(truncatedBoundryCenter), truncatedRadius), Color.black, true);
                        Vector2 A1 = RadToVector((localPosRadian + angleRad), truncatedBoundryStart);
                        Vector2 B1 = RadToVector((localPosRadian - angleRad), truncatedBoundryStart);
                        Vector2 A2 = RadToVector((localPosRadian + angleRad), truncatedBoundryStart + 10);
                        Vector2 B2 = RadToVector((localPosRadian - angleRad), truncatedBoundryStart + 10);

                        Debuger_K.debugSetLocalAvoidance.AddLine(agentNavmeshSample.position + ToV3(A1), agentNavmeshSample.position + ToV3(A2), Color.blue);
                        Debuger_K.debugSetLocalAvoidance.AddLine(agentNavmeshSample.position + ToV3(B1), agentNavmeshSample.position + ToV3(B2), Color.blue);

                        //debug local velocity
                        Debuger_K.debugSetLocalAvoidance.AddLine(agentNavmeshSample.position, agentNavmeshSample.position + ToV3(localVelocity), Color.blue);
                        Debuger_K.debugSetLocalAvoidance.AddDot(agentNavmeshSample.position + ToV3(localVelocity), Color.blue);
                    }
#endif
                    #endregion

                    #region collision course
                    if (localVelMagnitude >= truncatedBoundryDistance - truncatedRadius - NEAR_BORDER_OFFSET && //it's closest than closest boundry point
                        Vector2.Angle(localVelocity, localPos) < angleDeg + 5f) {//if local velocity in angle

                        if (localVelMagnitude <= truncatedBoundryStart) {//if inside truncated area
                            if (ORCAlines.Length == ORCAlinesCount)      //we for sure add new line so check if array need to be increased
                                GenericPoolArray<ORCAline>.IncreaseSize(ref ORCAlines);

                            Vector2 centerToLocalVel = localVelocity - truncatedBoundryCenter;
                            float centerToLocalValSqrMagnitude = centerToLocalVel.sqrMagnitude;

                            if (centerToLocalValSqrMagnitude < SOME_SMALL_NUMBER) {
                                //local velocity position are like right on top of center. is there near zero radius agent or what?
                                Vector2 uVector = -localPosNormalized;
                                ORCAlines[ORCAlinesCount++] = new ORCAline(uVector * truncatedRadius, uVector, responcibility);
                            }
                            else {
                                float centerToLocalValMagnitude = Mathf.Sqrt(centerToLocalValSqrMagnitude);
                                centerToLocalVel = centerToLocalVel / centerToLocalValMagnitude;//now normalized
                                Vector2 closestToBoundry = centerToLocalVel * truncatedRadius + truncatedBoundryCenter;
                                Vector2 uVector = closestToBoundry - truncatedBoundryCenter;
                                float dif = truncatedRadius - centerToLocalValMagnitude;
                                Vector2 velocityBorder = agentVelocity + (responcibility * dif * uVector);
                                ORCAlines[ORCAlinesCount++] = new ORCAline(velocityBorder, uVector.normalized, responcibility);
                            }
                        }
                        else {
                            //more fancy way but i dont fully understand it yet
                            //Vector2 w = localVel - 0.1f * localPos;
                            //float wLengthSq = (w).sqrMagnitude;
                            //float dotProduct1 = Vector2.Dot(w, localPos);
                            //float leg = (float)Math.Sqrt(localPosSqrMagnitude - radiusSumSqr);
                            //Vector2 lineDir;

                            //project on left leg
                            if (SomeMath.V2Cross(localPos, localVelocity) < 0) {
                                //lineDir = new Vector2(localPos.x * leg - localPos.y * radiusSum, localPos.x * radiusSum + localPos.y * leg) / localPosSqrMagnitude;
                                Vector2 legDir = RadToVector(localPosRadian + angleRad, 1f);

                                if (agent.preferOneSideEvasion) {
                                    responcibility = 1f;
                                }

                                //that formula below are god know what. here is short  description of it:
                                //Vector2 closestToBoundry = legDir * Vector2.Dot(legDir, localVel);
                                //Vector2 uVector = closestToBoundry - localVel;
                                //Vector2 velocityBorder = agentVelocity + (uVector * responcibility);
                                //ORCAlines.Add(new ORCAline(velocityBorder, new Vector2(-legDir.y, legDir.x), responcibility));

                                if (ORCAlines.Length == ORCAlinesCount)
                                    GenericPoolArray<ORCAline>.IncreaseSize(ref ORCAlines);

                                ORCAlines[ORCAlinesCount++] = new ORCAline(
                                        agentVelocity + (((legDir * Vector2.Dot(legDir, localVelocity)) - localVelocity) * responcibility),
                                        new Vector2(-legDir.y, legDir.x),
                                        responcibility);
                            }
                            //project on right leg
                            else {
                                //lineDir = -new Vector2(localPos.x * leg + localPos.y * radiusSum, -localPos.x * radiusSum + localPos.y * leg) / localPosSqrMagnitude;
                                Vector2 legDir = RadToVector(localPosRadian - angleRad, 1f);
                                if (ORCAlines.Length == ORCAlinesCount)
                                    GenericPoolArray<ORCAline>.IncreaseSize(ref ORCAlines);

                                ORCAlines[ORCAlinesCount++] = new ORCAline(
                                        agentVelocity + (((legDir * Vector2.Dot(legDir, localVelocity)) - localVelocity) * responcibility),
                                        new Vector2(legDir.y, -legDir.x),
                                        responcibility);
                            }
                            //Vector2 linePos = agentVelocity + 0.5f * (Vector2.Dot(localVel, lineDir) * lineDir - localVel);
                            //lineDir = new Vector2(-lineDir.x, lineDir.y);                        
                            //Debuger_K.AddDot(agent.positionVector3 + ToV3(linePos) + (Vector3.up * 0.1f), Color.red);
                            //Debuger_K.AddLine(agent.positionVector3 + (Vector3.up * 0.1f), agent.positionVector3 + ToV3(linePos) + (Vector3.up * 0.1f), Color.red);
                            //Debuger_K.AddLine(agent.positionVector3 + ToV3(linePos) + (Vector3.up * 0.1f), agent.positionVector3 + ToV3(lineDir) + (Vector3.up * 0.1f), Color.blue);
                        }
                    }
                    #endregion
                }
 
                #region deadlock failsafe
                //if (agent.navmeshDeadlock != -1) {
                //    LocalAvoidanceAgentDeadlock dl = LAWorkAgentDeadlocks[agent.navmeshDeadlock];
                //    if(DateTime.Now.Subtract(dl.deadLockTriggeredTime).TotalSeconds < dl.deadLockFailsafeTime) {
                //        for (int i = 0; i < curNeighbour; i++) {                 
                //            int neighbourIndex = neighboursArray[i].agentIndex;
                //            LocalAvoidanceAgentStruct neighbour = LAWorkAgents[neighbourIndex];

                //            Vector3 neighbourPos = neighbour.position;
                //            float distance = SomeMath.Distance(agentPosition.x, agentPosition.z, neighbourPos.x, neighbourPos.z);
                //            float radiusSum = agentRadius + neighbour.radius;
                //            float freeSpace = distance - radiusSum;

                //            if (freeSpace < agent.deadLockFailsafeVelocity) {
                //                if (freeSpace < 0f)
                //                    freeSpace = 0f;

                //                Vector2 direction = new Vector2(neighbourPos.x - agentPosition.x, neighbourPos.z - agentPosition.z).normalized;
                //                if (ORCAlines.Length == ORCAlinesCount)
                //                    GenericPoolArray<ORCAline>.IncreaseSize(ref ORCAlines);
                //                ORCAlines[ORCAlinesCount++] = new ORCAline((agent.deadLockFailsafeVelocity + freeSpace) * -1 * direction, -direction, 0.5f);
                //            }
                //        }
                //    }
                //}   
                #endregion

                #region MANUAL ADDING
                //**************MANUAL ADDING**************//
                //var debugOrca = agent.transform.GetComponentsInChildren<DebugOrca>();
                //foreach (var item in debugOrca) {
                //    if (item.enabled == false | item.gameObject.activeInHierarchy == false)
                //        continue;
                //    ORCAline dOrca = new ORCAline(ToV2(item.transform.position) - agentPosition, ToV2(item.transform.forward).normalized, item.responcibility);
                //    ORCAlines.Add(dOrca);
                //    Vector3 n = GetNormal3d(dOrca);
                //    //item.transform.position -= (new Vector3(n.x, 0, n.z) * Time.deltaTime * 0.1f);
                //}
                //**************MANUAL ADDING**************//
                #endregion

                #region debug Velocity Obstacle
#if UNITY_EDITOR
                if (Debuger_K.debugRVO && Debuger_K.debugRVOvelocityObstacles) {
                    for (int i = 0; i < ORCAlinesCount; i++) {
                        var ORCA = ORCAlines[i];
                        Debuger_K.debugSetLocalAvoidance.AddDot(ToV3(ORCA.position) + agentNavmeshSample.position, Color.blue, 0.05f);
                        Debuger_K.debugSetLocalAvoidance.AddLine(agentNavmeshSample.position + ToV3(ORCA.position), agentNavmeshSample.position + ToV3(ORCA.position) + ToV3(ORCA.normal), Color.blue, 0.002f);
                    }
                }
#endif
                #endregion

                VectorValue[] vecVal = GenericPoolArray<VectorValue>.Take(16);
                int vecValCount = 0;

                for (int i = 0; i < SHAPE_EDGES_COUNT; i++) {
                    Vector2 vec = defaultShape[i];
                    vecVal[i].Set(vec.x * agent.maxVelocity, vec.y * agent.maxVelocity, i + 1, VectorValueState.normal);
                }
                vecVal[SHAPE_EDGES_COUNT - 1].next = 0;
                vecValCount = SHAPE_EDGES_COUNT;

                #region applying orca lines
                //chipping off from target shape small chunks
                //it potentialy can work even if there is no base shape
                //but in this case looking up for target velocity will be a lot harder
                for (int ol = 0; ol < ORCAlinesCount; ol++) {
                    var ORCA = ORCAlines[ol];
                    Vector2 ORCApos = ORCA.position;
                    Vector2 ORCArotated = new Vector2(ORCA.normal.y, -ORCA.normal.x);

                    int edgeLeftIndex = INVALID_VALUE;
                    int edgeRightIndex = INVALID_VALUE;

                    // dividing all edges by cross product to target line
                    for (int i = 0; i < vecValCount; i++) {
                        VectorValue vv = vecVal[i];
                        if (vecVal[i].state > 0)
                            vecVal[i].state = (SomeMath.V2Cross(ORCArotated.x, ORCArotated.y, vv.x - ORCA.positionX, vv.y - ORCA.positionY) < 0f) ? VectorValueState.normal : VectorValueState.invalid;
                    }

                    //find indexes of 2 edges where nodes have (normal, invalid) and (invalid, normal) state
                    //since shape is convex only 2 edges possible
                    //also flaging all edges where bouth edges was invalid so they excluded from further checks
                    for (int i = 0; i < vecValCount; i++) {
                        VectorValue vv = vecVal[i];
                        if (vv.state != VectorValueState.invalidEdge) {
                            if (vv.state > 0) {
                                if (vecVal[vv.next].state <= 0)
                                    edgeLeftIndex = i;
                            }
                            else if (vecVal[vv.next].state == VectorValueState.normal)
                                edgeRightIndex = i;
                            else
                                vecVal[i].state = VectorValueState.invalidEdge;
                        }
                    }

                    if (edgeLeftIndex != INVALID_VALUE && edgeRightIndex != INVALID_VALUE) {
                        VectorValue edgeLeft = vecVal[edgeLeftIndex];  //cur: normal, next: invalid
                        VectorValue edgeRight = vecVal[edgeRightIndex];//cur: invalid, next: normal
                        float Lx, Ly, Rx, Ry; //two values used cause potentialy line intersection casted from same node. so either i get copy of all 4 nodes or have sepparated results          

                        if (SomeMath.LineIntersection(edgeLeft.vector, vecVal[edgeLeft.next].vector, ORCApos, ORCApos + ORCArotated, out Lx, out Ly) &&   //geting intersections of lines
                            SomeMath.LineIntersection(edgeRight.vector, vecVal[edgeRight.next].vector, ORCApos, ORCApos + ORCArotated, out Rx, out Ry)) {//geting intersections of lines
                                                                                                                                                         //increase array is it's size exceed its limit
                            if (vecVal.Length == vecValCount)
                                GenericPoolArray<VectorValue>.IncreaseSize(ref vecVal);

                            vecVal[vecValCount].Set(Lx, Ly, edgeRightIndex, VectorValueState.normal);//new edge
                            vecVal[edgeLeftIndex].next = vecValCount;                                //set left next edge to new edge
                            vecVal[edgeRightIndex].Set(Rx, Ry, VectorValueState.normal);             //set right edge first node to intersection              
                            vecValCount++; //increase count
                        }
                    }
                }

#if UNITY_EDITOR
                if (Debuger_K.debugRVO && Debuger_K.debugRVOconvexShape) {
                    float centerX = 0f, centerY = 0f;
                    int centerCount = 0;

                    for (int i = 0; i < vecValCount; i++) {
                        var vv = vecVal[i];
                        if (vv.state == VectorValueState.normal) {
                            centerX += vv.x;
                            centerY += vv.y;
                            centerCount++;
                        }
                    }

                    if (centerCount > 0) {
                        Vector3 center = new Vector3((centerX / centerCount) + agentPosition.x, agentPosition.y, (centerY / centerCount) + agentPosition.z);

                        for (int i = 0; i < vecValCount; i++) {
                            var vv0 = vecVal[i];
                            if (vv0.state == VectorValueState.normal) {
                                var vv1 = vecVal[vv0.next];
                                Vector3 v0 = ToV3(vv0.vector) + agentPosition;
                                Vector3 v1 = ToV3(vv1.vector) + agentPosition;
                                Debuger_K.debugSetLocalAvoidance.AddLine(v0, v1);
                                Debuger_K.debugSetLocalAvoidance.AddTriangle(center, v0, v1, new Color(0, 1, 0, 0.2f), false);
                            }
                        }
                    }
                }
#endif
                #endregion

                #region searching nearest position inside trimmed convex space
                //figuring out if target velocity outside free delta-velocity shape
                bool targetObstructed = false;
                bool anyFreeVelocity = false;

                if (preferableVelocityX == 0f & preferableVelocityY == 0f) {
                    //if target velocity have 0 length then logic a lot simplier
                    //checking if center always on right side for target line 
                    for (int obstacleIndex = 0; obstacleIndex < vecValCount; obstacleIndex++) {
                        VectorValue nodeL = vecVal[obstacleIndex];
                        if (nodeL.state == VectorValueState.normal) {
                            VectorValue nodeR = vecVal[nodeL.next];
                            if (SomeMath.V2Cross(nodeR.x - nodeL.x, nodeR.y - nodeL.y, -nodeL.x, -nodeL.y) > 0f) {
                                targetObstructed = true;
                                break;
                            }
                        }
                    }
                }
                else {
                    //if target velocity have vector
                    //in this case iteration over all lines, check if direction passing through this line 
                    //and measuring line-product for target vector
                    bool anyLine = false;
                    for (int obstacleIndex = 0; obstacleIndex < vecValCount; obstacleIndex++) {
                        VectorValue nodeL = vecVal[obstacleIndex];
                        if (nodeL.state == VectorValueState.normal) {
                            VectorValue nodeR = vecVal[nodeL.next];
                            float crossL = SomeMath.V2Cross(preferableVelocityX, preferableVelocityY, nodeL.x, nodeL.y);
                            float crossR = SomeMath.V2Cross(preferableVelocityX, preferableVelocityY, nodeR.x, nodeR.y);

                            if (crossL == 0f && crossR == 0f)
                                continue;

                            //check forward face
                            if (crossL <= 0f & crossR >= 0f) {
                                anyLine = true;
                                float dirX = nodeR.x - nodeL.x;
                                float dirY = nodeR.y - nodeL.y;
                                if ((-nodeL.x * dirY + nodeL.y * dirX) / (preferableVelocityY * dirX - preferableVelocityX * dirY) > 1f) {
                                    targetObstructed = true;
                                    break;
                                }
                            }

                            //check back face
                            if (crossL >= 0f & crossR <= 0f) {
                                anyLine = true;
                                float dirX = nodeR.x - nodeL.x;
                                float dirY = nodeR.y - nodeL.y;
                                if ((-nodeL.x * dirY + nodeL.y * dirX) / (preferableVelocityY * dirX - preferableVelocityX * dirY) < 1f) {
                                    targetObstructed = true;
                                    break;
                                }
                            }
                        }
                    }
                    //if no lines intersected then target velocity outside shape
                    if (!anyLine)
                        targetObstructed = true;
                }

                if (targetObstructed) {
                    //if line obstructed checking closest position on all lines to target
                    //TODO: maybe there is better way?

                    float sqrDist = float.MaxValue;
                    float newTargetVelocityX = preferableVelocityX;
                    float newTargetVelocityY = preferableVelocityY;

                    for (int obstacleIndex = 0; obstacleIndex < vecValCount; obstacleIndex++) {
                        VectorValue nodeL = vecVal[obstacleIndex];
                        if (nodeL.state == VectorValueState.normal) {
                            anyFreeVelocity = true;
                            VectorValue nodeR = vecVal[nodeL.next];
                            float rx, ry;
                            SomeMath.NearestPointToSegment(nodeL.x, nodeL.y, nodeR.x, nodeR.y, preferableVelocityX, preferableVelocityY, out rx, out ry);
                            float curSqrDist = SomeMath.SqrDistance(preferableVelocityX, preferableVelocityY, rx, ry);
                            if (curSqrDist < sqrDist) {
                                sqrDist = curSqrDist;
                                newTargetVelocityX = rx;
                                newTargetVelocityY = ry;
                            }
                        }
                    }

                    if (anyFreeVelocity) {
                        preferableVelocityX = newTargetVelocityX;
                        preferableVelocityY = newTargetVelocityY;
                    }
                    else {
                        //if shape have zero surface area
                        Vector2 planeResult;
                        //then some cool geometry is applyed
                        //in this case local avoidance search for first avaiable velocity if we expand ORCA lines in linear speed
                        //this results in setting agent safe velocity to maximasing collision time instead of moving towards it goal
                        if (SolvePlanesIntersections(ORCAlines, ORCAlinesCount, agentNavmeshSample.position, agent.maxVelocity, out planeResult) == false) {
                            //sometimes it still fail and it hard to understand why
                            LAagents[agentIndex].safeVelocity = Vector2.zero;
                            //string s = "\n";
                            //for (int i = 0; i < ORCAlinesCount; i++) {
                            //    var ORCA = ORCAlines[i];
                            //    s += string.Format("position: {0}, normal: {1} \n", ORCA.position, ORCA.normal);
                            //}
                            //Debug.LogWarningFormat("somehow velocity obstacles plane intersection solver dont solve those planes. pease tell developer how it's happen. ORCA list: \n{0}", s);
                        }

                        preferableVelocityX = planeResult.x;
                        preferableVelocityY = planeResult.y;
                    }
                }
                else {
                    anyFreeVelocity = true;
                }

                #endregion



                #region avoid borders
                if (preferableVelocityX != 0f & preferableVelocityY != 0f) {
                    int obstacleLinesCount = 0;
                    float maxVelocitySqr = agent.maxVelocity * agent.maxVelocity;
                 
                    int cellArrayCount = 0;
                    int globalCellsUsedCount = 0;

                    cellQueue[cellArrayCount++] = agentNavmeshSample.cellGlobalID;

                    for (int i = 0; i < cellArrayCount; i++) {
                        int cellID = cellQueue[i];

                        globalCellsFlags[cellID] = true;
                        if (globalCellsUsed.Length == globalCellsUsedCount)
                            GenericPoolArray<int>.IncreaseSize(ref globalCellsUsed);
                        globalCellsUsed[globalCellsUsedCount++] = cellID;

                        Cell cell = globalCells[cellID];
                        if ((1 << cell.bitMaskLayer & agent.layerMask) == 0)
                            continue;

                        //collecting edges
                        var convinientData = cell.raycastData;
                        int convinientDataCount = cell.raycastDataCount;

                        for (int d = 0; d < convinientDataCount; d++) {
                            CellContentRaycastData data = convinientData[d];

                            if (SomeMath.V2Cross(data.xRight - data.xLeft, data.zRight - data.zLeft, agentPosition.x - data.xLeft, agentPosition.z - data.zLeft) > 0f &&
                                SomeMath.SqrDistance(agentPosition.x, agentPosition.z, data.NearestPointXZ(agentPosition.x, agentPosition.z)) < maxVelocitySqr) {

                                if (data.connection == -1 || (1 << globalCells[data.connection].bitMaskLayer & agent.layerMask) == 0) {
                                    if (obstacleLines.Length == obstacleLinesCount)
                                        GenericPoolArray<Line2D>.IncreaseSize(ref obstacleLines);

                                    obstacleLines[obstacleLinesCount++] = new Line2D(
                                        data.xLeft - agentPosition.x, data.zLeft - agentPosition.z,
                                        data.xRight - agentPosition.x, data.zRight - agentPosition.z);
                                }

                                //Debug.Log(globalCellsFlags.Length + " : " + data.connection);

                                if (data.connection != -1 && globalCellsFlags[data.connection] == false) {
                                    if (cellQueue.Length == cellArrayCount)
                                        GenericPoolArray<int>.IncreaseSize(ref cellQueue);
                                    cellQueue[cellArrayCount++] = data.connection;
                                }
                            }
                        }

                    }

                    //clearing used bools
                    for (int i = 0; i < globalCellsUsedCount; i++) {
                        globalCellsFlags[globalCellsUsed[i]] = false;
                    }

                    if (obstacleLinesCount > 0) {
                        //check if target velocity obstructed by navmesh borders
                        targetObstructed = false;
                        for (int onstacleIndex = 0; onstacleIndex < obstacleLinesCount; onstacleIndex++) {
                            Line2D obstacle = obstacleLines[onstacleIndex];
                            if (SomeMath.V2Cross(preferableVelocityX, preferableVelocityY, obstacle.leftX, obstacle.leftY) <= 0f &
                                SomeMath.V2Cross(preferableVelocityX, preferableVelocityY, obstacle.rightX, obstacle.rightY) >= 0f) {
                                float dirX = obstacle.rightX - obstacle.leftX;
                                float dirY = obstacle.rightY - obstacle.leftY;
                                if ((-obstacle.leftX * dirY + obstacle.leftY * dirX) / (preferableVelocityY * dirX - preferableVelocityX * dirY) < 1f) {
                                    targetObstructed = true;
                                    break;
                                }
                            }
                        }
#if UNITY_EDITOR
                        if (Debuger_K.debugRVO && Debuger_K.debugRVONavmeshClearance) {
                            for (int i = 0; i < obstacleLinesCount; i++) {
                                Line2D line = obstacleLines[i];
                                Vector3 a = new Vector3(line.leftX + agentPosition.x, agentPosition.y, line.leftY + agentPosition.z);
                                Vector3 b = new Vector3(line.rightX + agentPosition.x, agentPosition.y, line.rightY + agentPosition.z);
                                Debuger_K.debugSetLocalAvoidance.AddTriangle(agentPosition, a, b, new Color(1f, 0f, 0f, 0.25f), outline: false);
                                Debuger_K.debugSetLocalAvoidance.AddLine(a, b, new Color(1f, 0f, 0f, 1f));
                            }
                        }
#endif

                        if (targetObstructed) {
                            float borderLimitedVelocityX, borderLimitedVelocityY;
                            //apply normal lines
                            if (DoBorderMagic(vecVal, vecValCount, anyFreeVelocity, obstacleLines, obstacleLinesCount, agent.maxVelocity, 0.0001f, preferableVelocityX, preferableVelocityY, out borderLimitedVelocityX, out borderLimitedVelocityY, agentPosition)) {
                                preferableVelocityX = borderLimitedVelocityX;
                                preferableVelocityY = borderLimitedVelocityY;
                            }
                            else {
                                ///handle zero space velocity
                            }
                        }
                    }
                }
                #endregion

                Vector2 nearestPointOnShape = new Vector2(preferableVelocityX, preferableVelocityY);
                LAagents[agentIndex].safeVelocity = new Vector2(preferableVelocityX, preferableVelocityY);

                #region apply ORCA lines
#if UNITY_EDITOR
                if (Debuger_K.debugRVO && Debuger_K.debugRVObasic) {
                    Debuger_K.debugSetLocalAvoidance.AddLine(ToV3(nearestPointOnShape) + agentNavmeshSample.position, ToV3(agent.prefVelocity) + agentNavmeshSample.position, Color.cyan);
                    Debuger_K.debugSetLocalAvoidance.AddDot(ToV3(nearestPointOnShape) + agentNavmeshSample.position, Color.black);
                }
#endif
                #endregion

                //check if velocity are too small
                //if (agent.useDeadLockFailsafe && nearestPointOnShape.sqrMagnitude < SomeMath.Sqr(agent.deadLockFailsafeVelocity)) {
                //    agent.deadLockTriggeredTime = DateTime.Now;
                //}

                GenericPoolArray<VectorValue>.ReturnToPool(ref vecVal);
            }

            for (int agentIndex = 0; agentIndex < LAWorkAgentsCount; agentIndex++) {
                UnregisterNavmeshSample(LAWorkAgents[agentIndex].navmeshPositionSample);
            }

            GenericPoolArray<LocalAvoidanceAgentStruct>.ReturnToPool(ref LAWorkAgents);
            GenericPoolArray<LocalAvoidanceAgentDeadlock>.ReturnToPool(ref LAWorkAgentDeadlocks);

            GenericPoolArray<ORCAline>.ReturnToPool(ref ORCAlines);
            GenericPoolArray<int>.ReturnToPool(ref stackArray);
            GenericPoolArray<int>.ReturnToPool(ref treeResultArray);
            GenericPoolArray<LocalAvoidanceAgentStruct>.ReturnToPool(ref neighboursArray);
            GenericPoolArray<float>.ReturnToPool(ref neightboursDistanceArray);

            //navmesh border avoidance
            GenericPoolArray<bool>.ReturnToPool(ref globalCellsFlags);
            GenericPoolArray<int>.ReturnToPool(ref globalCellsUsed);
            GenericPoolArray<int>.ReturnToPool(ref cellQueue);
            GenericPoolArray<Line2D>.ReturnToPool(ref obstacleLines);

            ClearTree();
        }
        
        private static bool DoBorderMagic(VectorValue[] vecVal, int vecValCount, bool excludeVectorValueSpace,
            Line2D[] lines, int linesCount, float speedBorders, float sqrSnapDist, 
            float targetX, float targetY, 
            out float resultX, out float resultY, Vector3 agentPos) {
            VectorValue[] data = GenericPoolArray<VectorValue>.Take(32);
            int dataCount = 0;

            //Debuger_K.ClearGeneric();
            //Vector3 debugOffset = new Vector3(0f, 0.2f, 0f);
            sqrSnapDist = 0.0001f;

            //adding data or connecting to existed one
            for (int lineIndex = 0; lineIndex < linesCount; lineIndex++) {
                Line2D line = lines[lineIndex];
                int left = INVALID_VALUE;
                int right = INVALID_VALUE;

                for (int i = 0; i < dataCount; i++) {
                    if (left == INVALID_VALUE && data[i].SqrDistance(line.leftX, line.leftY) < sqrSnapDist)
                        left = i;
                    if (right == INVALID_VALUE && data[i].SqrDistance(line.rightX, line.rightY) < sqrSnapDist)
                        right = i;
                }

                if (right == INVALID_VALUE) {
                    if (data.Length == dataCount)
                        GenericPoolArray<VectorValue>.IncreaseSize(ref data);

                    data[dataCount].Set(line.rightX, line.rightY, INVALID_VALUE, VectorValueState.border);
                    right = dataCount++;
                }

                if (left == INVALID_VALUE) {
                    if (data.Length == dataCount)
                        GenericPoolArray<VectorValue>.IncreaseSize(ref data);
                    data[dataCount++].Set(line.leftX, line.leftY, right, VectorValueState.border);
                }
                else
                    data[left].next = right;
            }

            //figuring out borders of chain
            for (int d = 0; d < dataCount; d++) {
                int next = data[d].next;
                if (next != INVALID_VALUE)
                    data[next].SetNormalIfNextValid();
            }

            //for (int i = 0; i < dataCount; i++) {
            //    var d = data[i];
            //    if (d.next != -1) {
            //        var dd = data[d.next];
            //        Debuger_K.AddLine(ToV3(d.vector) + agentPos + debugOffset, ToV3(dd.vector) + agentPos + debugOffset, Color.cyan);
            //    }
            //    Debuger_K.AddDot(ToV3(d.vector) + agentPos + debugOffset, Color.blue);
            //    Debuger_K.AddLabelFormat(ToV3(d.vector) + agentPos + debugOffset, "i {0}, next {1}, state {2}", i, d.next, d.state);
            //}
            //debugOffset.y += 0.2f;

            int initialBorderCount = dataCount;
            for (int b = 0; b < initialBorderCount; b++) {
                var border = data[b];
                if (border.state != VectorValueState.border)
                    continue;

                int nearestHit = INVALID_VALUE;
                float nearestProduct = float.MaxValue;
                bool obstructed = false;
                int resetCounter = 0;

                for (int e = 0; e < dataCount; e++) {
                    if (e == b) continue;
                    VectorValue L = data[e];     //left
                    if (L.state == VectorValueState.connection || L.next == b || L.next == INVALID_VALUE) continue;
                    VectorValue R = data[L.next];//right

                    //check cross product for left and right point
                    float crossL = SomeMath.V2Cross(border.x, border.y, L.x, L.y);
                    float crossR = SomeMath.V2Cross(border.x, border.y, R.x, R.y);

                    //wops border exactly alighned with other points
                    //i can just connect them but nothing guarantie that row have only 2 points. 
                    //if 3 then this is disaster and i dont want to deal with it. so border just moved slightly and loop reseted
                    if (crossL == 0f | crossR == 0f) {
                        //resetting loop
                        e = 0;               
                        nearestHit = INVALID_VALUE;
                        nearestProduct = float.MaxValue;
                        obstructed = false;

                        //moving slightly border 
                        data[b].x += ((R.x - L.x) * SOME_SMALL_NUMBER);
                        data[b].y += ((R.y - L.y) * SOME_SMALL_NUMBER);
                        border = data[b];

                        resetCounter++;
                        if (resetCounter > 10) {
                            Debug.LogError("loop was reseted 10 times. probably something went wrong");
                            break;
                        }
                    }
                    //cause we only ever check L<0 R>0 there cant be back false results so checking dot product is not required
                    else if (crossL < 0f && crossR > 0f) {
                        //bare bones of line intersection code
                        //instead of distance we measure ray product to lines
                        //if product 1f then point laying on this ray direction point (which should never be in practice but FYI)
                        float dirX = R.x - L.x;
                        float dirY = R.y - L.y;
                        float product = (-L.x * dirY + L.y * dirX) / (border.y * dirX - border.x * dirY);

                        if (product > 1f) {
                            if (product < nearestProduct) {
                                nearestProduct = product;
                                nearestHit = e;
                            }
                        }
                        else {
                            obstructed = true;
                            break;
                        }
                    }
                }


                if (obstructed) {
                    data[b].state = VectorValueState.obstructed;
                }
                else {
                    //end point is not ibstructed by anything and have clear view to border
                    if (nearestHit == INVALID_VALUE) {
                        //adding dummy edges. edges added to direction clambed to quad
                        float absDirX = border.x;
                        float absDirY = border.y;
                        if (absDirX < 0f) absDirX *= -1f;
                        if (absDirY < 0f) absDirY *= -1f;
                        //we can know what side do clamp just by comparing absolute values
                        //cause ray anyway casted from quad center
                        float mul = (speedBorders * 1.25f) / (absDirX > absDirY ? absDirX : absDirY);

                        if (mul >= 1f) {//cause if it is this point outside max velocity anyway so dummy border not needed anyway  
                            if (data.Length == dataCount)

                                GenericPoolArray<VectorValue>.IncreaseSize(ref data);
                            if (border.next == INVALID_VALUE) {
                                data[b].next = dataCount;
                                data[dataCount].Set(border.x * mul, border.y * mul, INVALID_VALUE, VectorValueState.border);
                            }
                            else {
                                data[dataCount].Set(border.x * mul, border.y * mul, b, VectorValueState.border);
                            }
                            data[b].state = VectorValueState.normal;
                            dataCount++;
                        }
                    }
                    else {
                        if (data.Length < dataCount + 2)
                            GenericPoolArray<VectorValue>.IncreaseSize(ref data);

                        if (border.next == INVALID_VALUE) {
                            data[b].next = dataCount;
                            data[dataCount++].Set(border.x * nearestProduct, border.y * nearestProduct, data[nearestHit].next, VectorValueState.connection);
                            data[nearestHit].next = dataCount;
                            data[dataCount++].Set(border.x * nearestProduct, border.y * nearestProduct, INVALID_VALUE, VectorValueState.invalid);
                        }
                        else {
                            data[dataCount++].Set(border.x * nearestProduct, border.y * nearestProduct, data[nearestHit].next, VectorValueState.obstructed);
                            data[nearestHit].next = dataCount;
                            data[dataCount++].Set(border.x * nearestProduct, border.y * nearestProduct, b, VectorValueState.connection);
                        }
                        data[b].state = VectorValueState.normal;
                    }
                }
            }

            //for (int i = 0; i < dataCount; i++) {
            //    var d = data[i];
            //    if (d.next != -1) {
            //        var dd = data[d.next];
            //        Debuger_K.AddLine(ToV3(d.vector) + agentPos + debugOffset, ToV3(dd.vector) + agentPos + debugOffset, Color.cyan);
            //    }
            //    Debuger_K.AddDot(ToV3(d.vector) + agentPos + debugOffset, Color.blue);
            //    Debuger_K.AddLabelFormat(ToV3(d.vector) + agentPos + debugOffset, "i {0}, next {1}, state {2}", i, d.next, d.state);
            //}
            //debugOffset.y += 0.2f;

            //converting unseen edges to invalid
            //****THIS CODE CAN BE INVINITE LOOP****
            //loop case - if next == current index. this can occure if for some reason sqrSnapDist is small enough that it just connect one dot to itself
            //OR if edge sampled inside excluded area and it connect to it shell 
            //so in order it to not ever connect be sure not used cell ever sample edges
            if (excludeVectorValueSpace) {        
                for (int d = 0; d < dataCount; d++) {
                    VectorValue value = data[d];
                    if (value.state == VectorValueState.obstructed) {
                        for (int index = d; index != INVALID_VALUE; index = data[index].next) {                        
                            data[index].state = VectorValueState.invalid;                
                        }
                    }
                }

                for (int i = 0; i < vecValCount; i++) {
                    var vec0 = vecVal[i];
                    if (vec0.state != VectorValueState.normal) continue;
                    var vec1 = vecVal[vec0.next];

                    float dirX = vec1.x - vec0.x;
                    float dirY = vec1.y - vec0.y;

                    for (int d = 0; d < dataCount; d++) {
                        VectorValue curData = data[d];
                        if (curData.state > 0) {
                            if (SomeMath.V2Cross(dirX, dirY, curData.x - vec0.x, curData.y - vec0.y) < 0f)
                                data[d].state = VectorValueState.normal;//we dont need previos positive states so they just seted as normal
                            else
                                data[d].state = VectorValueState.invalid;
                        }
                    }

                    for (int d = 0; d < dataCount; d++) {
                        VectorValue d0 = data[d];
                        if (d0.next != INVALID_VALUE) {
                            VectorValue d1 = data[d0.next];
                            if (d0.state != VectorValueState.normal & d0.state != VectorValueState.invalid)
                                Debug.LogError(d0.state);
                            if (d1.state != VectorValueState.normal & d1.state != VectorValueState.invalid)
                                Debug.LogError(d1.state);

                            if ((int)d0.state + (int)d1.state == 1) {
                                float ix, iy;
                                SomeMath.LineIntersection(vec0.x, vec0.y, vec1.x, vec1.y, d0.x, d0.y, d1.x, d1.y, out ix, out iy);

                                //one side is valid and other have irelevant cross. this edge should be separated
                                if (data.Length == dataCount)
                                    GenericPoolArray<VectorValue>.IncreaseSize(ref data);

                                if (d0.state == VectorValueState.normal) {
                                    data[d].next = dataCount;
                                    data[dataCount++].Set(ix, iy, INVALID_VALUE, VectorValueState.normal);
                                }
                                else {
                                    data[d].next = INVALID_VALUE;
                                    data[dataCount++].Set(ix, iy, d0.next, VectorValueState.normal);
                                }
                            }
                            else if ((int)d0.state + (int)d1.state != 2 & (int)d0.state + (int)d1.state != 0) {
                                Debug.LogError((int)d0.state + (int)d1.state + " i:" + d + " : " + d0.state + " : " + d1.state);
                            }
                        }
                    }
                }
            }
            
            //now search for closest position to target point
            float sqrDist = float.MaxValue;
            resultX = targetX;
            resultY = targetY;
            bool anyResult = false;
            for (int i = 0; i < dataCount; i++) {
                VectorValue d0 = data[i];
                if (d0.state != VectorValueState.invalid && d0.next != INVALID_VALUE) {
                    VectorValue d1 = data[d0.next];
                    float px, py;
                    SomeMath.NearestPointToSegment(d0.x, d0.y, d1.x, d1.y, targetX, targetY, out px, out py);
                    float curSqrDist = SomeMath.SqrDistance(targetX, targetY, px, py);

  

                    if (curSqrDist < sqrDist) {
                        sqrDist = curSqrDist;
                        resultX = px + ((d1.y - d0.y) * SOME_SMALL_NUMBER);
                        resultY = py - ((d1.x - d0.x) * SOME_SMALL_NUMBER);
                        anyResult = true;
                    }
                }
            }

#if UNITY_EDITOR
            if (Debuger_K.debugRVO && Debuger_K.debugRVObasic) {
                for (int i = 0; i < dataCount; i++) {
                    VectorValue d0 = data[i];
                    if (d0.state != VectorValueState.invalid && d0.next != INVALID_VALUE) {
                        VectorValue d1 = data[d0.next];
                        Debuger_K.debugSetLocalAvoidance.AddLine(ToV3(d0.vector) + agentPos, ToV3(d1.vector) + agentPos, Color.cyan);
                    }
                }
            }
#endif
            GenericPoolArray<VectorValue>.ReturnToPool(ref data);
            return anyResult;
        }

#region plane intersection
        /// <summary>
        /// solve plane intersection to return first avaible velocity in case when avaible velocity area is zero
        /// kinda long and hard to understand. short description:
        /// when orca lines enclose all space then this function are used. Is solve 3 cases:
        /// 1) When it is only one line enclose all space. In this case sellected point are closest to nearest point of this line. 
        /// It will be first avaiable point if this line are moved in opposite direction
        /// 2) when 2 lines enclose all space. there is 2 cases:
        /// a) Lines are paralel. In this case sellected farthest one which enclose visible space at all
        /// b) Lines are NOT paralel. In this case sellected first visible point if lines are moved to opposite directions
        /// 3) when there is more than 2 lines. In this case siquentialy will be sellected 3 planes, check where intersection
        /// of them represented as lines. And THEN checked intersection of those lines. If this point are not enclosed by other planes in is siutable one.
        /// lowest point will be result. it will be first avaible poine if orca lines moved in opposite direction.
        /// </summary>
        private static bool SolvePlanesIntersections(ORCAline[] orcaArray, int orcaCount, Vector3 agentPos, float maxVelocity, out Vector2 result) {
#if UNITY_EDITOR
            if (Debuger_K.debugRVO && Debuger_K.debugRVOplaneIntersections) {
                for (int i = 0; i < orcaCount; i++) {
                    DrawPlaneORCA(orcaArray[i], agentPos);
                }
            }
#endif

            bool haveResult = false;
            float maxVelocitySqr = maxVelocity * maxVelocity;

            switch (orcaCount) {
                case 1: {
                        ORCAline orcaLine = orcaArray[0];
                        float orcaDirX = -orcaLine.normalY;
                        float orcaDirY = orcaLine.normalX;
                        float orcaLineMagnitude = SomeMath.Magnitude(orcaDirX, orcaDirY);

                        orcaDirX /= orcaLineMagnitude;
                        orcaDirY /= orcaLineMagnitude;

                        float multiplier = SomeMath.Dot(-orcaLine.positionX, -orcaLine.positionY, orcaDirX, orcaDirY);
                        result = new Vector2(
                            orcaLine.positionX + orcaDirX * multiplier,
                            orcaLine.positionY + orcaDirY * multiplier);

                        if (SomeMath.SqrMagnitude(result) > maxVelocitySqr)
                            result = result.normalized * maxVelocity;
                        //Debuger_K.AddLine(agent.positionVector3, agent.positionVector3 + ToV3(result), Color.green);
                        return true;
                    }
                case 2: {
                        ORCAline orcaA = orcaArray[0];
                        ORCAline orcaB = orcaArray[1];
                        Vector3 normalA = GetNormal3d(orcaA);
                        Vector3 normalB = GetNormal3d(orcaB);
                        Vector3 intPos, intDir;
                        if (Math3d.PlanePlaneIntersection(out intPos, out intDir, normalA, SomeMath.ToVector3(orcaA.position), normalB, SomeMath.ToVector3(orcaB.position))) {
#if UNITY_EDITOR
                            if (Debuger_K.debugRVO && Debuger_K.debugRVOplaneIntersections) {
                                Debuger_K.debugSetLocalAvoidance.AddRay(agentPos + intPos, intDir, Color.red, 50f);
                                Debuger_K.debugSetLocalAvoidance.AddRay(agentPos + intPos, intDir, Color.red, -50f);
                                Debuger_K.debugSetLocalAvoidance.AddLine(DrawCircle(50, agentPos, maxVelocity), Color.black, true);
                                Debuger_K.debugSetLocalAvoidance.AddRay(agentPos + new Vector3(intPos.x, 0, intPos.z), new Vector3(intDir.x, 0, intDir.z), Color.red, 50f);
                                Debuger_K.debugSetLocalAvoidance.AddRay(agentPos + new Vector3(intPos.x, 0, intPos.z), new Vector3(intDir.x, 0, intDir.z), Color.red, -50f);
                            }
#endif

                            if (intDir.y > 0)//this vector should point down
                                intDir *= -1;

                            float dirX = intDir.x;
                            float dirY = intDir.z;

                            float ABmagnitude = SomeMath.Magnitude(dirX, dirY);
                            if (ABmagnitude == 0f) {//length of line are 0
                                result = new Vector2(intPos.x, intPos.y).normalized * maxVelocity;
                                return true;
                            }

                            dirX = dirX / ABmagnitude;
                            dirY = dirY / ABmagnitude;

                            float mul = SomeMath.Dot(-intPos.x, -intPos.z, dirX, dirY);
                            //float nearestX = intPos.x + dirX * mul;
                            //float nearestY = intPos.z + dirY * mul;
                            Vector2 nearest = new Vector2(intPos.x + dirX * mul, intPos.z + dirY * mul);

                            float nearestMagnitude = SomeMath.Magnitude(nearest);
            
                            if (nearestMagnitude >= maxVelocity) {
                                result = nearest.normalized * maxVelocity;
                            }
                            else {
                                float vectorLength = Mathf.Sqrt(SomeMath.Sqr(maxVelocity) - SomeMath.Sqr(nearestMagnitude)); //hail to pythagoras
                                result = new Vector2(nearest.x, nearest.y) + (new Vector2(intDir.x, intDir.z).normalized * vectorLength);
                            }

                            //Debuger_K.AddDot(offset + new Vector3(nearest.x, 0, nearest.y), Color.magenta);
                            //Debuger_K.AddDot(offset + new Vector3(result.x, 0, result.y), Color.magenta);
                            //Debuger_K.AddLine(agent.positionVector3, agent.positionVector3 + ToV3(result), Color.green);            
                        }
                        else {
                            //orca normals are paralel. this is some kind of unicorn case but it still possible
                            //at least one node is outside. now we must check bouth nodes to see if bouth nodes look outside.
                            //if bouth then sellect farthest one
                            //else sellect one pointing outside

                            float dotOfA = orcaA.Dot();
                            float dotOfB = orcaB.Dot();
                            ORCAline targetOrca;

                            if (dotOfA > 0) {
                                if(dotOfB > 0 && SomeMath.SqrMagnitude(orcaA.position) < SomeMath.SqrMagnitude(orcaB.position))
                                    targetOrca = orcaB;
                                else
                                    targetOrca = orcaA;
                            }
                            else if (dotOfB > 0) {
                                targetOrca = orcaB;
                            }
                            else {
                                //how did this happen? 2 lines, no space and they all look inwards and paralel?
                                Debug.LogWarningFormat("how did this happen? Line1: {0}, dot {2}, Line2: {1} dot {3}", orcaA, orcaB, dotOfA, dotOfB);
                                targetOrca = orcaA;
                            }

                            Vector2 targetOrcaPos = targetOrca.position;
                            Vector2 targetOrcaDir = targetOrca.normal;

                            float dirXcase2 = -targetOrcaDir.y;
                            float dirYcase2 = targetOrcaDir.x;

                            float case2ABmagnitude = SomeMath.Magnitude(dirXcase2, dirYcase2);

                            dirXcase2 /= case2ABmagnitude;
                            dirYcase2 /= case2ABmagnitude;

                            float case2mul = SomeMath.Dot(-targetOrcaPos.x, -targetOrcaPos.y, dirXcase2, dirYcase2);
                            float case2nearestX = targetOrcaPos.x + dirXcase2 * case2mul;
                            float case2nearestY = targetOrcaPos.y + dirYcase2 * case2mul;
                            result = new Vector2(case2nearestX, case2nearestY);
                            if (SomeMath.SqrMagnitude(result) > maxVelocitySqr)
                                result = result.normalized * maxVelocity;

                            //Debuger_K.AddLine(agent.positionVector3, agent.positionVector3 + ToV3(result), Color.green);
                        }
                        return true;
                    }
                default: {//OCRA LINES COUNT > 3                    
                        float resultX, resultY, resultZ;
                        resultX = resultY = resultZ = 0;
                        for (int i1 = 0; i1 < orcaCount; i1++) {//grab 1 vector
                            ORCAline line1 = orcaArray[i1];
                            Vector3 line1normal3d = GetNormal3d(line1);

                            for (int i2 = i1; i2 < orcaCount; i2++) {
                                if (i2 == i1)
                                    continue;

                                ORCAline line2 = orcaArray[i2];
                                Vector3 line2normal3d = GetNormal3d(line2);

                                for (int i3 = i1; i3 < orcaCount; i3++) {
                                    if (i3 == i1 | i3 == i2)
                                        continue;

                                    ORCAline line3 = orcaArray[i3];
                                    Vector3 line3normal3d = GetNormal3d(line3);

                                    Vector3 int12pos, int12dir, int13pos, int13dir;
                                    if (Math3d.PlanePlaneIntersection(out int12pos, out int12dir, line1normal3d, SomeMath.ToVector3(line1.position), line2normal3d, SomeMath.ToVector3(line2.position)) &
                                        Math3d.PlanePlaneIntersection(out int13pos, out int13dir, line1normal3d, SomeMath.ToVector3(line1.position), line3normal3d, SomeMath.ToVector3(line3.position))) {

                                        //Debuger_K.AddRay(offset + int12pos, int12dir, Color.red, 50f);
                                        //Debuger_K.AddRay(offset + int12pos, int12dir, Color.red, -50f);
                                        //Debuger_K.AddRay(offset + int13pos, int13dir, Color.red, 50f);
                                        //Debuger_K.AddRay(offset + int13pos, int13dir, Color.red, -50f);

                                        Vector3 intersection;
                                        if (LineLineIntersection(out intersection, int12pos, int12dir, int13pos, int13dir)) {
                                            bool flag = true;

                                            for (int i4 = 0; i4 < orcaCount; i4++) {
                                                if (i4 == i1 | i4 == i2 | i4 == i3)
                                                    continue;

                                                ORCAline line4 = orcaArray[i4];
                                                Vector3 line4normal3d = GetNormal3d(line4);

                                                Vector3 intersectionLocal = intersection - SomeMath.ToVector3(line4.position);

                                                float dot = SomeMath.Dot(intersectionLocal, line4normal3d);

                                                if (dot < 0) {
                                                    flag = false;
                                                    break;
                                                }
                                            }

                                            if (flag) {
                                                //Debuger_K.AddDot(offset + intersection, Color.magenta);

                                                if (haveResult) {
                                                    if (resultY > intersection.y) {
                                                        resultX = intersection.x;
                                                        resultY = intersection.y;
                                                        resultZ = intersection.z;
                                                    }
                                                }
                                                else {
                                                    haveResult = true;
                                                    resultX = intersection.x;
                                                    resultY = intersection.y;
                                                    resultZ = intersection.z;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        result = new Vector2(resultX, resultZ);
                        if (SomeMath.SqrMagnitude(result) > maxVelocitySqr)
                            result = result.normalized * maxVelocity;
                        break;
                    }
            }

            //if (haveResult) {
            //    if(DEBUG_VECTOR.HasValue == false)
            //        DEBUG_VECTOR = result;
            //}
            //if (haveResult)
            //    Debuger_K.AddDot(offset + new Vector3(resultX, 0, resultZ), Color.cyan, 0.05f);


            return haveResult;
        }

        private static Vector3 GetNormal3d(ORCAline line) {
            float rad = (Mathf.Clamp01(line.responcibility) * 90f) * Mathf.Deg2Rad;
            float x = Mathf.Cos(rad);
            return new Vector3(line.normalX * x, Mathf.Sin(rad), line.normalY * x);
        }

#if UNITY_EDITOR
        private static void DrawPlaneORCA(ORCAline line, Vector3 offset) {
            //float angle = Mathf.Clamp01(line.responcibility) * 90f;
            //float rad = angle * Mathf.Deg2Rad;

            Vector2 normal = line.normal;
            Vector2 position = line.position;

            //float x = Mathf.Cos(rad);
            //float y = Mathf.Sin(rad);

            //Vector2 normalTrim = normal * x;

            Vector3 normalV3 = GetNormal3d(line);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            Debuger_K.debugSetLocalAvoidance.AddDot(offset + positionV3, Color.green, 0.1f);
            Debuger_K.debugSetLocalAvoidance.AddRay(offset + positionV3, normalV3, Color.green);

            //Vector2 leftV2 = new Vector2(-normal.y, normal.x);
            Vector3 leftV3 = new Vector3(-normal.y, 0, normal.x);

            //Vector2 rightV2 = new Vector2(normal.y, -normal.x);
            Vector3 rightV3 = new Vector3(normal.y, 0, -normal.x);

            Vector3 crossL = Vector3.Cross(normalV3, leftV3);
            Vector3 crossR = Vector3.Cross(normalV3, rightV3);

            //Debuger_K.AddRay(offset + positionV3, leftV3, Color.blue);
            //Debuger_K.AddRay(offset + positionV3, rightV3, Color.blue);
            //Debuger_K.AddRay(offset + positionV3, crossL, Color.blue);
            //Debuger_K.AddRay(offset + positionV3, crossR, Color.blue);

            Vector3 a = offset + positionV3 + DrawPlaneORCA_shortcut(leftV3, crossL);
            Vector3 b = offset + positionV3 + DrawPlaneORCA_shortcut(rightV3, crossL);
            Vector3 c = offset + positionV3 + DrawPlaneORCA_shortcut(rightV3, crossR);
            Vector3 d = offset + positionV3 + DrawPlaneORCA_shortcut(leftV3, crossR);

            Debuger_K.debugSetLocalAvoidance.AddLine(offset + positionV3, a, Color.red);
            Debuger_K.debugSetLocalAvoidance.AddLine(offset + positionV3, b, Color.red);
            Debuger_K.debugSetLocalAvoidance.AddLine(offset + positionV3, c, Color.red);
            Debuger_K.debugSetLocalAvoidance.AddLine(offset + positionV3, d, Color.red);

            Debuger_K.debugSetLocalAvoidance.AddTriangle(a, b, c, new Color(0f, 0f, 1f, 0.1f));
            Debuger_K.debugSetLocalAvoidance.AddTriangle(a, c, d, new Color(0f, 0f, 1f, 0.1f));
            Debuger_K.debugSetLocalAvoidance.AddLine(a, b);
            Debuger_K.debugSetLocalAvoidance.AddLine(b, c);
            Debuger_K.debugSetLocalAvoidance.AddLine(c, d);
            Debuger_K.debugSetLocalAvoidance.AddLine(d, a);
        }
#endif

        private static Vector3 DrawPlaneORCA_shortcut(Vector3 A, Vector3 B) {
            return ((A + B) * 0.5f) * 2;
        }

        public static bool LineLineIntersection(
            out Vector3 intersection,
            Vector3 linePoint1, Vector3 lineVec1,
            Vector3 linePoint2, Vector3 lineVec2) {

            intersection = Vector3.zero;

            Vector3 lineVec3 = linePoint2 - linePoint1;
            Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
            Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

            float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

            //Lines are not coplanar. Take into account rounding errors.
            if ((planarFactor >= 0.00001f) || (planarFactor <= -0.00001f)) {
                return false;
            }

            //Note: sqrMagnitude does x*x+y*y+z*z on the input vector.
            float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
            intersection = linePoint1 + (lineVec1 * s);
            return true;
        }
        #endregion

        #region Tree
        //copy paste from Generic2dBoundsTreePureIndexes
        static AgentTreeValue[] sortableAgents;
        static int sortableAgentsCount;
        static kDTreeBoundsBranch[] tree;
        static int treeCount = 0;
        static int treeRoot;
        
        enum TreeSplitAxis : int {
            X = 0,
            Z = 1,
            END = 2
        }
        struct kDTreeBoundsBranch {
            public int start, end, branchLow, branchHigh;
            public float minX, minZ, maxX, maxZ;
            public TreeSplitAxis splitAxis;

            public kDTreeBoundsBranch(int start, int end, int branchLow, int branchHigh, float minX, float minY, float maxX, float maxY, TreeSplitAxis splitAxis) {
                this.start = start;
                this.end = end;
                this.branchLow = branchLow;
                this.branchHigh = branchHigh;
                this.minX = minX;
                this.minZ = minY;
                this.maxX = maxX;
                this.maxZ = maxY;
                this.splitAxis = splitAxis;
            }

            public Vector3 center {
                get { return new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2); }
            }

            public override string ToString() {
                return string.Format("BL {0}, BH {1}, S{2}, E{3}, minX{4}, minZ{5}, maxX{6}, maxZ{7}", branchLow, branchHigh, start, end, minX, minZ, maxX, maxZ);
            }
        }
        
        static void BuildTree(int membersPerBranch) {
            if (tree != null)
                GenericPoolArray<kDTreeBoundsBranch>.ReturnToPool(ref tree);
            tree = GenericPoolArray<kDTreeBoundsBranch>.Take(32);
            treeCount = 0;
            treeRoot = BuildRecursiveTree(0, sortableAgentsCount - 1, membersPerBranch);
        }

        static void ClearTree() {
            GenericPoolArray<AgentTreeValue>.ReturnToPool(ref sortableAgents);
            GenericPoolArray<kDTreeBoundsBranch>.ReturnToPool(ref tree);
        }

        static int BuildRecursiveTree(int leftStart, int rightStart, int membersPerBranch) {
            int count = rightStart - leftStart;
            float minX, minZ, maxX, maxZ;

            minX = maxX = sortableAgents[leftStart].x;
            minZ = maxZ = sortableAgents[leftStart].z;

            for (int i = leftStart + 1; i <= rightStart; i++) {
                if (sortableAgents[i].x < minX) minX = sortableAgents[i].x;
                if (sortableAgents[i].z < minZ) minZ = sortableAgents[i].z;
                if (sortableAgents[i].x > maxX) maxX = sortableAgents[i].x;
                if (sortableAgents[i].z > maxZ) maxZ = sortableAgents[i].z;
            }

            int L = -1;
            int H = -1;
            TreeSplitAxis axis = TreeSplitAxis.END;

            if (count > membersPerBranch) {
                int left = leftStart;
                int right = rightStart;
                float pivot = 0f;

                if ((maxX - minX) > (maxZ - minZ)) {//deside how to split. if size X > size Z then X            
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += sortableAgents[i].x;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (sortableAgents[left].x < pivot) left++;
                        while (sortableAgents[right].x > pivot) right--;
                        if (left <= right) {
                            AgentTreeValue tempData = sortableAgents[left];
                            sortableAgents[left++] = sortableAgents[right];
                            sortableAgents[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.X;
                }
                else {//z
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += sortableAgents[i].z;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (sortableAgents[left].z < pivot) left++;
                        while (sortableAgents[right].z > pivot) right--;
                        if (left <= right) {
                            AgentTreeValue tempData = sortableAgents[left];
                            sortableAgents[left++] = sortableAgents[right];
                            sortableAgents[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.Z;
                }

                L = BuildRecursiveTree(leftStart, right, membersPerBranch);
                H = BuildRecursiveTree(left, rightStart, membersPerBranch);
            }

            if (tree.Length == treeCount)
                GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref tree);

            tree[treeCount] = new kDTreeBoundsBranch(leftStart, rightStart, L, H, minX, minZ, maxX, maxZ, axis);
            return treeCount++;
        }

        static void TreeSearch(float minX, float maxX, float minZ, float maxZ, ref int[] result, out int resultCount, ref int[] iterationStack) {
            resultCount = 0;
            int iterationStackLength = 1;
            iterationStack[0] = treeRoot;

            while (iterationStackLength != 0) {
                kDTreeBoundsBranch branch = tree[iterationStack[--iterationStackLength]];
                if (branch.minX <= maxX && branch.maxX >= minX &&
                    branch.minZ <= maxZ && branch.maxZ >= minZ) {

                    if (branch.splitAxis == TreeSplitAxis.END) {
                        for (int i = branch.start; i < branch.end + 1; i++) {
                            AgentTreeValue val = sortableAgents[i];
                            if (val.x >= minX && val.x <= maxX && val.z >= minZ && val.z <= maxZ) {
                                if (result.Length == resultCount)
                                    GenericPoolArray<int>.IncreaseSize(ref result);
                                result[resultCount++] = i;
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
        }

#if UNITY_EDITOR
        static void DebugBoundsTree(DebugSet set) {
            DebugRecursive(treeRoot, 0, set);
        }

        static void DebugRecursive(int target, int depth, DebugSet set) {
            float height = depth;
            //float height = 0;
            if (tree == null)
                return;

            var branch = tree[target];

            System.Random rand = new System.Random();

            Color color = new Color(
                rand.Next(0, 255) / 255f,
                rand.Next(0, 255) / 255f,
                rand.Next(0, 255) / 255f, 1f);

            Vector3 v1 = new Vector3(branch.minX, height, branch.minZ);
            Vector3 v2 = new Vector3(branch.minX, height, branch.maxZ);
            Vector3 v3 = new Vector3(branch.maxX, height, branch.maxZ);
            Vector3 v4 = new Vector3(branch.maxX, height, branch.minZ);
            set.AddLine(v1, v2, color);
            set.AddLine(v2, v3, color);
            set.AddLine(v3, v4, color);
            set.AddLine(v4, v1, color);

            if (branch.splitAxis == TreeSplitAxis.END) {
                Vector3 center = branch.center;
                center = new Vector3(center.x, height, center.z);
                set.AddLabel(center, branch.end - branch.start + 1);
                for (int i = branch.start; i <= branch.end; i++) {
                    AgentTreeValue memder = sortableAgents[i];
                    set.AddLine(center, new Vector3(memder.x, height, memder.z), Color.blue, 0.0025f);
                    set.AddDot(new Vector3(memder.x, height, memder.z), Color.green, 0.1f);
                }
            }
            else {
                DebugRecursive(branch.branchLow, depth + 1, set);
                DebugRecursive(branch.branchHigh, depth + 1, set);
            }
        }
#endif
        #endregion



        #region things
        private static Vector3 CondenceVector(Vector2 vectorXY, float Z) {
            return new Vector3(vectorXY.x, vectorXY.y, Z);
        }

        private static Vector2 RadToVector(float radian, float length) {
            return new Vector2(Mathf.Cos(radian) * length, Mathf.Sin(radian) * length);
        }

        static Vector2 RadToVector(float radian) {
            return new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));
        }

        static Vector3[] DrawCircle(int value, Vector3 position, float radius) {
            Vector3[] result = new Vector3[value];
            for (int i = 0; i < value; ++i) {
                result[i] = new Vector3(
                    (float)Math.Cos(i * 2.0f * Math.PI / value) * radius + position.x,
                    position.y,
                    (float)Math.Sin(i * 2.0f * Math.PI / value) * radius + position.z);
            }
            return result;
        }
#endregion
    }
}