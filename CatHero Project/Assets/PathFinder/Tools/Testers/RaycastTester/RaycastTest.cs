#if UNITY_EDITOR
using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using System.Collections.Generic;
using System.Text;
#endif
using UnityEngine;

namespace K_PathFinder {
#if UNITY_EDITOR
    [ExecuteInEditMode()]
#endif
    public class RaycastTest : MonoBehaviour {
#if UNITY_EDITOR
        public AgentProperties properties;
        public float range = 5f;
        public int tests = 30;
        //public bool doForward = true;
        
        [SerializeField]
        public ParticularData[] dataList;
        Vector2[] testDirections;
   
        private const int circleThings = 50;
        private Vector3[] circle;
        private RaycastHitNavMesh2[] hits;

        [Header("Step By Step Raycast Test")]
        public bool doStepByStepTest = true;
        public bool useTransformPosition = false;
        public Vector3 testPosition;
        public bool useTransformForward = false;
        public Vector2 testDirection;
        [Range(0f, 1f)]
        public float iterationHeightDelta = 0.1f;

        [Header("Debug Nearest Nell")]
        public bool debugNearestCell = true;
        [Range(0f, 1f)]
        public float debugNearestCellSeparation = 0.1f;

        public BitMaskPF bitMask;

        public bool testSimpleLayout = true;

        StringBuilder sb = new StringBuilder();




        void Start() {
            circle = new Vector3[circleThings];
            for (int i = 0; i < circleThings; i++) {
                float x = Mathf.Cos((i / (float)circleThings) * 2 * Mathf.PI);
                float z = Mathf.Sin((i / (float)circleThings) * 2 * Mathf.PI);
                circle[i] = new Vector3(x, 0, z);
            }
        }

