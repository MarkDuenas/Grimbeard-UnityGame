using K_PathFinder.CoolTools;
using K_PathFinder.EdgesNameSpace;
using K_PathFinder.PFDebuger;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    //**********************************//
    //*********Layer Separator**********//
    //**********************************//
    //*******For separating layers******//
    //**********************************//

    public partial class VolumeContainerNew {
        struct CompactSortableData {
            public int index, y;
            public ushort x, z;


            public void Set(int Index, int Y, ushort X, ushort Z) {
                index = Index;
                y = Y;
                x = X;
                z = Z;
            }
        }

        class LayerInfoHolder {
            public bool isValid;
            public bool[] mask;

            public IndexPair[] indexes;
            public int indexesCount;

            public int[] banned;
            public int bannedCount;

            public struct IndexPair {
                public int maskIndex, globalIndex;

                public void Set(int MaskIndex, int GlobalIndex) {
                    maskIndex = MaskIndex;
                    globalIndex = GlobalIndex;
                }
            }

            public LayerInfoHolder(int Length) {
                isValid = true;
                mask = GenericPoolArray<bool>.Take(Length, makeDefault: true);

                indexes = GenericPoolArray<IndexPair>.Take(Length, makeDefault: true);
                indexesCount = 0;

                banned = GenericPoolArray<int>.Take(Length, makeDefault: true);
                bannedCount = 0;
            }

            public void Release() {
                if (isValid) {
                    GenericPoolArray<bool>.ReturnToPool(ref mask);
                    GenericPoolArray<IndexPair>.ReturnToPool(ref indexes);
                    GenericPoolArray<int>.ReturnToPool(ref banned);
                    isValid = false;
                }
            }

            public void Merge(LayerInfoHolder mergeWith) {
                if (mergeWith.isValid == false) {
                    Debug.LogError("You merge with invalid layer");
                    return;
                }

                IndexPair[] mergeIndexes = mergeWith.indexes;
                int mergeIndexesFilled = mergeWith.indexesCount;

                for (int i = 0; i < mergeIndexesFilled; i++) {
                    IndexPair pair = mergeIndexes[i];
                    mask[pair.maskIndex] = true;
                    indexes[indexesCount++] = pair;
                }
            }

            public void AddValue(int maskIndex, int globalIndex) {
                mask[maskIndex] = true;
                indexes[indexesCount++].Set(maskIndex, globalIndex);
            }

            public void AddBanned(int bannedID) {
                banned[bannedCount++] = bannedID;
            }

            public bool IsBanned(int target) {
                for (int i = 0; i < bannedCount; i++) {
                    if (banned[i] == target)
                        return true;
                }
                return false;
            }

            public bool IsIntersect(LayerInfoHolder otherLayer) {
                int OtherLayerIndexesCount = otherLayer.indexesCount;
                var otherLayerIndexes = otherLayer.indexes;

                for (int i = 0; i < OtherLayerIndexesCount; i++) {
                    if (mask[otherLayerIndexes[i].maskIndex])
                        return true;
                }
                return false;
            }
        }
        
        void SplitToLayers(out short maxLayersCount) {
            //var sortTime = new System.Diagnostics.Stopwatch();
            var totalTime = new System.Diagnostics.Stopwatch();
            totalTime.Start();
            
            var lastCollum = collums[collumsCount - 1];
            int maxIndex = lastCollum.index + lastCollum.length;

            CompactSortableData[] sortableData = GenericPoolArray<CompactSortableData>.Take(maxIndex);

            float heightError = template.voxelSize / 10;

            for (ushort x = 0; x < sizeX; x++) {
                for (ushort z = 0; z < sizeZ; z++) {
                    IndexLengthInt collum = collums[(z * sizeX) + x];
                    for (int i = 0; i < collum.length; i++) {
                        int index = collum.index + i;                  
                        sortableData[index].Set(index, (int)(data[index].y / heightError), x, z);
                    }
                }
            }


            if (maxIndex > 1) {
                Quicksort(sortableData, 0, maxIndex - 1);
            }

            //float min = sortableData[0].y;
            //float max = sortableData[sortableData.Length - 1].y;
            //totalTime.Stop();
            //for (int i = 0; i < sortableData.Length; i++) {
            //    var value = sortableData[i];
            //    //float lerp = Mathf.InverseLerp(min, max, value.y);
            //    float lerp = Mathf.InverseLerp(0, sortableData.Length, i);
            //    Vector3 p = GetPos(value.x, value.z, value.y * heightError);
            //    Color col = new Color(1f - lerp, lerp, 0, 1f);
            //    //Color col = Color.black; ;
            //    Debuger_K.AddDot(p, col);
            //    //Debuger_K.AddLabelFormat(p, "{0}", i);
            //}
            //totalTime.Start();

            int flatSize = collumsCount;
            LayerInfoHolder[] layersArray = new LayerInfoHolder[10000];
            short layerCount = 0;

            for (int i = 0; i < maxIndex; i++) {
                CompactSortableData sortedData = sortableData[i];
                int dataIndex = sortedData.index;
                Data value = data[dataIndex];
 
                int collumIndex = (sortedData.z * sizeX) + sortedData.x;

                //unfolded calls
                if (value.xPlus != -1) {
                    short neighbourLayer = data[collums[(sortedData.z * sizeX) + (sortedData.x + 1)].index + value.xPlus].layer;

                    if (neighbourLayer != -1) {
                        LayerInfoHolder neighbourLayerData = layersArray[neighbourLayer];

                        if (neighbourLayerData.mask[collumIndex] == false) {
                            short dataLayer = data[dataIndex].layer;

                            if (dataLayer == -1) {
                                data[dataIndex].layer = neighbourLayer;
                                neighbourLayerData.AddValue(collumIndex, dataIndex);
                            }
                            else {
                                LayerInfoHolder currentLayerData = layersArray[dataLayer];
                                if (currentLayerData.IsBanned(neighbourLayer) == false) {
                                    if (currentLayerData.IsIntersect(neighbourLayerData)) {
                                        neighbourLayerData.AddBanned(dataLayer);
                                        currentLayerData.AddBanned(neighbourLayer);
                                    }
                                    else {
                                        LayerInfoHolder.IndexPair[] neighbourIndexes = neighbourLayerData.indexes;
                                        int neighbourIndexCount = neighbourLayerData.indexesCount;
                                        for (int nbValue = 0; nbValue < neighbourIndexCount; nbValue++) { data[neighbourIndexes[nbValue].globalIndex].layer = dataLayer; }
                                        currentLayerData.Merge(neighbourLayerData);
                                        neighbourLayerData.Release();
                                    }
                                }
                            }
                        }
                    }
                }

                if (value.xMinus != -1) {
                    short neighbourLayer = data[collums[(sortedData.z * sizeX) + (sortedData.x - 1)].index + value.xMinus].layer;

                    if (neighbourLayer != -1) {
                        LayerInfoHolder neighbourLayerData = layersArray[neighbourLayer];

                        if (neighbourLayerData.mask[collumIndex] == false) {
                            short dataLayer = data[dataIndex].layer;

                            if (dataLayer == -1) {
                                data[dataIndex].layer = neighbourLayer;
                                neighbourLayerData.AddValue(collumIndex, dataIndex);
                            }
                            else {
                                LayerInfoHolder currentLayerData = layersArray[dataLayer];
                                if (currentLayerData.IsBanned(neighbourLayer) == false) {
                                    if (currentLayerData.IsIntersect(neighbourLayerData)) {
                                        neighbourLayerData.AddBanned(dataLayer);
                                        currentLayerData.AddBanned(neighbourLayer);
                                    }
                                    else {
                                        LayerInfoHolder.IndexPair[] neighbourIndexes = neighbourLayerData.indexes;
                                        int neighbourIndexCount = neighbourLayerData.indexesCount;
                                        for (int nbValue = 0; nbValue < neighbourIndexCount; nbValue++) { data[neighbourIndexes[nbValue].globalIndex].layer = dataLayer; }
                                        currentLayerData.Merge(neighbourLayerData);
                                        neighbourLayerData.Release();
                                    }
                                }
                            }
                        }
                    }
                }

                if (value.zPlus != -1) {
                    short neighbourLayer = data[collums[((sortedData.z + 1) * sizeX) + sortedData.x].index + value.zPlus].layer;

                    if (neighbourLayer != -1) {
                        LayerInfoHolder neighbourLayerData = layersArray[neighbourLayer];

                        if (neighbourLayerData.mask[collumIndex] == false) {
                            short dataLayer = data[dataIndex].layer;

                            if (dataLayer == -1) {
                                data[dataIndex].layer = neighbourLayer;
                                neighbourLayerData.AddValue(collumIndex, dataIndex);
                            }
                            else {
                                LayerInfoHolder currentLayerData = layersArray[dataLayer];
                                if (currentLayerData.IsBanned(neighbourLayer) == false) {
                                    if (currentLayerData.IsIntersect(neighbourLayerData)) {
                                        neighbourLayerData.AddBanned(dataLayer);
                                        currentLayerData.AddBanned(neighbourLayer);
                                    }
                                    else {
                                        LayerInfoHolder.IndexPair[] neighbourIndexes = neighbourLayerData.indexes;
                                        int neighbourIndexCount = neighbourLayerData.indexesCount;
                                        for (int nbValue = 0; nbValue < neighbourIndexCount; nbValue++) { data[neighbourIndexes[nbValue].globalIndex].layer = dataLayer; }
                                        currentLayerData.Merge(neighbourLayerData);
                                        neighbourLayerData.Release();
                                    }
                                }
                            }
                        }
                    }
                }


                if (value.zMinus != -1) {
                    short neighbourLayer = data[collums[((sortedData.z - 1) * sizeX) + sortedData.x].index + value.zMinus].layer;

                    if (neighbourLayer != -1) {
                        LayerInfoHolder neighbourLayerData = layersArray[neighbourLayer];

                        if (neighbourLayerData.mask[collumIndex] == false) {
                            short dataLayer = data[dataIndex].layer;

                            if (dataLayer == -1) {
                                data[dataIndex].layer = neighbourLayer;
                                neighbourLayerData.AddValue(collumIndex, dataIndex);
                            }
                            else {
                                LayerInfoHolder currentLayerData = layersArray[dataLayer];
                                if (currentLayerData.IsBanned(neighbourLayer) == false) {
                                    if (currentLayerData.IsIntersect(neighbourLayerData)) {
                                        neighbourLayerData.AddBanned(dataLayer);
                                        currentLayerData.AddBanned(neighbourLayer);
                                    }
                                    else {
                                        LayerInfoHolder.IndexPair[] neighbourIndexes = neighbourLayerData.indexes;
                                        int neighbourIndexCount = neighbourLayerData.indexesCount;
                                        for (int nbValue = 0; nbValue < neighbourIndexCount; nbValue++) { data[neighbourIndexes[nbValue].globalIndex].layer = dataLayer; }
                                        currentLayerData.Merge(neighbourLayerData);
                                        neighbourLayerData.Release();
                                    }
                                }
                            }
                        }
                    }
                }
                
                //for (int dir = 0; dir < 4; dir++) {
                //    sbyte connection = value.GetConnection((Directions)dir);
                //    if (connection == -1)
                //        continue;
                //    int x = sortedData.x;
                //    int z = sortedData.z;
                //    switch ((Directions)dir) {
                //        case Directions.xPlus: x += 1; break;
                //        case Directions.xMinus: x -= 1; break;
                //        case Directions.zPlus: z += 1; break;
                //        case Directions.zMinus: z -= 1; break;
                //    }
                //    short neighbourLayer = data[collums[(z * sizeX) + x].index + connection].layer;
                //    if (neighbourLayer != -1) {
                //        LayerInfoHolder neighbourLayerData = layersArray[neighbourLayer];
                //        if (neighbourLayerData.mask[collumIndex] == false) {
                //            short dataLayer = data[dataIndex].layer;
                //            if (dataLayer == -1) {
                //                data[dataIndex].layer = neighbourLayer;
                //                neighbourLayerData.AddValue(collumIndex, dataIndex);
                //            }
                //            else {
                //                LayerInfoHolder currentLayerData = layersArray[dataLayer];
                //                if (currentLayerData.IsBanned(neighbourLayer) == false) {
                //                    if (currentLayerData.IsIntersect(neighbourLayerData)) {
                //                        neighbourLayerData.AddBanned(dataLayer);
                //                        currentLayerData.AddBanned(neighbourLayer);
                //                    }
                //                    else {
                //                        LayerInfoHolder.IndexPair[] neighbourIndexes = neighbourLayerData.indexes;
                //                        int neighbourIndexCount = neighbourLayerData.indexesCount;
                //                        for (int nbValue = 0; nbValue < neighbourIndexCount; nbValue++) { data[neighbourIndexes[nbValue].globalIndex].layer = dataLayer; }
                //                        currentLayerData.Merge(neighbourLayerData);
                //                        neighbourLayerData.Release();
                //                    }
                //                }
                //            }
                //        }
                //    }
                //}


                if (data[dataIndex].layer == -1) {
                    short newLayerIndex = layerCount++;
                    LayerInfoHolder newHolder = new LayerInfoHolder(flatSize);
                    newHolder.AddValue(collumIndex, dataIndex);
                    layersArray[newLayerIndex] = newHolder;
                    data[dataIndex].layer = newLayerIndex;                 
                }
            }

            totalTime.Stop();

            maxLayersCount = 0;
            
            for (int layer = 0; layer < layerCount; layer++) {
                var curLayer = layersArray[layer];
                if (curLayer.isValid) {
                    int indexesCount = curLayer.indexesCount;
                    var indexes = curLayer.indexes;

                    for (int indexC = 0; indexC < indexesCount; indexC++) {
                        data[indexes[indexC].globalIndex].layer = maxLayersCount;
                    }
                    maxLayersCount++;
                }
            }


            //string s = "";
            //s += string.Format("{0} total time\n", totalTime.Elapsed);
            //Debug.Log(s);
            //for (ushort x = 0; x < sizeX; x++) {
            //    for (ushort z = 0; z < sizeZ; z++) {
            //        IndexLengthInt collum = collums[(z * sizeX) + x];
            //        for (int i = 0; i < collum.length; i++) {
            //            var val = data[collum.index + i];
            //            Vector3 p = GetPos(x, z, val.y);
            //            //Color col = IntToColor(val.layer);
            //            //Debuger_K.AddDot(p, col);
            //            Debuger_K.AddLabel(p, val.layer);
            //        }
            //    }
            //}

            for (int i = 0; i < layerCount; i++) {
                layersArray[i].Release();
            }
            GenericPoolArray<CompactSortableData>.ReturnToPool(ref sortableData);
        }
        
        private static int Comparison(CompactSortableData L, CompactSortableData R) {
            if (L.y < R.y) return -1; if (L.y > R.y) return 1;//y
            if (L.z < R.z) return -1; if (L.z > R.z) return 1;//z
            if (L.x < R.x) return -1; else return 0;          //x          
        }

        private static void Quicksort(CompactSortableData[] dataArray, int leftStart, int rightStart) {
            int left = leftStart, right = rightStart;
            CompactSortableData pivot = dataArray[(leftStart + rightStart) / 2];

            while (left <= right) {
                //while (Comparison(elements[i_left], pivot) < 0) { i_left++; }
                //while (Comparison(elements[i_right], pivot) > 0) { i_right--; }

                while (true) {
                    CompactSortableData data = dataArray[left];
                    if (data.y < pivot.y) { left++; continue; } if (data.y > pivot.y) break;
                    if (data.z < pivot.z) { left++; continue; } if (data.z > pivot.z) break;
                    if (data.x < pivot.x) { left++; continue; } else break;
                }

                while (true) {
                    CompactSortableData data = dataArray[right];
                    if (data.y > pivot.y) { right--; continue; } if (data.y < pivot.y) break;
                    if (data.z > pivot.z) { right--; continue; } if (data.z < pivot.z) break;
                    if (data.x > pivot.x) { right--; continue; } else break;
                }

                if (left <= right) {
                    CompactSortableData tempData = dataArray[left];
                    dataArray[left++] = dataArray[right];
                    dataArray[right--] = tempData;  
                }
            }
       
            if (leftStart < right) { Quicksort(dataArray, leftStart, right); }
            if (left < rightStart) { Quicksort(dataArray, left, rightStart); }
        }
    }
}