using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using K_PathFinder.CoverNamespace;

namespace K_PathFinder.Serialization2 {
    [Serializable]
    public class SerializedCover {
        [SerializeField]
        public List<SerializedCoverPoint> coverPoints = new List<SerializedCoverPoint>();
        public Vector3 left, right, normal;
        public float leftX, leftY, leftZ, rightX, rightY, rightZ, normalX, normalY, normalZ;
        public int coverType;

        public SerializedCover(Cover cover) {
            coverType = cover.coverType;
            left = cover.left;
            right = cover.right;
            normal = cover.normalV3;

            foreach (var p in cover.coverPoints) {
                coverPoints.Add(new SerializedCoverPoint(p, p.cell.globalID));
            }
        }
    }

    [Serializable]
    public struct SerializedCoverPoint {
        public float 
            positionX, 
            positionY, 
            positionZ,
            cellPositionX, 
            cellPositionY, 
            cellPositionZ;        
        public int cell;

        public SerializedCoverPoint(NodeCoverPoint point, int cell) {
            positionX = point.x;
            positionY = point.y;
            positionZ = point.z;
            Vector3 cellPoint = point.cellPos;

            cellPositionX = cellPoint.x;
            cellPositionY = cellPoint.y;
            cellPositionZ = cellPoint.z;

            this.cell = cell;
        }
    }
}