        void Update() {
            if (properties == null) {
                Debug.LogWarning("no properties");
                return;
            }

            RaycastHit raycastHit;
            if (!Physics.Raycast(transform.position, Vector3.down, out raycastHit, 10)) {
                Debug.LogWarning("no raycast hit");
                return;
            }

            Vector3 p = raycastHit.point;
            Debug.DrawLine(transform.position, p, Color.red);

            Debuger_K.ClearGeneric();

            //PathFinder.Raycast2(p, new Vector3(forward.x, 0, forward.z), properties);

            foreach (var d in dataList) {
                if (d.enabled) {
                    RaycastHitNavMesh2 hit;            
                    PathFinder.Raycast(d.position, d.direction, properties, out hit);
                    Debuger_K.AddDot(d.position, Color.magenta);
                    Debuger_K.AddRay(d.position, d.direction, Color.magenta);
                    DrawLine(d.position, hit);
                }
            }
            
            //if (doForward) {
            //    RaycastHitNavMesh2 hit;
            //    var f = transform.forward;
            //    PathFinder.Raycast(p.x, p.y, p.z, f.x, f.z, properties, range, out hit);
            //    DrawLine(p, hit);
            //}

            if (tests > 0) {
                if (testDirections == null)
                    testDirections = new Vector2[0];

                if(testDirections.Length != tests) {
                    testDirections = new Vector2[tests];
                    for (int i = 0; i < tests; i++) {
                        float x = Mathf.Cos((i / (float)tests) * 2 * Mathf.PI);
                        float z = Mathf.Sin((i / (float)tests) * 2 * Mathf.PI);
                        testDirections[i] = new Vector2(x, z);
                    }
                }

                PathFinder.Raycast(p.x, p.y, p.z, testDirections, properties, range, ref hits);
                for (int i = 0; i < hits.Length; i++) {
                    DrawLine(p, hits[i]);
                }
            }

            if (tests > 0) {
                for (int i = 0; i < circleThings - 1; i++) {
                    Debug.DrawLine(p + (circle[i] * range), p + (circle[i + 1] * range), Color.blue);
                }
                Debug.DrawLine(p + (circle[circleThings - 1] * range), p + (circle[0] * range), Color.blue);
            }


            if (testSimpleLayout) {
                Vector3 ps = transform.position;
                Cell ce;
                Vector3 cPos;
                bool outside;

                if(PathFinder.TryGetClosestCell_Internal(ps.x, ps.y, ps.z, properties, out cPos, out ce, out outside)) {
                    Debug.DrawLine(ps, cPos, Color.blue);
                    Debug.DrawLine(ps, ce.centerVector3, Color.magenta);
                }
            }


            Cell cell;
            if(debugNearestCell) {
                if(PathFinder.TryGetCell(transform.position, properties, out cell)) {    

                    sb.AppendFormat("ID: {0}\nLayer: {1}\nArea: {2}\npassability: {3}\nAdvanced: {4}\nBitmask Layer: {5}\nOriginal Edges Count: {6}\nRaycast Data Count: {7}\n", 
                        cell.globalID, 
                        cell.layer, 
                        cell.area.name, 
                        cell.passability,
                        cell.advancedAreaCell,
                        cell.bitMaskLayer,
                        cell.originalEdgesCount,
                        cell.raycastDataCount);


                    if (cell.advancedAreaCell && cell.pathContent != null) {
                        sb.AppendLine("Path Content:");
                        foreach (var item in cell.pathContent) {
                            sb.AppendLine(item.ToString());
                        }
                    }

                    sb.AppendFormat("Cell Content: {0}\n",cell.cellContentValues.Count);
                    foreach (var item in cell.cellContentValues) {
                        sb.AppendLine(item.ToString());
                    }

                    var connections = cell.connections;
                    int connectionsCount = cell.connectionsCount;

                    sb.AppendFormat("Connections count: {0}\n", connectionsCount);
                    sb.Append("Connections: ");
                    for (int i = 0; i < connectionsCount; i++) {
                        var con = connections[i];
                        sb.AppendFormat("{0}, ", con.connection);
                    }
                    sb.Append("\n");

                    Vector3 up = new Vector3(0f, debugNearestCellSeparation, 0f);
  

                    var raycastData = cell.raycastData;
                    int raycastDataCount = cell.raycastDataCount;
                    for (int i = 0; i < raycastDataCount; i++) {
                        var d = raycastData[i]; 
                        Debuger_K.AddLabel(SomeMath.MidPoint(new Vector3(d.xLeft, d.yLeft, d.zLeft), new Vector3(d.xRight, d.yRight, d.zRight)), d.connection);
                    }

                    for (int i = 0; i < cell.originalEdgesCount; i++) {
                        var e = cell.originalEdges[i];
                        Debuger_K.AddLine(e, Color.green, addOnTop: debugNearestCellSeparation);
                    }


                    Debuger_K.AddDot(cell.centerVector3 + up, Color.red);
                    Debuger_K.AddLabel(cell.centerVector3 + up, sb);
                    Debuger_K.AddLine(cell.centerVector3 + up, transform.position, Color.red);
                    sb.Length = 0;

                }
            }

            //if (doStepByStepTest) {
            //    Vector3 pos = useTransformPosition ? transform.position : testPosition;
            //    Vector2 dir;
            //    if (useTransformForward) {
            //        var f = transform.forward;
            //        dir.x = f.x;
            //        dir.y = f.z;
            //    }
            //    else
            //        dir = testDirection;

            //    if (pos.x % PathFinder.gridSize <= 0.01f)
            //        pos.x += 0.02f;
            //    if (pos.z % PathFinder.gridSize <= 0.01f)
            //        pos.z += 0.02f;

            //    if (PathFinder.TryGetCell(pos, properties, out cell)) {
            //        pos.x += ((cell.centerVector3.x - pos.x) * 0.001f);
            //        pos.z += ((cell.centerVector3.z - pos.z) * 0.001f);

            //        Vector3 testHit;
            //        Vector3 testHitPrevious = pos;
            //        int iterations = 1;

            //        while (cell != null) {
            //            CellContentRaycastData[] raycastData = cell.raycastData;
            //            int raycastDataCount = cell.raycastDataCount;
            //            int hit = -1;

            //            float debugUp = iterations * iterationHeightDelta;
            //            bool haveResult = false;

            //            for (int i = 0; i < raycastDataCount; i++) {
            //                CellContentRaycastData d = raycastData[i];
            //                Vector3 pLeft = new Vector3(d.xLeft, d.yLeft + debugUp, d.zLeft);
            //                Vector3 pRight = new Vector3(d.xRight, d.yRight + debugUp, d.zRight);
            //                float crossLeft = SomeMath.V2Cross(d.xLeft - pos.x, d.zLeft - pos.z, dir.x, dir.y);
            //                float crossRight = SomeMath.V2Cross(d.xRight - pos.x, d.zRight - pos.z, dir.x, dir.y);


            //                if (crossLeft >= 0f && crossRight < 0f) {
            //                    float ccdDirX = d.xRight - d.xLeft;
            //                    float ccdDirZ = d.zRight - d.zLeft;
            //                    float cross = ccdDirZ * dir.x - ccdDirX * dir.y; // if d == 0 then bad situation;                   

            //                    if (cross != 0f) {
            //                        if (haveResult)
            //                            Debug.LogError("raycasting already have result but found another one");

            //                        Debuger_K.AddLine(pLeft, pRight, Color.green);

            //                        hit = i;
            //                        float product = ((d.xLeft - pos.x) * dir.y + (pos.z - d.zLeft) * dir.x) / cross;
            //                        testHit.x = d.xLeft + ccdDirX * product;
            //                        testHit.y = d.yLeft + (d.yRight - d.yLeft) * product + debugUp;
            //                        testHit.z = d.zLeft + ccdDirZ * product;

            //                        Debuger_K.AddDot(testHit, Color.red);
            //                        Debuger_K.AddLine(testHitPrevious, testHit, Color.yellow);

            //                        testHitPrevious = testHit;
            //                        cell = d.connection;
            //                    }
            //                    else {
            //                        Debuger_K.AddLine(pLeft, pRight, Color.magenta);
            //                    }

            //                }
            //                else {
            //                    Debuger_K.AddLine(pLeft, pRight, Color.red);
            //                }
            //            }

            //            if (hit == -1) {
            //                //no valid edges
            //                Debug.LogError("ray did not hit any cell border");
            //            }


            //            if (iterations++ > 100) {
            //                Debug.LogError("iterations > 100");
            //                break;
            //            }
            //        }
            //    }
            //    else {
            //        Debuger_K.AddLabel(pos, "OutsideGraph");
            //    }
            //}
        }

        private void DrawLine(Vector3 start, RaycastHitNavMesh2 hit) {
            //Debuger_K.AddDot(start, Color.magenta);

            Color color;
            switch (hit.resultType) {
                case NavmeshRaycastResultType2.Nothing:
                    color = Color.red;
                    break;
                case NavmeshRaycastResultType2.NavmeshBorderHit:
                    color = Color.green;
                    break;
                case NavmeshRaycastResultType2.ReachMaxDistance:
                    color = Color.blue;
                    break;
                default:
                    color = Color.magenta;
                    break;
            }
            Debug.DrawLine(start, hit.point, color);
            //Debuger_K.AddLabel(hit.point, hit.resultType.ToString());
        }
#endif
    }

    [System.Serializable]
    public struct ParticularData {
        public bool enabled;
        public Vector3 position;
        public Vector2 direction;
    }
}