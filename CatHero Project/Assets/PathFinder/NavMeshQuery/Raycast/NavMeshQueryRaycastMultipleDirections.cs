using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//work in progress

//namespace K_PathFinder {
//    public class NavMeshQueryRaycastMultipleDirections : NavMeshQueryAbstract<List<RaycastHitNavMesh2>> {
//        Vector3 position;
//        float maxRange;
//        Vector2[] directions;

//        public NavMeshQueryRaycastMultipleDirections(AgentProperties properties) : base(properties) { }

//        public void QueueWork(Vector3 position, float maxRange = float.MaxValue, bool updatePathFinder = true, params Vector2[] directions) {
//            if (!queryHaveWork) {
//                queryHaveWork = true;

//                this.position = position;
//                this.maxRange = maxRange;

//                this.directions = GenericPoolArray<Vector2>.Take(directions.Length, true);
//                for (int i = 0; i < directions.Length; i++) {
//                    this.directions[i] = directions[i];
//                }
//                workCount = directions.Length;

//                PathFinder.queryBatcher.AddWork(this, null);
//                if (updatePathFinder)
//                    PathFinder.Update();
//            }
//        }

//        public override void PerformWork(object context) {
//            notThreadSafeResult = GenericPool<List<RaycastHitNavMesh2>>.Take();
//            notThreadSafeResult.Clear();

//            var sampleResults = PathFinder.navmeshPositionResults;

//            for (int i = 0; i < workCount; i++) {
//                var curWork = work[i];
//                RaycastHitNavMesh2 hit;
//                PathFinder.Raycast(
//                    curWork.position.x, curWork.position.y, curWork.position.z,
//                    curWork.direction.x, curWork.direction.y,
//                    properties, curWork.maxRange,
//                    curWork.expectedArea, curWork.checkArea,
//                    curWork.expectedPassability, curWork.checkPassability,
//                    out hit);
//                notThreadSafeResult.Add(hit);
//            }

//            Finish();
//        }

//        protected override void OnUnityMainThreadFinalize() {
//            queryHaveWork = false;
//            threadSafeResult.Clear();
//            threadSafeResult.AddRange(notThreadSafeResult);
//            GenericPool<List<RaycastHitNavMesh2>>.ReturnToPool(ref notThreadSafeResult);

//            if (recieveDelegate_TS != null)
//                recieveDelegate_TS.Invoke(threadSafeResult);
//        }
//    }
//}