using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {
    [RequireComponent(typeof(PathFinderAgent), typeof(CharacterController))]
    public class Example17Agent : MonoBehaviour {
        [Range(0f, 5f)] public float speed = 3;
    
        public LineRenderer line;
        public Transform moveTarget;
        public float interactionDistance = 1f;
        [LayerPF]
        public BitMaskPF layerMaskForSearchOnDisabledArea = 3;
        
        private CharacterController controler;
        private PathFinderAgent agent;
        private NavMeshPointQuery<DoorHandle> doorHandlesQuery;
        private Vector3? currentMoveTarget;
        private DoorHandle curTargetHandle;
        
        void Start() {
            controler = GetComponent<CharacterController>();
            agent = GetComponent<PathFinderAgent>();            
            agent.SetRecievePathDelegate(RecievePathDelegate, AgentDelegateMode.ThreadSafe);
            doorHandlesQuery = new NavMeshPointQuery<DoorHandle>(agent.properties);
            doorHandlesQuery.SetOnFinishedDelegate(RecieveDoorHandlesDelegate, AgentDelegateMode.ThreadSafe);
            currentMoveTarget = moveTarget.position;
        }
        
        void Update() {
            if(currentMoveTarget.HasValue)
                agent.SetGoalMoveHere(
                    currentMoveTarget.Value,                   
                    layerMask: layerMaskForSearchOnDisabledArea, 
                    costModifierMask: 0,
                    collectPathContent: true);             

            //if we found some handle to poll 
            if (curTargetHandle != null && Vector3.Distance(transform.position, curTargetHandle.position) < interactionDistance) {
                currentMoveTarget = null; //agent waits for door to open
                curTargetHandle.door.sceneDoor.UseHandle(() => { currentMoveTarget = moveTarget.position; }); //when door open it uses that callback to set path target to it's original position
                curTargetHandle = null;
            }

            controler.SimpleMove(new Vector3(agent.safeVelocity.x, 0, agent.safeVelocity.y));

            agent.preferableVelocity = Vector2.zero;
            agent.velocity = agent.safeVelocity;

            if (agent.haveNextNode == false)
                return;

            //remove next node if closer than radius in top projection. there is other variants of this function
            agent.RemoveNextNodeIfCloserSqrVector2(0.05f);

            //if next point still exist then move towards it
            if (agent.haveNextNode) {
                Vector2 moveDirection = agent.nextNodeDirectionVector2.normalized;
                agent.preferableVelocity = agent.nextNodeDirectionVector2.normalized * speed;
            }
        }

        void RecievePathDelegate(Path path) {
            if (path.pathType != PathResultType.Valid) {
                Debug.LogWarningFormat("path is not valid. reason: {0}", path.pathType);
                return;
            }

            ExampleThings.PathToLineRenderer(agent.positionVector3, line, path, 0.2f);

            CellPathContentDoorData doorControlerThatBlocksPath = null;

            var pathContent = path.pathContent; //List of content
            for (int i = 0; i < pathContent.Count; i++) {
                if (pathContent[i] is CellPathContentDoorData) {
                    CellPathContentDoorData doorControler = pathContent[i] as CellPathContentDoorData;
                    if (doorControler.closed) {
                        doorControlerThatBlocksPath = doorControler;
                        currentMoveTarget = null; //stop movement
                        break;
                    }
                }
            }

            if (doorControlerThatBlocksPath != null && doorControlerThatBlocksPath.closed) {
                //search for door handle if it blocks path
                doorHandlesQuery.QueueWork(transform.position, 100f, predicate: (DoorHandle dh) => { return dh.door == doorControlerThatBlocksPath; });
            }
        }

        void RecieveDoorHandlesDelegate(List<PointQueryResult<DoorHandle>> handles) {
            if (handles.Count == 0) {
                Debug.LogWarning("Agent did not find any handle to pull. he realy need to open this door");
            }
            else {
                curTargetHandle = handles[0].value;
                currentMoveTarget = handles[0].value.position;
            }
        }
    }
}