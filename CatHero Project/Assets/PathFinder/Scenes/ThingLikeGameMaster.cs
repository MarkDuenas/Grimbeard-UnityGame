using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {
    public class ThingLikeGameMaster : MonoBehaviour {
        private void Update() {
            PathFinder.UpdateRVO();
        }
    }
}