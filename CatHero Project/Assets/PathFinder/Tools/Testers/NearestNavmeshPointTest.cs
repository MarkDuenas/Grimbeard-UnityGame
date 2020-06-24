using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
namespace K_PathFinder.Tester {
    [ExecuteInEditMode()]
    public class NearestNavmeshPointTest : MonoBehaviour {
        Vector3 p;
        public AgentProperties properties;

        public bool clearGeneric = true;

        void Update() {
            if(properties != null) {
                p = transform.position;
                var sample = PathFinder.TryGetClosestCell(p, properties);

                if(sample.type != NavmeshSampleResultType.InvalidNoNavmeshFound) {
                    if(clearGeneric)
                        Debuger_K.ClearGeneric();

                    Debuger_K.AddLine(p, sample.cell.centerVector3, Color.blue);

                    if (sample.type == NavmeshSampleResultType.OutsideNavmesh) {
                        Debuger_K.AddLine(p, sample.position, Color.red);
                    }
                    else {
                        Debuger_K.AddLine(p, sample.position, Color.green);
                    }
                }
            }
        }
    }
}
#endif