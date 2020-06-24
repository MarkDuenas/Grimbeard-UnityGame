using K_PathFinder.Graphs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    public class NavMeshQueryNavmeshToMeshResult {
        public List<Vector3> verts = new List<Vector3>();
        public List<int> tris = new List<int>();

        public void Copy(NavMeshQueryNavmeshToMeshResult result) {
            verts.Clear();
            verts.AddRange(result.verts);
            tris.Clear();
            tris.AddRange(result.tris);
        }
        
        public void Clear() {
            verts.Clear();
            tris.Clear();
        }
    }

    public class NavMeshQueryNavmeshToMesh : NavMeshQueryAbstract<NavMeshQueryNavmeshToMeshResult> {
        Area targetArea;
        List<Graph> tempList = new List<Graph>();

        public NavMeshQueryNavmeshToMesh(AgentProperties properties) : base(properties) {
            threadSafeResult = new NavMeshQueryNavmeshToMeshResult();
            notThreadSafeResult = new NavMeshQueryNavmeshToMeshResult();
        }

        public void QueueWork(Area area, bool updatePathFinder = true) {
            if (!queryHaveWork) {
                queryHaveWork = true;
                targetArea = area;
                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        public override void PerformWork(object context) {
            tempList.Clear();
            notThreadSafeResult.Clear();
            List<Vector3> verts = notThreadSafeResult.verts;
            List<int> tris = notThreadSafeResult.tris;
            PathFinder.GetAllGraphs(tempList);

            for (int i = 0; i < tempList.Count; i++) {
                Graph graph = tempList[i];
                if (graph.properties != properties)
                    continue;

                Cell[] cellsArray;
                int cellsArrayCount;
                graph.GetCells(out cellsArray, out cellsArrayCount);

                for (int c = 0; c < cellsArrayCount; c++) {
                    Cell cell = cellsArray[c];
                    if (cell == null) //this should not be case but you never know
                        continue;

                    if(cell.area == targetArea) {
                        CellContentData[] edges = cell.originalEdges;
                        int edgesCount = cell.originalEdgesCount;
                        Vector3 center = cell.centerVector3;

                        for (int e = 0; e < edgesCount; e++) {
                            CellContentData curE = edges[e];
                            tris.Add(verts.Count);
                            verts.Add(center);
                            tris.Add(verts.Count);
                            verts.Add(curE.leftV3);
                            tris.Add(verts.Count);
                            verts.Add(curE.rightV3);
                        }              
                    }
                }
            }
                
            Finish();
        }

        protected override void OnUnityMainThreadFinalize() {          
            queryHaveWork = false;
            targetArea = null;

            threadSafeResult.Copy(notThreadSafeResult);

            if (recieveDelegate_TS != null)
                recieveDelegate_TS.Invoke(threadSafeResult);
        }
    }
}