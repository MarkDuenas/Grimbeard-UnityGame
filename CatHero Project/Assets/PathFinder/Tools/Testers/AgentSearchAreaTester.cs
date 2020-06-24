using K_PathFinder;
using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using K_PathFinder.Samples;
using UnityEngine;

namespace K_PathFinder.Tester {
    [RequireComponent(typeof(LineRenderer), typeof(PathFinderAgent))]
    public class AgentSearchAreaTester : MonoBehaviour, IPathOwner {
        LineRenderer _line;
        NavMeshPathQueryTargetArea query;

        public AgentProperties properties;

        [Area] public int targetArea;

        void Start() {
            _line = GetComponent<LineRenderer>();
        }

        void Update() {
            if (properties != null) {
                if (query == null) {
                    query = new NavMeshPathQueryTargetArea(properties, this);
                    query.SetOnFinishedDelegate(RecivePathDelegate, AgentDelegateMode.ThreadSafe);
                }

                PathFinder.QueueGraph(new Bounds(transform.position, Vector3.one * 30), properties);
                query.QueueWork(transform.position, 100, targetArea);
            }
        }

        private void RecivePathDelegate(Path path) {
            ExampleThings.PathToLineRenderer(transform.position, _line, path, 0.2f);
        }

        public virtual Vector3 pathFallbackPosition {
            get { return transform.position; }
        }
    }
}