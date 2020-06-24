using K_PathFinder.Graphs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {

    //public struct RaycastHitNavMesh {
    //    public readonly Vector3 point;
    //    public readonly NavmeshRaycastResultType resultType;
    //    public readonly Cell cellBorder;//last cell this ray was in

    //    public RaycastHitNavMesh(Vector3 point, NavmeshRaycastResultType resultType, Cell cellBorder) {
    //        this.point = point;
    //        this.resultType = resultType;
    //        this.cellBorder = cellBorder;
    //    }
    //}

    public struct RaycastHitNavMesh2 {
        public float x, y, z;
        public NavmeshRaycastResultType2 resultType;
        public Cell lastCell;//last cell this ray was in

        public RaycastHitNavMesh2(float x, float y, float z, NavmeshRaycastResultType2 resultType, Cell lastCell) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.resultType = resultType;
            this.lastCell = lastCell;
        }

        public RaycastHitNavMesh2(Vector3 pos, NavmeshRaycastResultType2 resultType, Cell lastCell) {
            this.x = pos.x;
            this.y = pos.y;
            this.z = pos.z;
            this.resultType = resultType;
            this.lastCell = lastCell;
        }

        public Vector3 point {
            get { return new Vector3(x, y, z); }
        }

        /// <summary>
        ///Nothing = 0           false 
        ///ReachMaxDistance = 1  false
        ///OutsideGraph = 2      true
        ///NavmeshBorderHit = 3  true
        ///PassabilityChange = 4 true
        ///AreaChange = 5        true
        ///IncativeCell = 6      true
        /// </summary>
        public bool hitSomething {
            get { return (int)resultType > 1; }
        }
    }
}
