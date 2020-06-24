using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.PFTools {
    //have no use ATM but it was fine thing to test and i probably revert some things to it later on
    public class GenericMap<MapType> where MapType : class, new() {
        private Bounds2DInt mapSpace;
        private MapType[,] map = null;

        private void ResizeMap(Bounds2DInt newMapSpace) {
            if (newMapSpace.minX > mapSpace.minX ||
                newMapSpace.minY > mapSpace.minY ||
                newMapSpace.maxX < mapSpace.maxX ||
                newMapSpace.maxY < mapSpace.maxY)
                throw new ArgumentException(
                    String.Format("Sizes of content map can only be expanded. Old: x {0}, New: x {1}", mapSpace, newMapSpace));

            if (mapSpace == newMapSpace)
                return;

            int offsetX = mapSpace.minX - newMapSpace.minX;
            int offsetZ = mapSpace.minY - newMapSpace.minY;
            int newSizeX = newMapSpace.sizeX;
            int newSizeZ = newMapSpace.sizeZ;

            MapType[,] newMap = new MapType[newSizeX, newSizeZ];

            for (int x = 0; x < mapSpace.sizeX; x++) {
                for (int z = 0; z < mapSpace.sizeZ; z++) {
                    newMap[x + offsetX, z + offsetZ] = map[x, z];
                }
            }

            mapSpace = newMapSpace;
            map = newMap;
        }

        public void IncludeBounds(int startX, int startZ, int endX, int endZ) {
            Bounds2DInt boundsPlusOne = new Bounds2DInt(startX, startZ, endX + 1, endZ + 1);
            if (map == null) {
                mapSpace = boundsPlusOne;
                map = new MapType[boundsPlusOne.sizeX, boundsPlusOne.sizeZ];
            }
            else {
                ResizeMap(Bounds2DInt.GetIncluded(mapSpace, boundsPlusOne));
            }
        }
        public void IncludeBounds(XZPosInt pos) {
            IncludeBounds(pos.x, pos.z, pos.x, pos.z);
        }
        public void IncludeBounds(Bounds2DInt bounds) {
            IncludeBounds(bounds.minX, bounds.minY, bounds.maxX, bounds.maxY);
        }

        public MapType GetChunkContent(int x, int z) {
            x = x - mapSpace.minX;
            z = z - mapSpace.minY;

            MapType result = map[x, z];
            if (result == null) {
                result = new MapType();
                map[x, z] = result;
            }
            return result;
        }

        public bool TryGetChunkContent(int x, int z, out MapType content) {
            if (x >= mapSpace.minX &&
                z >= mapSpace.minY &&
                x < mapSpace.maxX &&
                z < mapSpace.maxY) {
                content = GetChunkContent(x, z);
                return content != null;
            }
            else {
                content = null;
                return false;
            }
        }

        public void Clear() {
            mapSpace = Bounds2DInt.zero;
            map = null;
        }

        private int startX {
            get { return mapSpace.minX; }
        }
        private int startZ {
            get { return mapSpace.minY; }
        }
        private int endX {
            get { return mapSpace.maxX; }
        }
        private int endZ {
            get { return mapSpace.maxY; }
        }
    }
}
