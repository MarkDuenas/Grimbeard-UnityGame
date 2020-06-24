using K_PathFinder.Graphs;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    public struct CellSamplePoint_Internal {
        public Cell cell;
        public float x, y, z;
        public int gridX, gridZ;

        public CellSamplePoint_Internal(Vector3 realPos, int gridX, int gridZ, Cell cell) {
            this.x = realPos.x;
            this.y = realPos.y;
            this.z = realPos.z;
            this.gridX = gridX;
            this.gridZ = gridZ;
            this.cell = cell;
        }

        public CellSamplePoint_Internal(float x, float y, float z, int gridX, int gridZ, Cell cell) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.gridX = gridX;
            this.gridZ = gridZ;
            this.cell = cell;
        }
    }

    //build in cell content value that describe cell sample
    //it does not contain any cell reference but not contain its global grid value incase you want to reconstruct whole grid
    public struct CellSamplePoint : ICellContentValue, IEquatable<CellSamplePoint> {
        public readonly float x, y, z;
        public readonly int gridX, gridZ, layer;
        public readonly Passability passability;
        public readonly Area area;

        public CellSamplePoint(float x, float y, float z, int gridX, int gridZ, int layer, Area area, Passability passability) {
            this.x = x;
            this.y = y;
            this.z = z;
            this.gridX = gridX;
            this.gridZ = gridZ;
            this.layer = layer;
            this.area = area;
            this.passability = passability;
        }
        
        public CellSamplePoint(Vector3 pos, int gridX, int gridZ, int layer, Area area, Passability passability) {
            this.x = pos.x;
            this.y = pos.y;
            this.z = pos.z;
            this.gridX = gridX;
            this.gridZ = gridZ;
            this.layer = layer;
            this.area = area;
            this.passability = passability;
        } 

        public Vector3 position {
            get { return new Vector3(x, y, z); } 
        }

        public Vector2 positionV2 {
            get { return new Vector2(x, z); }
        }

        public VectorInt.Vector2Int gridPos {
            get { return new VectorInt.Vector2Int(gridX, gridZ); }
        }

        public static implicit operator Vector3(CellSamplePoint obj) {
            return obj.position;
        }
        public static implicit operator Vector2(CellSamplePoint obj) {
            return obj.positionV2;
        }

        public static bool operator ==(CellSamplePoint a, CellSamplePoint b) {
            return 
                a.x == b.x && 
                a.y == b.y && 
                a.z == b.z && 
                a.passability == b.passability && 
                a.area == b.area;
        }

        public static bool operator !=(CellSamplePoint a, CellSamplePoint b) {
            return !(a == b);
        }

        public override bool Equals(object obj) {
            if (obj == null || !(obj is CellSamplePoint))
                return false;

            return Equals((CellSamplePoint)obj);
        }

        public bool Equals(CellSamplePoint other) {
            return this == other;
        }

        public override int GetHashCode() {
            return (int)(x + y + z) + (sbyte)passability;
        }

        public override string ToString() {
            return string.Format("x: {0}, y: {1}, z: {2}, Area: {3}, {4}", x, y, z, area.ToString(), passability.ToString());
        }
    }
}