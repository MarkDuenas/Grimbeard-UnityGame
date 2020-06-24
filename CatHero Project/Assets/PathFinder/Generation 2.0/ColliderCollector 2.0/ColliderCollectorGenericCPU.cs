using K_PathFinder.Collector;
using K_PathFinder.Settings;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif

namespace K_PathFinder.Collector {
    public partial class ColliderCollector {
        List<ShapeDataAbstract> shapeDataGenericCPU = new List<ShapeDataAbstract>();

        
        private void AddColliderGenericCPU(Collider collider) {
            var gameObjectArea = collider.transform.GetComponent<AreaGameObject>();
            Area area;
            if (gameObjectArea != null) {
                area = PathFinder.GetArea(gameObjectArea.areaInt);
            }
            else {
                if (PathFinder.settings.checkRootTag) {
                    area = PathFinderSettings.tagAssociations[collider.transform.root.tag];
                }
                else {
                    area = PathFinderSettings.tagAssociations[collider.transform.tag];
                }
            }

            if (collider is BoxCollider) {
                shapeDataGenericCPU.Add(new ShapeDataBox(collider as BoxCollider, area));
            }
            else if (collider is SphereCollider) {
                shapeDataGenericCPU.Add(new ShapeDataSphere(collider as SphereCollider, area));
            }
            else if (collider is CapsuleCollider) {
                shapeDataGenericCPU.Add(new ShapeDataCapsule(collider as CapsuleCollider, area));
            }
            else if (collider is CharacterController) {
                shapeDataGenericCPU.Add(new ShapeDataCharacterControler(collider as CharacterController, area));
            }
            else if (collider is MeshCollider) {
                shapeDataGenericCPU.Add(new ShapeDataMesh(collider as MeshCollider, area));
            }
            else {
                Debug.LogFormat("Collider type on {0} currently not supported. Tell developer what is going on", collider.gameObject.name);
                return;
            }

#if UNITY_EDITOR
            if (Debuger_K.doDebug && Debuger_K.debugOnlyNavMesh == false)
                Debuger_K.AddColliderBounds(template.gridPosition.x, template.gridPosition.z, template.properties, collider);
#endif
        }

        private void CollectCollidersCPU(ShapeCollector collector) {
            if (shapeDataGenericCPU.Count > 0) {
                if (profiler != null) profiler.AddLogFormat("Start shape collecting by CPU. count: {0}", shapeDataGenericCPU.Count);

                foreach (var shape in shapeDataGenericCPU) {
                    collector.Append(shape);
                    if (profiler != null) profiler.AddLogFormat("Collected {0} of type {1}", shape.name, shape.GetType().Name);
                }

                if (profiler != null) profiler.AddLog("Finish shape collecting by CPU");
            }
            else {
                if (profiler != null) profiler.AddLog("No pending shapes to be collected by CPU");
            }
        }

        public int pendingGenericShapeCPU {
            get { return shapeDataGenericCPU.Count; }
        }
    }
}
