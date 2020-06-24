using UnityEngine;
using System.Collections;

namespace K_PathFinder {
    [RequireComponent(typeof(Terrain))][ExecuteInEditMode()]
    public class TerrainNavmeshSettings : MonoBehaviour {
        public Terrain terrain;
        [SerializeField]public int[] data; //index is splat map index. value is area index. index can only be assigned if area present in global area list

        void OnEnable() {
            terrain = GetComponent<Terrain>();
        }

        public void SetArea(int splatMapIndex, int area) {
            data[splatMapIndex] = area;
        }

        public void SetArea(int splatMapIndex, Area area) {
            if (area.id < 0)
                Debug.LogWarningFormat("area of terrain can only be assigned if it present in global area list");
            data[splatMapIndex] = area.id;
        }

        public void OnPathFinderSceneInit() {
            if (data == null) {
                Debug.Log("Creating new terrain data");
#if UNITY_2018_3_OR_NEWER
                data = new int[terrain.terrainData.terrainLayers.Length];
#else
                data = new int[terrain.terrainData.splatPrototypes.Length];
#endif
            }

            int maxAreaCount = PathFinder.areaCount;
            for (int i = 0; i < data.Length; i++) {
                if (data[i] > maxAreaCount) {
                    Debug.LogWarningFormat("On {0} terrain in data index of area was higher than it possible can be. Now it is default", gameObject.name);
                    data[i] = 0;
                }
            }
        }
    }
}