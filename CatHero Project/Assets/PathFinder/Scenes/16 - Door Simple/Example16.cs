using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace K_PathFinder.Samples {
    [RequireComponent(typeof(AreaWorldMod))]
    public class Example16 : MonoBehaviour {
        public GameObject doorObject;
        AreaWorldMod mode;
        bool state;

        // Use this for initialization
        void Start() {
            mode = GetComponent<AreaWorldMod>();
            SetState(true);
        }
        
        public void Toggle() {
            SetState(!state);
        }

        void SetState(bool newState) {
            state = newState;

            if (newState) {
                doorObject.SetActive(true);
                mode.SetCellsState(false);
            }
            else {
                doorObject.SetActive(false);
                mode.SetCellsState(true);
            }
        }
    }
}