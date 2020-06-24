using K_PathFinder.CoolTools;
using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using K_PathFinder.PFTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder {
    //class that responcible for holding some important data that userful in navmesh generation
    //excentialy it hold bounds that staked in StackedDictionary and return then when time comes
    public static partial class PathFinder {
        private static StackedDictionary<XZPosInt, IChunkContent> chunkContentMap = new StackedDictionary<XZPosInt, IChunkContent>();
        private static Dictionary<IChunkContent, Bounds2DInt> chunkContent = new Dictionary<IChunkContent, Bounds2DInt>();
        
        //internal fuction to clean up chunk content
        private static void ClearChunkContentMap() {
            chunkContentMap.Clear();
            chunkContent.Clear();
        }
             
        //function co collect chunk content
        public static void GetChunkContent<T>(int x, int z, ICollection<T> collectionToFill) where T : class, IChunkContent {
            chunkContentMap.Read(new XZPosInt(x, z), collectionToFill);
        }

        public static void GetChunkContent<T>(int x, int z, ICollection<T> collectionToFill, Predicate<T> match) where T : class, IChunkContent {
            chunkContentMap.Read(new XZPosInt(x, z), collectionToFill, match);
        }

        public static int CountContent(int x, int z) {
            return chunkContentMap.Count(new XZPosInt(x, z));
        }

        public static void RemoveChunkContent(IChunkContent removedContent) {
            AddPathfinderThreadDelegate(() => {
                Bounds2DInt metaData;
                if (chunkContent.TryGetValue(removedContent, out metaData)) {
                    GetChunkRemoveChunkContent(metaData, removedContent);
                    chunkContent.Remove(removedContent);
                }
#if UNITY_EDITOR
                SceneDebug(Debuger_K.debugSetChunkContentMap);
#endif
            });
        }

        public static void RemoveChunkContent<T>(List<T> processedContentArray) where T : IChunkContent {
            AddPathfinderThreadDelegate(() => {
                for (int i = 0; i < processedContentArray.Count; i++) {
                    IChunkContent removedContent = processedContentArray[i];
                    Bounds2DInt metaData;
                    if (chunkContent.TryGetValue(removedContent, out metaData)) {
                        GetChunkRemoveChunkContent(metaData, removedContent);
                        chunkContent.Remove(removedContent);
                    }
                }
#if UNITY_EDITOR
                SceneDebug(Debuger_K.debugSetChunkContentMap);
#endif
            });

#if UNITY_EDITOR
            SceneDebug(Debuger_K.debugSetChunkContentMap);
#endif
        }

        /// <summary>
        /// target content will be added or update it's possition if already added
        /// </summary>
        public static void ProcessChunkContent(IChunkContent processedContent) {
            AddPathfinderThreadDelegate(() => {
                Bounds2DInt processedContentBounds = ToChunkPosition(processedContent.chunkContentBounds);
                Bounds2DInt metaData;
                if (chunkContent.TryGetValue(processedContent, out metaData)) {
                    Bounds2DInt currentContentBounds = metaData;

                    if (currentContentBounds == processedContentBounds)
                        return; //nothing to change since it occupy same space

                    GetChunkRemoveChunkContent(currentContentBounds, processedContent);
                    GetChunkAddChunkContent(processedContentBounds, processedContent);
                    chunkContent[processedContent] = processedContentBounds;
                }
                else {
                    GetChunkAddChunkContent(processedContentBounds, processedContent);
                    chunkContent[processedContent] = processedContentBounds;
                }

#if UNITY_EDITOR
                SceneDebug(Debuger_K.debugSetChunkContentMap);
#endif
            });
        }
        /// <summary>
        /// target content will be added or update it's possition if already added
        /// </summary>
        public static void ProcessChunkContent<T>(List<T> processedContentList) where T : IChunkContent {
            if (processedContentList == null || processedContentList.Count == 0)
                return;

            AddPathfinderThreadDelegate(() => {
                for (int i = 0; i < processedContentList.Count; i++) {
                    IChunkContent processedContent = processedContentList[i];
                    Bounds2DInt processedContentBounds = PathFinder.ToChunkPosition(processedContent.chunkContentBounds);

                    Bounds2DInt metaData;
                    if (chunkContent.TryGetValue(processedContent, out metaData)) {
                        Bounds2DInt currentContentBounds = metaData;

                        if (currentContentBounds == processedContentBounds)
                            return; //nothing to change since it occupy same space

                        GetChunkRemoveChunkContent(currentContentBounds, processedContent);
                        GetChunkAddChunkContent(processedContentBounds, processedContent);
                        chunkContent[processedContent] = processedContentBounds;
                    }
                    else {
                        GetChunkAddChunkContent(processedContentBounds, processedContent);
                        chunkContent[processedContent] = processedContentBounds;
                    }
                }

#if UNITY_EDITOR
                SceneDebug(Debuger_K.debugSetChunkContentMap);
#endif
            });
        }

        //adding removing
        private static void GetChunkAddChunkContent(Bounds2DInt bounds, IChunkContent content) {
            for (int x = bounds.minX; x < bounds.maxX + 1; x++) {
                for (int z = bounds.minY; z < bounds.maxY + 1; z++) {
                    chunkContentMap.Add(new XZPosInt(x, z), content);
                }
            }
        }
        private static void GetChunkRemoveChunkContent(Bounds2DInt bounds, IChunkContent content) {
            for (int x = bounds.minX; x < bounds.maxX + 1; x++) {
                for (int z = bounds.minY; z < bounds.maxY + 1; z++) {
                    chunkContentMap.Remove(new XZPosInt(x, z), content);
                }
            }
        }
        
#if UNITY_EDITOR      
        private static void SceneDebug(DebugSet debugSet) {
            debugSet.Clear();
            float gs = gridSize;
            Vector3 add = new Vector3(gs * 0.5f, 0, gs * 0.5f);
            System.Random random = new System.Random();

            List<IChunkContent> TContent = new List<IChunkContent>();   

            foreach (var item in chunkContentMap.Keys) {
                chunkContentMap.Read(item, TContent);
                int x = item.x;
                int z = item.z;

                sb.Length = 0;
                int worldMods = 0;
                int trees = 0;

                Vector3 p = (new Vector3(x, 0, z) * gs) + add;

                sb.AppendFormat("x: {0}, z: {1}\n", x, z);

                Color color = new Color(1, 0, 0, 0.1f);

                debugSet.AddTriangle(
                    new Vector3(x, 0, z) * gs,
                    new Vector3(x + 1, 0, z) * gs,
                    new Vector3(x, 0, z + 1) * gs,
                    color, false);

                debugSet.AddTriangle(
                    new Vector3(x + 1, 0, z + 1) * gs,
                    new Vector3(x + 1, 0, z) * gs,
                    new Vector3(x, 0, z + 1) * gs,
                    color, false);

                Color cColor = new Color(random.Next(100) / 100f, random.Next(100) / 100f, random.Next(100) / 100f, 1f);
                foreach (var CContent in TContent) {
                    debugSet.AddBounds(CContent.chunkContentBounds, cColor);
                    debugSet.AddLine(p, CContent.chunkContentBounds.center, cColor);

                    if (CContent is AreaWorldMod)
                        worldMods++;
                    else if (CContent is Collector.PathFinderTerrainMetaData.TreeInstanceData)
                        trees++;
                    else
                        sb.AppendLine(CContent.ToString());
                }             
                

                TContent.Clear();

                if(worldMods > 0)
                    sb.AppendFormat("Mods {0} ", worldMods);
                if(trees > 0)
                    sb.AppendFormat("Trees {0} ", trees);
                debugSet.AddLabel(p, sb.ToString());
            }   
        }
#endif
    }
}