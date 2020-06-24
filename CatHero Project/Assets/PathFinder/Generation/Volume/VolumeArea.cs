using System.Collections.Generic;
using UnityEngine;
using K_PathFinder.EdgesNameSpace;
using K_PathFinder.GraphGeneration;
using K_PathFinder.Graphs;

namespace K_PathFinder {
    /// <summary>
    /// class for storing volume area
    /// it stored position, it added to volume as reference and extracted due marching sqares iterate throu volume. 
    /// then short edges also stored reference to that. and finaly after ramer douglas peuker algorithm colume area gets final set of edges it belong
    /// </summary>
    public class VolumeArea {
        public Vector3 position;
        public AreaType areaType;//cause there may be some types of area. jump points and cover points right now
     
        private HashSet<EdgeAbstract> _edges = new HashSet<EdgeAbstract>();
        private HashSet<GraphGeneratorNew.NodeEdgeValue> _edgesNew = new HashSet<GraphGeneratorNew.NodeEdgeValue>();
        public List<CellContentData> cellContentDatas = new List<CellContentData>();
        
        public VolumeArea(Vector3 position, AreaType areaType) {
            this.position = position;
            this.areaType = areaType;
        }

        //add edges
        public void AddEdge(EdgeAbstract edge) {
            _edges.Add(edge);
        }
        public void AddEdge(IEnumerable<EdgeAbstract> edges) {
            foreach (var item in edges) {
                AddEdge(item);
            }
        }
        public void AddEdge(IEnumerable<EdgeTemp> edges) {
            foreach (var item in edges) {
                AddEdge(item);
            }
        }

        public void AddEdge(GraphGeneratorNew.NodeEdgeValue edge) {
            _edgesNew.Add(edge);
        }
        public void AddEdge(IEnumerable<GraphGeneratorNew.NodeEdgeValue> edges) {
            foreach (var item in edges) {
                AddEdge(item);
            }
        }

        //acessor
        public IEnumerable<EdgeAbstract> edges {
            get { return _edges; }
        }
        public IEnumerable<GraphGeneratorNew.NodeEdgeValue> edgesNew {
            get { return _edgesNew; }
        }
    }

}