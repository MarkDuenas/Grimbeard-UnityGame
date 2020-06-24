using K_PathFinder.CoverNamespace;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {
    [RequireComponent(typeof(CharacterController), typeof(PathFinderAgent))]
    public class Example13 : MonoBehaviour {
        public LineRenderer line;
        PathFinderAgent agent;

        CharacterController controler;

        //cached positions
        Vector3 targetPosition;
        public NicePointer nicePointer; //simple class to point somwhere 

        [Range(10f, 180f)] public float angleThreshold = 45f;
        [Range(0f, 5f)] public float speed = 3;
        public float searchMaxCost = 100f;

        public bool follow = true;
        public bool stand = true;

        // Use this for initialization
        void Start() {
            controler = GetComponent<CharacterController>();

            //setting agent and delegates
            agent = GetComponent<PathFinderAgent>();    
            agent.SetRecieveCoverDelegate(RecieveCoverDelegate);
            agent.SetRecievePathDelegate(RecivePathDelegate, AgentDelegateMode.ThreadSafe);

#if UNITY_EDITOR
            //agent.queryCover.debug = true;
#endif
        }

        // Update is called once per frame
        void Update() {
            targetPosition = nicePointer.positionTop; //NOTE: here used top of pointer (it sphere) so too close position will be also ignored. 

            //order cover search
            agent.SetGoalFindCover(searchMaxCost);

            if (stand)
                return;

            if (agent.haveNextNode) {
                //remove next node if closer than radius in top projection. there is other variants of this function
                //in this example we kinda need more precision at target position so we always retain last node 
                agent.RemoveNextNodeIfCloserThanRadiusVector2(true);

                //if next point still exist then we move towards it    
                if (agent.haveNextNode) {
                    Vector2 moveDirection = agent.nextNodeDirectionVector2;

                    //if it is last point and if it is near then slow down proportionaly to distance to that point
                    if (agent.path.count == 1 && moveDirection.magnitude < speed)
                        moveDirection = moveDirection * speed;
                    else
                        moveDirection = moveDirection.normalized * speed;
                    
                    //in the end we have direction to next node and if it close enough agent will slow down near it
                    controler.SimpleMove(new Vector3(moveDirection.x, 0, moveDirection.y));
                }
            }
        }

        //here all recieved points iterated and sellected one that most userful
        void RecieveCoverDelegate(IEnumerable<PointQueryResult<NodeCoverPoint>> coverCollection) {
            if (follow) {
                agent.SetGoalMoveHere(nicePointer.transform.position, applyRaycast: true);
                return;
            }

            float smallestCost = float.MaxValue;
            NodeCoverPoint closestCoverPoint = null;

            foreach (var coverPoint in coverCollection) {
                //all covers have normal that point in direction from cover
                //here direction to target testet against cover normal
                Vector3 directionToTarget = coverPoint.value.positionV3 - targetPosition;
                float directionCoverNormalAngle = Vector3.Angle(directionToTarget, coverPoint.value.normalV3);

#if UNITY_EDITOR
                if (agent.queryCover.debug) {
                    PFDebuger.Debuger_K.AddLabel(coverPoint.value.positionV3 + (Vector3.up * 0.1f), directionCoverNormalAngle);
                    Color color = directionCoverNormalAngle < angleThreshold ? Color.green : Color.red;
                    PFDebuger.Debuger_K.AddDot(coverPoint.value.positionV3 + (Vector3.up * 0.1f), color, 0.1f);
                    PFDebuger.Debuger_K.AddRay(coverPoint.value.positionV3 + (Vector3.up * 0.1f), directionToTarget, color);
                }
#endif

                //only covers that will cover against target should be further checked
                if (directionCoverNormalAngle < angleThreshold) {
                    //check for closest by move cost          
                    if (coverPoint.cost < smallestCost) {
                        smallestCost = coverPoint.cost;
                        closestCoverPoint = coverPoint.value;
                    }
                }
            }

            //here are setted where to move       
            if (closestCoverPoint != null) {
                agent.SetGoalMoveHere(closestCoverPoint.positionV3);

#if UNITY_EDITOR
                if (agent.queryCover.debug)         
                    PFDebuger.Debuger_K.AddRay(closestCoverPoint.positionV3 + (Vector3.up * 0.1f), Vector3.up, Color.blue);                
#endif
            }
        }

        //Debug and checks handling
        private void RecivePathDelegate(Path path) {
            if (path.pathType != PathResultType.Valid)
                Debug.LogWarningFormat("path is not valid. reason: {0}", path.pathType);

            ExampleThings.PathToLineRenderer(agent.transform.position, line, path, 0.5f);
        }

    }
}