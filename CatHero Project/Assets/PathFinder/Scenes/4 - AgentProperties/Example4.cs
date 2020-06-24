using K_PathFinder.Graphs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {
    public class Example4 : MonoBehaviour {
        public Camera myCamera;
        public GameObject targetGameObject, linePrefab;
        public PathFinderAgent[] agents;
        private LineRenderer[] lines;
        
        void Start() {
            lines = new LineRenderer[agents.Length];

            for (int i = 0; i < agents.Length; i++) {         
                GameObject lineGameObject = Instantiate(linePrefab);
                lines[i] = lineGameObject.GetComponent<LineRenderer>();
          
                int cahedIndex = i;//or else delegates wount work as expected    
                //update line renderer for this agent by delegate
                agents[i].SetRecievePathDelegate((Path path) => {
                    ExampleThings.PathToLineRenderer(agents[cahedIndex].transform.position, lines[cahedIndex], path, 0.2f);         
                }, AgentDelegateMode.ThreadSafe);     
            }
        }
        
        void Update() {
            RaycastHit hit;
            if (Input.GetMouseButtonDown(0) && Physics.Raycast(myCamera.ScreenPointToRay(Input.mousePosition), out hit, 10000f, 1)) {
                targetGameObject.transform.position = hit.point;

                for (int i = 0; i < agents.Length; i++) {
                    agents[i].Update();
                    agents[i].SetGoalMoveHere(targetGameObject.transform.position, applyRaycast: true);
                }
            }
        }
    }
}