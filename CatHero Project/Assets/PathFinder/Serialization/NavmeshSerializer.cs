using K_PathFinder.Graphs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace K_PathFinder.Serialization2 {
    public class NavmeshSerializer {
        List<Graph> targetGraphs = new List<Graph>();
        Dictionary<GameObject, int> gameObjectLibrary;

        public int serializedGraphsCount = 0;
        public int serializedCellsCount =0;

        public NavmeshSerializer(AgentProperties properties, Dictionary<GeneralXZData, Graph> chunkData, Dictionary<GameObject, int> gameObjectLibraryIDs) {
            gameObjectLibrary = new Dictionary<GameObject, int>(gameObjectLibraryIDs);

            foreach (var graph in chunkData.Values) {
                if (graph.empty || graph.properties != properties)
                    continue;

                targetGraphs.Add(graph);
            }
        }
        
        public SerializedNavmesh Serialize(string version) {
            SerializedGraph[] serializedGraphs = new SerializedGraph[targetGraphs.Count];

            for (int i = 0; i < targetGraphs.Count; i++) {
                serializedGraphs[i] = new SerializedGraph(this, targetGraphs[i]);
            }

            SerializedNavmesh serializedNavmesh = new SerializedNavmesh(version, serializedGraphs);
            serializedGraphsCount += serializedNavmesh.serializedGraphs.Length;
            foreach (var item in serializedNavmesh.serializedGraphs) {
                serializedCellsCount += item.serializedCells.Length;
            }

            return serializedNavmesh;
        }

        public int GetGameObjectID(GameObject go) {
            int result;

            if (gameObjectLibrary.TryGetValue(go, out result) == false) {
                result = gameObjectLibrary.Count;
                gameObjectLibrary.Add(go, result);
            }
            return result;
        }
    }
}