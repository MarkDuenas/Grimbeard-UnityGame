using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    public struct NavmeshRay {
        public Vector3 position;
        public Vector2 direction;
        public float maxRange;
        public Area expectedArea;
        public Passability expectedPassability;
        public bool checkArea, checkPassability;
        public int layerMask;

        public NavmeshRay(Vector3 position, Vector2 direction, float maxRange, Area expectedArea, Passability expectedPassability, int layerMask = -1) {    
            this.position = position;
            this.direction = direction;
            this.maxRange = maxRange;
            this.expectedArea = expectedArea;       
            this.expectedPassability = expectedPassability;
            checkArea = checkPassability = true;
            this.layerMask = layerMask;
        }

        public NavmeshRay(Vector3 position, Vector2 direction, float maxRange, Area expectedArea, int layerMask = -1) {
            this.position = position;
            this.direction = direction;
            this.maxRange = maxRange;
            this.expectedArea = expectedArea;
            checkArea = true;
            expectedPassability = Passability.Walkable;
            checkPassability = false;
            this.layerMask = layerMask;
        }

        public NavmeshRay(Vector3 position, Vector2 direction, float maxRange, Passability expectedPassability, int layerMask = -1) {
            this.position = position;
            this.direction = direction;
            this.maxRange = maxRange;
            expectedArea = null;
            checkArea = false;
            this.expectedPassability = expectedPassability;
            checkPassability = true;
            this.layerMask = layerMask;
        }

        public NavmeshRay(Vector3 position, Vector2 direction, float maxRange, int layerMask = -1) {
            this.position = position;
            this.direction = direction;
            this.maxRange = maxRange;
            expectedArea = null;
            expectedPassability = Passability.Walkable;
            checkArea = checkPassability = false;
            this.layerMask = layerMask;
        }

        public NavmeshRay(Vector3 position, Vector2 direction, int layerMask = -1) {
            this.position = position;
            this.direction = direction;
            maxRange = float.MaxValue;
            expectedArea = null;
            expectedPassability = Passability.Walkable;
            checkArea = checkPassability = false;
            this.layerMask = layerMask;
        }
    }

    public class NavMeshQueryRaycastGeneric : NavMeshQueryAbstract<List<RaycastHitNavMesh2>> {
        NavmeshRay[] work;
        int workCount;     

        public NavMeshQueryRaycastGeneric(AgentProperties properties) : base(properties) { }

        public void QueueWork(bool updatePathFinder = true, params NavmeshRay[] rays) {
            if (!queryHaveWork) {
                queryHaveWork = true;

                work = GenericPoolArray<NavmeshRay>.Take(rays.Length);
                for (int i = 0; i < rays.Length; i++) {
                    work[i] = rays[i];
                }
                workCount = rays.Length;

                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        public override void PerformWork(object context) {
            notThreadSafeResult = GenericPool<List<RaycastHitNavMesh2>>.Take();
            notThreadSafeResult.Clear();

            for (int i = 0; i < workCount; i++) {
                var curWork = work[i];
                RaycastHitNavMesh2 hit;
                PathFinder.Raycast(
                    curWork.position.x, curWork.position.y, curWork.position.z,
                    curWork.direction.x, curWork.direction.y,
                    properties, curWork.maxRange,
                    curWork.expectedArea, curWork.checkArea,
                    curWork.expectedPassability, curWork.checkPassability,
                    curWork.layerMask,
                    out hit);
                notThreadSafeResult.Add(hit);
            }

            GenericPoolArray<NavmeshRay>.ReturnToPool(ref work);
            Finish();
        }

        protected override void OnUnityMainThreadFinalize() {
            queryHaveWork = false;
            threadSafeResult.Clear();
            threadSafeResult.AddRange(notThreadSafeResult);
            GenericPool<List<RaycastHitNavMesh2>>.ReturnToPool(ref notThreadSafeResult);

            if (recieveDelegate_TS != null)
                recieveDelegate_TS.Invoke(threadSafeResult);
        }
    }
}