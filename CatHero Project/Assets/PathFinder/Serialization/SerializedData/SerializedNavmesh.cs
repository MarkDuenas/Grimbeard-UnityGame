using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Serialization2 {
    [Serializable]
    public class SerializedNavmesh {
        public string pathFinderVersion;
        public SerializedGraph[] serializedGraphs;

        public SerializedNavmesh(string pathFinderVersion, SerializedGraph[] serializedGraphs) {
            this.pathFinderVersion = pathFinderVersion;
            this.serializedGraphs = serializedGraphs;
        }

        public int CountCells() {
            int result = 0;
            for (int i = 0; i < serializedGraphs.Length; i++) {
                result += serializedGraphs[i].serializedCells.Length;
            }
            return result;
        }
    }
}