using K_PathFinder.Graphs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {    
    [RequireComponent(typeof(PathFinderAgent))]
    public class Example3 : MonoBehaviour {
        public Camera myCamera;               //camera reference
        public LineRenderer lineRenderer;     //line renderrer to show path
        public PathFinderAgent myAgent;       //reference to agent
        public GameObject targetGameObject;   //reference to target
        private bool update;                  //if true then update path  
        
        void Start() {
            //you can specify callback delegate so agent execute some function when it recieve path
            //you can sellect ThreadSafe option. in this case delegate will be called when agent recieve it's result in Unity Thread
            //or you can sellect NotThreadSafe. in this case delegate will be called as soon as path ready in PathFinder thread
            //in this example this delegate call Unity API for LineRenderrer so ThreadSafe is used
            myAgent.SetRecievePathDelegate(RecivePathDelegate, AgentDelegateMode.ThreadSafe); 
            update = true;
        }
        
        void Update() {
            RaycastHit hit;
            if (Input.GetMouseButtonDown(0) && Physics.Raycast(myCamera.ScreenPointToRay(Input.mousePosition), out hit, 10000f, 1)) {
                myAgent.transform.position = hit.point;
                update = true;
            }

            if (Input.GetMouseButtonDown(1) && Physics.Raycast(myCamera.ScreenPointToRay(Input.mousePosition), out hit, 10000f, 1)) {
                targetGameObject.transform.position = hit.point;
                update = true;
            }

            if (update) {
                update = false;
                myAgent.Update(); //this function called cause agent cache it position
                myAgent.SetGoalMoveHere(targetGameObject.transform.position); //here we requesting path
            }
        }

        //this function is called when agent recieve path
        private void RecivePathDelegate(Path path) {
            Debug.LogFormat("Path is {0}, it have {1} nodes", path.pathType, path.count);       //we ricieve that
            ExampleThings.PathToLineRenderer(myAgent.positionVector3, lineRenderer, path, 0.2f);//move line to path position
        }
    }
}