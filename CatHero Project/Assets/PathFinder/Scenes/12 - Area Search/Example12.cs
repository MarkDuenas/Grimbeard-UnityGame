using K_PathFinder.Graphs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {
    //this example uses NavMeshPathQueryTargetArea to get it path and dont use PathFinderAgent
    //in general best decision would be inherit this class from PathFinderAgent 
    //but right now unity dont handle modifying inspector and inheritance very well
    //so between "readable inspector" and "proper code" first one seems more pleasing :)
    
    public class Example12 : MonoBehaviour, IPathOwner {
        public AgentProperties properties;
        public Camera myCamera;             //camera to raycast   
        public LineRenderer line;
         
        NavMeshPathQueryTargetArea query;   //query to search path (!)

        [Area]//used attribute to draw int as global dictionary index
        public int targetArea;//target area in global dictionary
        public float maxSearchCost = 100f;
        
        public void Awake() {
            query = new NavMeshPathQueryTargetArea(properties, this);  //create query
            query.SetOnFinishedDelegate(RecivePathDelegate, AgentDelegateMode.ThreadSafe); //setting here delegate to update line renderer and check if path valid
        }
        
        public void Update() {
            RaycastHit hit;
            if (Physics.Raycast(myCamera.ScreenPointToRay(Input.mousePosition), out hit, 10000f, 1)) {
                transform.position = hit.point;
                query.QueueWork(transform.position, maxSearchCost, targetArea);
            }
        }

        //Debug and checks
        private void RecivePathDelegate(Path path) {
            if (path.pathType != PathResultType.Valid)
                Debug.LogWarningFormat("path is not valid. reason: {0}", path.pathType);
            else
                ExampleThings.PathToLineRenderer(transform.position, line, path, 0.5f);
        }

        public virtual Vector3 pathFallbackPosition {
            get { return transform.position; }
        }
    }
}