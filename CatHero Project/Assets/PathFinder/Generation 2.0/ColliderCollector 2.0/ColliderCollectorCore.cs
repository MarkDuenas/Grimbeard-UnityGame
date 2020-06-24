using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Collector {
    public partial class ColliderCollector {
        public NavmeshProfiler profiler = null;
        public NavMeshTemplateCreation template;
        Bounds chunkBounds;

        public ShapeCollector shapeCollectorResult;

        public List<KeyValuePair<AreaWorldMod, ShapeCollector>> shapeCollectorMods;

        List<AreaWorldModShapeData> modsChangeArea = new List<AreaWorldModShapeData>();
        List<AreaWorldModShapeData> modsMakeHole = new List<AreaWorldModShapeData>();

        struct AreaWorldModShapeData {
            public AreaWorldMod mod;
            public int priority;
            public Area area;
            public List<ShapeDataAbstract> shapes;
            public ColliderInfoMode mode;

            public AreaWorldModShapeData(AreaWorldMod Mod, List<ShapeDataAbstract> Shapes) {
                mod = Mod;
                shapes = Shapes;
                priority = Mod.priority;
                area = Mod.GetArea();
                mode = Mod.mode;
            }
        }    

        struct TerrainShape {
            public ShapeCollector terrain;
            public ShapeCollector trees;

            public TerrainShape(ShapeCollector terrain, ShapeCollector trees) {
                this.terrain = terrain;
                this.trees = trees;
            }
        }

        public ColliderCollector(NavMeshTemplateCreation template) {
            this.template = template;
            chunkBounds = template.chunkOffsetedBounds;
        }
        
        public void AddCollider(params Collider[] colliders) {
            for (int i = 0; i < colliders.Length; i++) {
                Collider collider = colliders[i];
                if(collider == null) {
                    if (profiler != null) profiler.AddLogFormat("Collider {0} was excluded cause it's null (?)", collider.name);
                    continue;
                }

                if (collider.enabled == false) {
                    if (profiler != null) profiler.AddLogFormat("Collider {0} was excluded cause it's disabled", collider.name);
                    continue;
                }

                if (collider.isTrigger) {
                    if (profiler != null) profiler.AddLogFormat("Collider {0} was excluded cause it's trigger", collider.name);
                    continue;
                }

                if (chunkBounds.Intersects(collider.bounds) == false) {
                    if (profiler != null) profiler.AddLogFormat("Collider {0} was excluded cause it was outside chunk", collider.name);
                    continue;
                }

                if (template.checkHierarchyTag) {
                    for (Transform t = collider.transform; t != null; t = t.parent) {
                        if (template.IgnoredTagsContains(t.tag)) {
                            if (profiler != null) profiler.AddLogFormat("Collider {0} was excluded by hierarchy tag ({1})", collider.name, collider.tag);
                            continue;
                        }
                    }
                }
                else if (template.IgnoredTagsContains(collider.tag)) {
                    if (profiler != null) profiler.AddLogFormat("Collider {0} was excluded cause it's tag ({1}) was contained in ignored tag list", collider.name, collider.tag);
                    continue;
                }

                if(collider is TerrainCollider) {
                    AddColliderTerrain(collider, PathFinder.terrainCollectionType);                 
                }
                else {
                    switch (PathFinder.colliderCollectorType) {
                        case ColliderCollectorType.CPU:
                            AddColliderGenericCPU(collider);
                            break;
                        case ColliderCollectorType.ComputeShader:
                            if(collider is MeshCollider && (collider as MeshCollider).convex == false)
                                AddColliderGenericCPU(collider);
                            else
                                AddColliderGenericGPU(collider);
                            break;
                    }
           
                }
            }
        }

        public void AddModifyers(List<AreaWorldMod> mods) {
            var chunkBounds = template.chunkData.bounds;
            if (profiler != null) profiler.AddLogFormat("Adding {0} modifyers", mods.Count);

            foreach (var mod in mods) {
                if (mod == null) {
                    if (profiler != null) profiler.AddLogFormat("Mod {0} was excluded cause it's null (?)", mod.name);
                    continue;
                }

                if (mod.enabledThreadsafe == false) {
                    if (profiler != null) profiler.AddLogFormat("Mod {0} was excluded cause it's disabled", mod.name);
                    continue;
                }

                if (chunkBounds.Intersects(mod.bounds) == false) {
                    if (profiler != null) profiler.AddLogFormat("Mod {0} was excluded cause it was outside chunk", mod.name);
                    continue;
                }

                if (template.IgnoredTagsContains(mod.threadsafeTag)) {
                    if (profiler != null) profiler.AddLogFormat("Mod {0} was excluded cause it's tag ({1}) was contained in ignored tag list", mod.name, mod.tag);
                    continue;
                }

                if ((template.includedLayers.value & (1 << mod.threadsafeUnityLayer)) == 0) {
                    if (profiler != null) profiler.AddLogFormat("Mod {0} was excluded cause it physical layer is not included", mod.name);
                    continue;
                }

                if (mod.useAdvancedArea)
                    template.hashData.AddAreaHash(mod.advancedArea);

                Matrix4x4 modMatrix = mod.threadSafeLocalToWorldMatrix;
                Area area = mod.GetArea();
                var modInfoMode = mod.mode;

                List<ShapeDataAbstract> list = new List<ShapeDataAbstract>();
                foreach (var value in mod.allMods) {
                    if (chunkBounds.Intersects(value.bounds) == false)
                        continue;

                    switch (value.myType) {
                        case AreaWorldModMagicValueType.Sphere:
                            list.Add(new ShapeDataSphere(value, area, modInfoMode));
                            break;
                        case AreaWorldModMagicValueType.Capsule:
                            list.Add(new ShapeDataCapsule(value, area, BC.GetRotation(modMatrix), modInfoMode));
                            break;
                        case AreaWorldModMagicValueType.Cuboid:
                            list.Add(new ShapeDataBox(value, area, BC.GetRotation(modMatrix), modInfoMode));
                            break;
                    }
                }

                switch (modInfoMode) {
                    case ColliderInfoMode.Solid:
                        shapeDataGenericCPU.AddRange(list);
                        break;
                    case ColliderInfoMode.ModifyArea:
                        modsChangeArea.Add(new AreaWorldModShapeData(mod, list));
                        break;
                    case ColliderInfoMode.MakeHoleApplyArea: case ColliderInfoMode.MakeHoleRetainArea:
                        modsMakeHole.Add(new AreaWorldModShapeData(mod, list));
                        break;
                }
            }
        }

        public void Collect() {
            shapeCollectorResult = ShapeCollector.GetFromPool(template.lengthX_extra, template.lengthZ_extra, template);

            CollectTerrainCPU(shapeCollectorResult);
            CollectTerrainGPU(shapeCollectorResult);

            CollectCollidersCPU(shapeCollectorResult);
            CollectCollidersGPU(shapeCollectorResult);

            ApplyMods();   
            //shapeCollectorGeneric.DebugMe();
        }

        private void ApplyMods() {
            if(modsChangeArea.Count > 0) {
                if (profiler != null) profiler.AddLogFormat("Start applying mods that change Area. count: {0}", modsChangeArea.Count);

                modsChangeArea.Sort(Comparer);
                for (int i = 0; i < modsChangeArea.Count; i++) {
                    shapeCollectorResult.ChangeArea(modsChangeArea[i].shapes, modsChangeArea[i].area);
                }

                if (profiler != null) profiler.AddLog("Finish applying mods that change Area");
            }
            else 
                if (profiler != null) profiler.AddLog("No mods that change Area");
            
            if (modsMakeHole.Count > 0) {
                if (profiler != null) profiler.AddLogFormat("Start applying mods that make hole. count: {0}", modsChangeArea.Count);

                modsMakeHole.Sort(Comparer);
                for (int i = 0; i < modsMakeHole.Count; i++) {
                    if (modsMakeHole[i].mode == ColliderInfoMode.MakeHoleApplyArea)
                        shapeCollectorResult.MakeHole(modsMakeHole[i].shapes, modsMakeHole[i].area);
                    if (modsMakeHole[i].mode == ColliderInfoMode.MakeHoleRetainArea)
                        shapeCollectorResult.MakeHole(modsMakeHole[i].shapes, null);
                }

                if (profiler != null) profiler.AddLog("Finish applying mods that make hole");
            }
            else
                if (profiler != null) profiler.AddLog("No mods that make hole");
        }

        static int Comparer(AreaWorldModShapeData left, AreaWorldModShapeData right) {
            if (left.priority < right.priority) return -1;
            if (left.priority > right.priority) return 1;

            //if priority equal
            if (left.area.overridePriority < right.area.overridePriority) return -1;
            if (left.area.overridePriority > right.area.overridePriority) return 1;
            return 0;
        }
    }
}