using K_PathFinder.Rasterization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Collector {
    public partial class ShapeCollector {
        //public void AppendTerrain(CSRasterization2DResult rasterizationResult, TerrainColliderInfoMesh terrainInfo) {
        //    var voxels = rasterizationResult.voxels;
        //    for (int x = 0; x < sizeX; x++) {
        //        for (int z = 0; z < sizeZ; z++) {
        //            var curVoxel = voxels[x + (z * sizeX)];
        //            if (curVoxel.exist)
        //                SetVoxel(x, z, curVoxel.height - 20f, curVoxel.height, (sbyte)curVoxel.passability);
        //        }
        //    }
        //}
//#if UNITY_EDITOR
//        private DataCompact[] GetTerrainCompactData(TerrainColliderInfoMesh terrain) {
//            var compactData = TakeCompactData();
//            int hSizeX = terrain.hSizeX;
//            int hSizeZ = terrain.hSizeZ;
//            int resolution = terrain.resolution;
//            float[,] heightMap = terrain.heightMap;
//            Matrix4x4 heightMatrix = terrain.heightMatrix;

//            for (int x = 0; x < hSizeX - 1; x++) {
//                for (int z = 0; z < hSizeZ - 1; z++) {
//                    Vector3 pBL = heightMatrix.MultiplyPoint3x4(new Vector3(x, heightMap[z * resolution, x * resolution], z));
//                    Vector3 pBR = heightMatrix.MultiplyPoint3x4(new Vector3((x + 1), heightMap[z * resolution, (x + 1) * resolution], z));
//                    Vector3 pTL = heightMatrix.MultiplyPoint3x4(new Vector3(x, heightMap[(z + 1) * resolution, x * resolution], (z + 1)));
//                    Vector3 pTR = heightMatrix.MultiplyPoint3x4(new Vector3((x + 1), heightMap[(z + 1) * resolution, (x + 1) * resolution], (z + 1)));


//                    PFDebuger.Debuger_K.AddDot(pBL, Color.red);
//                    PFDebuger.Debuger_K.AddDot(pBR, Color.red);
//                    PFDebuger.Debuger_K.AddDot(pTL, Color.red);
//                    PFDebuger.Debuger_K.AddDot(pTR, Color.red);
//                    PFDebuger.Debuger_K.AddLine(pBL, pBR, Color.red);
//                    PFDebuger.Debuger_K.AddLine(pTL, pTR, Color.red);
//                    PFDebuger.Debuger_K.AddLine(pBL, pTL, Color.red);
//                    PFDebuger.Debuger_K.AddLine(pBR, pTR, Color.red);               
//                    PFDebuger.Debuger_K.AddLine(pBL, pTR, Color.red);
//                }
//            }

//            return compactData;
//        }

//        public void AppendTerrain(TerrainColliderInfoMesh terrain, Area area) {
//            //rasterization preparings
//            Debug.Log("this part of code should not be used right now");

//            var compactData = GetTerrainCompactData(terrain);
//            //AppendCompactData(compactData, GetAreaValue(area));
//        }
//#endif

        private DataCompact[] GetTerrainCompactData(Vector3[] vrts, int[] trs, int trisCount) {
            var compactData = TakeCompactData();

            float maxSlopeCos = Mathf.Cos(template.maxSlope * Mathf.PI / 180.0f);
            float voxelSize = template.voxelSize;

            Vector3 realChunkPos = template.realOffsetedPosition;
            float chunkPosX = realChunkPos.x;
            float chunkPosZ = realChunkPos.z;

            int offsetX = Mathf.RoundToInt(chunkPosX / voxelSize);
            int offsetZ = Mathf.RoundToInt(chunkPosZ / voxelSize);

            int sizeX = template.lengthX_extra;
            int sizeZ = template.lengthZ_extra;

            //actual rasterization
            for (int i = 0; i < trisCount; i += 3) {
                Vector3 A = vrts[trs[i]];
                Vector3 B = vrts[trs[i + 1]];
                Vector3 C = vrts[trs[i + 2]];

                sbyte passability = CalculateWalk(A, B, C, maxSlopeCos) ? (sbyte)Passability.Walkable : (sbyte)Passability.Slope;//if true then walkable else slope;

                int minX = Mathf.Clamp(Mathf.FloorToInt(SomeMath.Min(A.x, B.x, C.x) / voxelSize) - offsetX, 0, sizeX);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(SomeMath.Max(A.x, B.x, C.x) / voxelSize) - offsetX, 0, sizeX);
                int minZ = Mathf.Clamp(Mathf.FloorToInt(SomeMath.Min(A.z, B.z, C.z) / voxelSize) - offsetZ, 0, sizeZ);
                int maxZ = Mathf.Clamp(Mathf.CeilToInt(SomeMath.Max(A.z, B.z, C.z) / voxelSize) - offsetZ, 0, sizeZ);

                for (int x = minX; x < maxX; x++) {
                    for (int z = minZ; z < maxZ; z++) {
                        float pointX = (x * voxelSize) + chunkPosX;
                        float pointZ = (z * voxelSize) + chunkPosZ;
                        if (SomeMath.LineSide(A.x, A.z, B.x, B.z, pointX, pointZ) <= 0.001 &
                            SomeMath.LineSide(B.x, B.z, C.x, C.z, pointX, pointZ) <= 0.001 &
                            SomeMath.LineSide(C.x, C.z, A.x, A.z, pointX, pointZ) <= 0.001) {
                            float height = SomeMath.CalculateHeight(A, B, C, pointX, pointZ);

                            compactData[GetIndex(x, z)].Update(height - 20, height, passability);
                        }
                    }
                }
            }

            return compactData;
        }

        public void AppendTerrain(Vector3[] vrts, int[] trs, int trisCount, Area area) {
            //rasterization preparings
            var compactData = GetTerrainCompactData(vrts, trs, trisCount);
            AppendCompactData(compactData, GetAreaValue(area));
        }

        public void AppendTerrain(Vector3[] vrts, int[] trs, int trisCount, byte[] area) {
            //rasterization preparings
            var compactData = GetTerrainCompactData(vrts, trs, trisCount);
            AppendCompactData(compactData, area);
        }
    }
}