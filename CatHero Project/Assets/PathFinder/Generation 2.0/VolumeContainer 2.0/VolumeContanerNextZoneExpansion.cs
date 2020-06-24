using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    //***********************************//
    //**********Zone Expansion***********//
    //***********************************//
    //*********For expanding zones*******//
    //***********************************//

    public partial class VolumeContainerNew {
        //this part of code are dedicated to expanding sones so they can fit agent radius. 
        //this is really bunch of copy-pasted code
        //but copy-pasting kinda nesesary so this code have dicent perfomance

        //near obstacle set
        int nearObstacleSetCount = 0;//actual count is this number divided by 3
        int[] nearObstacleSet;//[x, z, index], [x, z, index], [x, z, index], and so on 

        //near crouch set
        int nearCrouchSetCount = 0;//actual count is this number divided by 3
        int[] nearCrouchSet; //[x, z, index], [x, z, index], [x, z, index], and so on
        
        #region obstacles
        private void CreateNearObstaclesSet() {
            //near obstacles
            nearObstacleSetCount = 0;
            nearObstacleSet = GenericPoolArray<int>.Take(1024);
            
            int sx = sizeX;
            int sz = sizeZ;

            if (doCrouch) {
                nearCrouchSetCount = 0;
                nearCrouchSet = GenericPoolArray<int>.Take(1024); 

                sbyte crouchVal = (sbyte)Passability.Crouchable;
                sbyte walkVal = (sbyte)Passability.Walkable;

                for (int x = 0; x < sizeX; x++) {
                    for (int z = 0; z < sizeZ; z++) {
                        var collum = collums[GetIndex(x, z)];

                        for (int i = 0; i < collum.length; i++) {
                            Data value = data[collum.index + i];

                            if (value.pass == walkVal) {             
                                if ((value.xMinus != INVALID_CONNECTION && data[GetRealIndex(x - 1, z, value.xMinus)].pass == crouchVal) |
                                    (value.xPlus != INVALID_CONNECTION && data[GetRealIndex(x + 1, z, value.xPlus)].pass == crouchVal) |
                                    (value.zMinus != INVALID_CONNECTION && data[GetRealIndex(x, z - 1, value.zMinus)].pass == crouchVal) |
                                    (value.zPlus != INVALID_CONNECTION && data[GetRealIndex(x, z + 1, value.zPlus)].pass == crouchVal)){

                                    if (nearCrouchSet.Length <= nearCrouchSetCount + 3)
                                        GenericPoolArray<int>.IncreaseSize(ref nearCrouchSet);

                                    nearCrouchSet[nearCrouchSetCount++] = x;
                                    nearCrouchSet[nearCrouchSetCount++] = z;
                                    nearCrouchSet[nearCrouchSetCount++] = collum.index + i;
                                }
                            }

                            if ((x > 0 && value.xMinus == INVALID_CONNECTION) |
                                (x < sx - 1 && value.xPlus == INVALID_CONNECTION) |
                                (z > 0 && value.zMinus == INVALID_CONNECTION) |
                                (z < sz - 1 && value.zPlus == INVALID_CONNECTION)) {

                                if (nearObstacleSet.Length <= nearObstacleSetCount + 3)
                                    GenericPoolArray<int>.IncreaseSize(ref nearObstacleSet);

                                nearObstacleSet[nearObstacleSetCount++] = x;
                                nearObstacleSet[nearObstacleSetCount++] = z;
                                nearObstacleSet[nearObstacleSetCount++] = collum.index + i;                         
                            }
                        }
                    }
                }
            }
            else {
                for (int x = 0; x < sizeX; x++) {
                    for (int z = 0; z < sizeZ; z++) {
                        var collum = collums[GetIndex(x, z)];

                        for (int i = 0; i < collum.length; i++) {
                            Data value = data[collum.index + i];

                            if ((x > 0 && value.xMinus == INVALID_CONNECTION) |
                                (x < sx - 1 && value.xPlus == INVALID_CONNECTION) |
                                (z > 0 && value.zMinus == INVALID_CONNECTION) |
                                (z < sz - 1 && value.zPlus == INVALID_CONNECTION)) {

                                if (nearObstacleSet.Length <= nearObstacleSetCount + 3)
                                    GenericPoolArray<int>.IncreaseSize(ref nearObstacleSet);

                                nearObstacleSet[nearObstacleSetCount++] = x;
                                nearObstacleSet[nearObstacleSetCount++] = z;
                                nearObstacleSet[nearObstacleSetCount++] = collum.index + i;               
                            }
                        }
                    }
                }
            }
            
            //filling corners
            byte crouchable = (byte)Passability.Crouchable;

            bool[] alreadyObstacle = GenericPoolArray<bool>.Take(dataCount, defaultValue: false);
            bool[] potentialObstacle = GenericPoolArray<bool>.Take(dataCount, defaultValue: false);
            int[] dataSet = GenericPoolArray<int>.Take(256); //[x, z, index], [x, z, index], [x, z, index]
            int dataSetCount = 0;

            for (int i = 0; i < nearObstacleSetCount; i += 3) {
                alreadyObstacle[nearObstacleSet[i + 2]] = true;
            }

            for (int i = 0; i < nearObstacleSetCount; i += 3) {    
                int x = nearObstacleSet[i];
                int z = nearObstacleSet[i + 1];
                Data d = data[nearObstacleSet[i + 2]];
                int index;

                if (dataSet.Length <= dataSetCount + 12)
                    GenericPoolArray<int>.IncreaseSize(ref dataSet);

                if (d.xPlus != -1) {
                    index = collums[(z * sx) + x + 1].index + d.xPlus;
                    if (alreadyObstacle[index] == false && potentialObstacle[index] == false && data[index].pass >= crouchable) {
                        potentialObstacle[index] = true;
                        dataSet[dataSetCount++] = x + 1;
                        dataSet[dataSetCount++] = z;
                        dataSet[dataSetCount++] = index;
                    }
                }
                if (d.xMinus != -1) {
                    index = collums[(z * sx) + x - 1].index + d.xMinus;
                    if (alreadyObstacle[index] == false && potentialObstacle[index] == false && data[index].pass >= crouchable) {
                        potentialObstacle[index] = true;
                        dataSet[dataSetCount++] = x - 1;
                        dataSet[dataSetCount++] = z;
                        dataSet[dataSetCount++] = index;
                    }
                }
                if (d.zPlus != -1) {
                    index = collums[((z + 1) * sx) + x].index + d.zPlus;
                    if (alreadyObstacle[index] == false && potentialObstacle[index] == false && data[index].pass >= crouchable) {
                        potentialObstacle[index] = true;
                        dataSet[dataSetCount++] = x;
                        dataSet[dataSetCount++] = z + 1;
                        dataSet[dataSetCount++] = index;
                    }
                }
                if (d.zMinus != -1) {
                    index = collums[((z - 1) * sx) + x].index + d.zMinus;
                    if (alreadyObstacle[index] == false && potentialObstacle[index] == false && data[index].pass >= crouchable) {
                        potentialObstacle[index] = true;
                        dataSet[dataSetCount++] = x;
                        dataSet[dataSetCount++] = z - 1;
                        dataSet[dataSetCount++] = index;
                    }
                }
            }
            
            for (int i = 0; i < dataSetCount; i += 3) {
                int x = dataSet[i];
                int z = dataSet[i + 1];
                int index = dataSet[i + 2];
                Data d = data[index];
                //Debug.LogFormat("set{7}, x {0}, z {1}, index {2}, x+ {3}, x- {4}, z+ {5}, z- {6}", x, z, index, d.xPlus, d.xMinus, d.zPlus, d.zMinus, i);

                int xPlusIndex = d.xPlus == -1 ? -1 : collums[(z * sx) + x + 1].index + d.xPlus;
                int xMinusIndex = d.xMinus == -1 ? -1 : collums[(z * sx) + x - 1].index + d.xMinus;
                int zPlusIndex = d.zPlus == -1 ? -1 : collums[((z + 1) * sx) + x].index + d.zPlus;
                int zMinusIndex = d.zMinus == -1 ? -1 : collums[((z - 1) * sx) + x].index + d.zMinus;

                //diagonal checkups
                if (((xPlusIndex != -1 && alreadyObstacle[xPlusIndex]) && (zPlusIndex != -1 && alreadyObstacle[zPlusIndex])) |     //x+ z+
                    ((xPlusIndex != -1 && alreadyObstacle[xPlusIndex]) && (zMinusIndex != -1 && alreadyObstacle[zMinusIndex])) |   //x+ z-
                    ((xMinusIndex != -1 && alreadyObstacle[xMinusIndex]) && (zPlusIndex != -1 && alreadyObstacle[zPlusIndex])) |   //x- z+
                    ((xMinusIndex != -1 && alreadyObstacle[xMinusIndex]) && (zMinusIndex != -1 && alreadyObstacle[zMinusIndex]))) {//x- z-               

                    if (nearObstacleSet.Length <= nearObstacleSetCount + 3) 
                        GenericPoolArray<int>.IncreaseSize(ref nearObstacleSet);

                    //Debuger_K.AddDot(GetPos(x, z, d.y), Color.cyan, size: 0.04f);

                    nearObstacleSet[nearObstacleSetCount++] = x;
                    nearObstacleSet[nearObstacleSetCount++] = z;
                    nearObstacleSet[nearObstacleSetCount++] = index;                
                }
            }

            GenericPoolArray<bool>.ReturnToPool(ref alreadyObstacle);
            GenericPoolArray<bool>.ReturnToPool(ref potentialObstacle);
            GenericPoolArray<int>.ReturnToPool(ref dataSet);


            //for (int i = 0; i < nearObstacleSetCount; i++) {
            //    int x = nearObstacleSetX[i];
            //    int z = nearObstacleSetZ[i];
            //    int index = nearObstacleSetIndex[i];
            //    var value = data[index];

            //    Vector3 p1 = GetPos(x,z,value.y);
            //    Debuger_K.AddRay(p1, Vector3.up, Color.cyan, 0.2f);
            //}

            //for (int i = 0; i < nearCrouchSetCount; i++) {
            //    int x = nearCrouchSetX[i];
            //    int z = nearCrouchSetZ[i];
            //    int index = nearCrouchSetIndex[i];
            //    var value = data[index];

            //    Vector3 p1 = GetPos(x, z, value.y);
            //    Debuger_K.AddRay(p1, Vector3.up, Color.red, 0.2f);
            //}
        }
        private void GrowthObstacles() {
            if (nearObstacleSetCount == 0)
                return;

            int distance = template.agentRagius - 1;
            byte unwalkable = (byte)Passability.Unwalkable;
            byte crouchable = (byte)Passability.Crouchable;

            bool[] flags = GenericPoolArray<bool>.Take(dataCount, defaultValue: false);
            int[] set = GenericPoolArray<int>.Take(nearObstacleSetCount * 3);//[x, z, index], [x, z, index], and so on
            
            int curSetBatchStart = 0;
            int curSetBatchEnd = nearObstacleSetCount;
            int sx = sizeX;

            for (int i = 0; i < nearObstacleSetCount; i += 3) {
                set[i] = nearObstacleSet[i];
                set[i + 1] = nearObstacleSet[i + 1];
                int index = nearObstacleSet[i + 2];
                set[i + 2] = index;
                data[index].pass = unwalkable;
                flags[index] = true;
            }

            //main iterations
            for (int i = 0; i < distance; i++) {
                int curBatchIndex = curSetBatchEnd;

                for (int b = curSetBatchStart; b < curSetBatchEnd; b += 3) {
                    //up to 12 new numbers can be added. [x, z, index] * 4 sides
                    if (curBatchIndex + 12 >= set.Length)
                        GenericPoolArray<int>.IncreaseSize(ref set);

                    int x = set[b];
                    int z = set[b + 1];
                    Data d = data[set[b + 2]];

                    int index;
                    Data neighbour;

                    if (d.xPlus != -1) {
                        index = collums[(z * sx) + x + 1].index + d.xPlus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass >= crouchable) {
                                data[index].pass = unwalkable;
                                set[curBatchIndex++] = x + 1;
                                set[curBatchIndex++] = z;
                                set[curBatchIndex++] = index;
                                flags[index] = true;                       
                            }
                        }
                    }
                    if (d.xMinus != -1) {
                        index = collums[(z * sx) + x - 1].index + d.xMinus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass >= crouchable) {
                                data[index].pass = unwalkable;
                                set[curBatchIndex++] = x - 1;
                                set[curBatchIndex++] = z;
                                set[curBatchIndex++] = index;
                                flags[index] = true;
                            }
                        }
                    }
                    if (d.zPlus != -1) {
                        index = collums[((z + 1) * sx) + x].index + d.zPlus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass >= crouchable) {
                                data[index].pass = unwalkable;
                                set[curBatchIndex++] = x;
                                set[curBatchIndex++] = z + 1;
                                set[curBatchIndex++] = index;
                                flags[index] = true;
                            }
                        }
                    }
                    if (d.zMinus != -1) {
                        index = collums[((z - 1) * sx) + x].index + d.zMinus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass >= crouchable) {
                                data[index].pass = unwalkable;
                                set[curBatchIndex++] = x;
                                set[curBatchIndex++] = z - 1;
                                set[curBatchIndex++] = index;
                                flags[index] = true;
                            }
                        }
                    }
                }

                curSetBatchStart = curSetBatchEnd;
                curSetBatchEnd = curBatchIndex;
            }

            GenericPoolArray<bool>.ReturnToPool(ref flags);
            GenericPoolArray<int>.ReturnToPool(ref set);
        }
        #endregion

        #region crouch
        private void CreateNearCrouchSet() {
            nearCrouchSetCount = 0;
            nearCrouchSet = GenericPoolArray<int>.Take(1024);

            sbyte crouchVal = (sbyte)Passability.Crouchable;
            sbyte walkVal = (sbyte)Passability.Walkable;

            for (int x = 0; x < sizeX; x++) {
                for (int z = 0; z < sizeZ; z++) {
                    var collum = collums[GetIndex(x, z)];

                    for (int i = 0; i < collum.length; i++) {
                        Data value = data[collum.index + i];

                        if (value.pass == walkVal) {
                            if ((value.xMinus != INVALID_CONNECTION && data[GetRealIndex(x - 1, z, value.xMinus)].pass == crouchVal) |
                                (value.xPlus != INVALID_CONNECTION && data[GetRealIndex(x + 1, z, value.xPlus)].pass == crouchVal) |
                                (value.zMinus != INVALID_CONNECTION && data[GetRealIndex(x, z - 1, value.zMinus)].pass == crouchVal) |
                                (value.zPlus != INVALID_CONNECTION && data[GetRealIndex(x, z + 1, value.zPlus)].pass == crouchVal)) {

                                if (nearCrouchSet.Length <= nearCrouchSetCount + 3)
                                    GenericPoolArray<int>.IncreaseSize(ref nearCrouchSet);

                                nearCrouchSet[nearCrouchSetCount++] = x;
                                nearCrouchSet[nearCrouchSetCount++] = z;
                                nearCrouchSet[nearCrouchSetCount++] = collum.index + i;
                            }
                        }
                    }
                }
            }
        }

        private void GrowthCrouch() {
            if (nearCrouchSetCount == 0)
                return;

            int distance = template.agentRagius - 1;
            byte walkable = (byte)Passability.Walkable;
            byte crouchable = (byte)Passability.Crouchable;

            bool[] flags = GenericPoolArray<bool>.Take(dataCount, defaultValue: false);
            int[] set = GenericPoolArray<int>.Take(nearCrouchSetCount * 3);//[x, z, index], [x, z, index], and so on

            int curSetBatchStart = 0;
            int curSetBatchEnd = nearCrouchSetCount;
            int sx = sizeX;

            for (int i = 0; i < nearCrouchSetCount; i += 3) {
                set[i] = nearCrouchSet[i];
                set[i + 1] = nearCrouchSet[i + 1];
                int index = nearCrouchSet[i + 2];
                set[i + 2] = index;
                data[index].pass = crouchable;
                flags[index] = true;
            }

            //main iterations
            for (int i = 0; i < distance; i++) {
                int curBatchIndex = curSetBatchEnd;

                for (int b = curSetBatchStart; b < curSetBatchEnd; b += 3) {
                    //up to 12 new numbers can be added. [x, z, index] * 4 sides
                    if (curBatchIndex + 12 >= set.Length)
                        GenericPoolArray<int>.IncreaseSize(ref set);

                    int x = set[b];
                    int z = set[b + 1];
                    Data d = data[set[b + 2]];

                    int index;
                    Data neighbour;

                    if (d.xPlus != -1) {
                        index = collums[(z * sx) + x + 1].index + d.xPlus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x + 1;
                                set[curBatchIndex++] = z;
                                set[curBatchIndex++] = index;
                                flags[index] = true;
                            }
                        }
                    }
                    if (d.xMinus != -1) {
                        index = collums[(z * sx) + x - 1].index + d.xMinus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x - 1;
                                set[curBatchIndex++] = z;
                                set[curBatchIndex++] = index;
                                flags[index] = true;
                            }
                        }
                    }
                    if (d.zPlus != -1) {
                        index = collums[((z + 1) * sx) + x].index + d.zPlus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x;
                                set[curBatchIndex++] = z + 1;
                                set[curBatchIndex++] = index;
                                flags[index] = true;
                            }
                        }
                    }
                    if (d.zMinus != -1) {
                        index = collums[((z - 1) * sx) + x].index + d.zMinus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x;
                                set[curBatchIndex++] = z - 1;
                                set[curBatchIndex++] = index;
                                flags[index] = true;
                            }
                        }
                    }
                }

                curSetBatchStart = curSetBatchEnd;
                curSetBatchEnd = curBatchIndex;
            }

            GenericPoolArray<bool>.ReturnToPool(ref flags);
            GenericPoolArray<int>.ReturnToPool(ref set);
        }


        private void GrowthCrouchOld() {
            if (nearCrouchSetCount == 0)
                return;

            int distance = template.agentRagius - 1;
            byte crouchable = (byte)Passability.Crouchable;
            byte walkable = (byte)Passability.Walkable;

            bool[] flags = GenericPoolArray<bool>.Take(dataCount, defaultValue: false);
            int[] set = GenericPoolArray<int>.Take(nearCrouchSetCount * 4);//[x, z, index], [x, z, index], and so on

            int curSetBatchStart = 0;
            int curSetBatchEnd = nearCrouchSetCount * 3;
            int sx = sizeX;

            for (int i = 0; i < nearCrouchSetCount; i++) {
                set[i] = nearCrouchSet[(i * 3)];//x
                set[i + 1] = nearCrouchSet[(i * 3) + 1];//z
                int index = nearCrouchSet[(i * 3) + 2];
                set[i + 2] = index;
                data[index].pass = crouchable;
                flags[index] = true;
            }

            for (int i = 0; i < distance; i++) {
                int curBatchIndex = curSetBatchEnd;
                for (int b = curSetBatchStart; b < curSetBatchEnd; b += 3) {
                    //up to 12 new numbers can be added. [x, z, index] * 4 sides
                    if (curBatchIndex + 12 >= set.Length)
                        GenericPoolArray<int>.IncreaseSize(ref set);

                    int x = set[b];
                    int z = set[b + 1];
                    Data d = data[set[b + 2]];

                    int index;
                    Data neighbour;

                    if (d.xPlus != -1) {
                        index = collums[(z * sx) + x + 1].index + d.xPlus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x + 1;
                                set[curBatchIndex++] = z;
                                set[curBatchIndex++] = index;
                            }
                        }
                    }
                    if (d.xMinus != -1) {
                        index = collums[(z * sx) + x - 1].index + d.xMinus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x - 1;
                                set[curBatchIndex++] = z;
                                set[curBatchIndex++] = index;
                            }
                        }
                    }
                    if (d.zPlus != -1) {
                        index = collums[((z + 1) * sx) + x].index + d.zPlus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x;
                                set[curBatchIndex++] = z + 1;
                                set[curBatchIndex++] = index;
                            }
                        }
                    }
                    if (d.zMinus != -1) {
                        index = collums[((z - 1) * sx) + x].index + d.zMinus;
                        if (flags[index] == false) {
                            neighbour = data[index];
                            if (neighbour.pass == walkable) {
                                data[index].pass = crouchable;
                                set[curBatchIndex++] = x;
                                set[curBatchIndex++] = z - 1;
                                set[curBatchIndex++] = index;
                            }
                        }
                    }
                }

                curSetBatchStart = curSetBatchEnd;
                curSetBatchEnd = curBatchIndex;
            }

            GenericPoolArray<bool>.ReturnToPool(ref flags);
            GenericPoolArray<int>.ReturnToPool(ref set);
        }
        #endregion
    }
}