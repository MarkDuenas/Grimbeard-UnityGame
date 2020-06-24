using K_PathFinder;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {
    [RequireComponent(typeof(PathFinderAgent), typeof(CharacterController))]
    public class Example15 : MonoBehaviour {
        public Material bodyMaterial;
        public float radius = 15;
        public float scale = 0.1f;

        public LineRenderer lineRenderer;
        public GameObject follow;
        [Range(0f, 5f)] public float speed = 3;

        PathFinderAgent agent;
        CharacterController controler;

        Stack<GameObject> spheresPool = new Stack<GameObject>();
        List<GameObject> spheresInUse = new List<GameObject>();

        // Use this for initialization
        void Start() {
            controler = GetComponent<CharacterController>();
            agent = GetComponent<PathFinderAgent>();          
            agent.SetRecieveSampleDelegate(RecieveSamplePointDelegate, AgentDelegateMode.ThreadSafe);
            agent.SetRecievePathDelegate(RecivePathDelegate, AgentDelegateMode.ThreadSafe);
        }

        // Update is called once per frame
        void Update() {
            agent.SetGoalMoveHere(follow.transform.position, applyRaycast: true);
            agent.SetGoalGetSamplePoints(radius, richCost: true);

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
                    controler.Move(new Vector3(moveDirection.x, 0, moveDirection.y) * Time.deltaTime);
                }
            }
        }

        //delegate that used when returned sampled points
        //haha thats some awkward result type    
        void RecieveSamplePointDelegate(List<PointQueryResult<CellSamplePoint>> list) {
            //reset spheres
            foreach (var item in spheresInUse) {
                item.SetActive(false);
                spheresPool.Push(item);
            }
            spheresInUse.Clear();

            foreach (var queryResult in list) {
                GameObject go;
                if (spheresPool.Count > 0) {
                    go = spheresPool.Pop();
                    go.SetActive(true);
                }
                else {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.GetComponent<MeshRenderer>().material = bodyMaterial;
                }

                go.transform.position = queryResult.value.position;

                float size = (radius - queryResult.cost) * scale;
                go.transform.localScale = new Vector3(size, size, size);
                spheresInUse.Add(go);
            }
        }

        //this function is called when agent recieve path
        private void RecivePathDelegate(Path path) {
            ExampleThings.PathToLineRenderer(transform.position, lineRenderer, path, 0.2f);
        }
    }
}