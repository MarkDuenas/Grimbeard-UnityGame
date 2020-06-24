using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace K_PathFinder.Samples {
    public class Example18 : MonoBehaviour {
        public AgentProperties properties;
        public GameObject prefabObject;
        public GameObject prefabLine;
        public int objects = 9;
        public int minObjects = 3;
        public int density = 10;        
        List<UserDefinedPoint> curObjects = new List<UserDefinedPoint>();
        List<Vector2> grid = new List<Vector2>();
        public float spawnArea = 10f;
        public NicePointer pointer;
        [Range(0f, 1f)]
        public float yOffset = 0.5f;

        public Color
            lowestValue = Color.green,
            highestValue = Color.red;

        List<LineRenderer> linePool = new List<LineRenderer>();
        NavMeshPointQuery<UserDefinedPoint> points;

        private void Start() {
            //creating query
            points = new NavMeshPointQuery<UserDefinedPoint>(properties);
            //points.debug = false;
            points.SetOnFinishedDelegate(VeryCoolDelegate, AgentDelegateMode.ThreadSafe);
        }
        
        void Update() {
            points.QueueWork(pointer.positionBottom, 25f, predicate: MyPredicate);
        }        

        bool MyPredicate(UserDefinedPoint point) {
            return point.isPointValid;
        }

        void VeryCoolDelegate(List<PointQueryResult<UserDefinedPoint>> points) {
            foreach (var item in points) {
                if (item.value == null)
                    Debug.LogError("item.value == null");
                //Debug.Log(item.ToString());
            }

            //geting ranges for nice colors
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (var item in points) {
                if (item.cost < min)
                    min = item.cost;
                if (item.cost > max)
                    max = item.cost;
            }

            //populating pool
            if(linePool.Count < points.Count) {
                while (linePool.Count <= points.Count) {
                    GameObject newLineGO = Instantiate(prefabLine, transform);
                    LineRenderer newLR = newLineGO.GetComponent<LineRenderer>();
                    linePool.Add(newLR);
                }
            }

            //assign values to lines
            Vector3 offset = new Vector3(0, yOffset, 0);
            for (int i = 0; i < points.Count; i++) {
                PointQueryResult<UserDefinedPoint> curResult = points[i];
                if (curResult.value == null) //continue in case scene reload
                    continue;

                LineRenderer lr = linePool[i];
                lr.gameObject.SetActive(true);
                lr.SetPosition(0, pointer.positionBottom + offset);
                lr.SetPosition(1, curResult.value.transform.position + offset);
                lr.material.color = Color.Lerp(lowestValue, highestValue, Mathf.InverseLerp(min, max, curResult.cost));
            }

            //disable remaining lines
            for (int i = points.Count; i < linePool.Count; i++) {
                if (linePool[i].gameObject != null)//in case scene reload
                    linePool[i].gameObject.SetActive(false);
            }
        }

        public void DropCubes() {
            foreach (var item in curObjects) {
                Destroy(item.gameObject);
            }
            curObjects.Clear();

            grid.Clear();
            for (int x = 0; x < density; x++) {
                for (int z = 0; z < density; z++) {
                    grid.Add(new Vector2((float)x / density, (float)z / density));
                }
            }

            Vector3 transformPos = transform.position;
            float areaSpawnMinX = transformPos.x - spawnArea;
            float areaSpawnMaxX = transformPos.x + spawnArea;
            float areaSpawnMinZ = transformPos.z - spawnArea;
            float areaSpawnMaxZ = transformPos.z + spawnArea;

            int validCubes = Random.Range(minObjects, objects - minObjects);
            for (int i = 0; i < objects; i++) {
                bool isValid = i >= validCubes;
                int posIndex = Random.Range(0, grid.Count);
                Vector2 pos = grid[posIndex];
                grid.RemoveAt(posIndex);


                GameObject cube = Instantiate(
                    prefabObject,
                    new Vector3(
                        Mathf.Lerp(areaSpawnMinX, areaSpawnMaxX, pos.x), 
                        transformPos.y, 
                        Mathf.Lerp(areaSpawnMinZ, areaSpawnMaxZ, pos.y)),
                    Random.rotation);

                cube.GetComponent<MeshRenderer>().material.color = isValid ? Color.green : Color.red;

                UserDefinedPoint udp = cube.GetComponent<UserDefinedPoint>();
                udp.isPointValid = isValid;
                curObjects.Add(udp);
            }
        }
    }
}