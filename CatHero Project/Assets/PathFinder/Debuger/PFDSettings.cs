#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

using System.Collections.Generic;
using System;
using K_PathFinder.Settings;

namespace K_PathFinder.PFDebuger {
    public class PFDSettings : ScriptableObject {
        //[SerializeField]
        //public string sceneName = "_pathFinderHelper";
        [SerializeField]
        public bool autoUpdateSceneView = true;

        ////options of chunks to debug
        //[SerializeField]public List<Color> optionColors;
        //[SerializeField]public List<bool> optionIsShows;

        //general
        [SerializeField] public bool doDebug = true;
        [SerializeField] public bool doDebugFull = false;
        [SerializeField] public bool doProfiler = true;
        [SerializeField] public bool showChunkContentMap = false;
        [SerializeField] public bool showDeltaCost = true;
        
        [SerializeField] public bool showSelector = true;
        [SerializeField] public bool showPoolDebug = false;
        [SerializeField] public bool showGenericPoolDebug = false;
        [SerializeField] public bool showArrayPoolDebug = false;
        [SerializeField] public bool showNavmeshIntegrity = false;
        [SerializeField] public bool showNavmeshThreadState = false;

        //flags to debug
        [SerializeField] public bool drawGenericDots = true;
        [SerializeField] public bool drawGenericLines = true;
        [SerializeField] public bool drawGenericLabels = true;
        [SerializeField] public bool drawGenericMesh = true;
        [SerializeField] public bool drawErrors = true;

        //delta cost
        [SerializeField] public bool drawDeltaCost = false;
        [SerializeField] public bool drawDeltaCostProperties = false;
        [SerializeField] public bool drawDeltaCostGroup = false;

        [SerializeField]public bool[] debugFlags;

        //RVO flags
        [SerializeField] public bool debugRVO = false;
        [SerializeField] public bool debugRVODKTree = true;
        [SerializeField] public bool debugRVONeighbours = true;
        [SerializeField] public bool debugRVObasic = true;
        [SerializeField] public bool debugRVOvelocityObstacles = true;
        [SerializeField] public bool debugRVOconvexShape = true;
        [SerializeField] public bool debugRVOplaneIntersections = true;
        [SerializeField] public bool debugRVONavmeshClearance = true;

        public static PFDSettings LoadSettings() {
            AssetDatabase.Refresh();
            string settingsPath = string.Format("{0}/{1}/{2}.asset", new string[] {
                    PathFinderSettings.FindProjectPath(),
                    PathFinderSettings.EDITOR_FOLDER,
                    PathFinderSettings.DEBUGER_ASSET_NAME });

            PFDSettings result = AssetDatabase.LoadAssetAtPath<PFDSettings>(settingsPath);

            if (result == null) {
                Debug.LogWarning(string.Format("Could not load debuger settings at {0} path. Creating new one", settingsPath));
                result = CreateInstance<PFDSettings>();
                AssetDatabase.CreateAsset(result, settingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return result;
        }
        
        [CustomEditor(typeof(PFDSettings))]
        public class PFDSettingsEditor : Editor {
            public override void OnInspectorGUI() {
                EditorGUILayout.LabelField("you probably should not edit this file in inspector", GUILayout.ExpandHeight(true));
            }
        }
    }



    [Serializable]
    public class PFD3Option {
        public bool showMe;
        public Color color;

        public PFD3Option() {
            this.showMe = false;
            this.color = Color.white;
        }
    }
}
#endif