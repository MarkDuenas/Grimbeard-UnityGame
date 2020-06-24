using K_PathFinder;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


#if UNITY_EDITOR
namespace K_PathFinder.Tester {
    [ExecuteInEditMode()]
    public class ThingyCostMod : MonoBehaviour {
        public AgentProperties properties;
        public int group;
        [Range(-1f, 1f)] public AnimationCurve multiplier;
        public float maxNavmeshCost = 50f;
        DeltaCostValue value;



        // Start is called before the first frame update
        public void OnEnable() {
            value = new DeltaCostValue(properties, transform.position, group, maxNavmeshCost, multiplier);
            PathFinder.AddDeltaCostValue(value);
        }

        // Update is called once per frame
        public void Update() {
            value.SetValues(transform.position, maxNavmeshCost);
            PathFinder.Update();
        }
    }

    [CustomEditor(typeof(ThingyCostMod))]
    public class EffectRadiusEditor : Editor {
        public void OnSceneGUI() {
            ThingyCostMod t = (target as ThingyCostMod);

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
}
#endif
