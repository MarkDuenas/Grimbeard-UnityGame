using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


//this is exaple class how to use DeltaCostValue
//it is not a mandatory to use this class to tell delta cost
//it only create and update delta cost. you can do same things from any place
namespace K_PathFinder {
    public class CostModifier : MonoBehaviour {
        public AgentProperties properties;

        //initial values
        [Range(0, 31)]
        public int group;
        public AnimationCurve multiplier = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        public float maxNavmeshCost = 50f;

        //actual value. also you can just create and use it without MonoBehaviour. just use 
        //PathFinder.AddDeltaCostValue / PathFinder.RemoveDeltaCostValue / value.SetValues to interact with it
        private DeltaCostValue value;

        void OnEnable() {
            value = new DeltaCostValue(properties, transform.position, group, maxNavmeshCost, multiplier);
            PathFinder.AddDeltaCostValue(value);
        }

        void OnDisable() {
            PathFinder.RemoveDeltaCostValue(value);
        }

        private void OnDestroy() {
            PathFinder.RemoveDeltaCostValue(value);
        }

        void Update() {
            if (transform.hasChanged) {
                transform.hasChanged = false;
                value.SetValues(transform.position, maxNavmeshCost);         
            }
        }

        public void SetValues(Vector3 position, float maxNavmeshCost) {
            value.SetValues(position, maxNavmeshCost);
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(CostModifier))]
    public class CostModifierEditor : Editor {
        public void OnSceneGUI() {
            CostModifier t = (target as CostModifier);

            EditorGUI.BeginChangeCheck();
            Color color = Handles.color;
            Handles.color = Color.blue;
            float areaOfEffect = Handles.RadiusHandle(Quaternion.identity, t.transform.position, t.maxNavmeshCost);
            Handles.color = color;
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(target, "Changed Area Of Effect");
                t.maxNavmeshCost = areaOfEffect;
            }
        }
    }
#endif
}