using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditorInternal; //for checking tag associations. you probably wount change tags list in runtime (if ever can) so that check are excluded from builded projects
using UnityEditor;
#endif

namespace K_PathFinder.Settings {
    //none of these values should be changed outside pathfinder
    [Serializable]
    public class PathFinderSettings : ScriptableObject {
        public const bool DO_NON_CONVEX_COLLIDERS = true;

        public const string PROJECT_FOLDER = "PathFinder";
        //public const string ASSETS_FOLDER = "Assets";
        public const string EDITOR_FOLDER = "Editor";
        //public const string MANUAL_FOLDER = "Manual";
        public const string SHADERS_FOLDER = "Shaders";
        public const string UNITY_TOP_MENU_FOLDER = "Window/K-PathFinder";
        public const string RESOURSES_FOLDER = "Resources";
        public const string PROPERTIES_FOLDER = "Properties";
        public const string DEBUGER_FOLDER = "Debuger";
        public const string SETTINGS_ASSET_NAME = "PathfinderSettings";
        public const string DEBUGER_ASSET_NAME = "DebugerSettings";
        public const string MANUAL_ASSET_NAME = "ManualSettings";

        public const string NOT_WALKABLE = "Not Walkable";
        public const string DEFAULT = "Default";

        public const float TERRAIN_FAST_MIN_SIZE = 0.1f;

        [SerializeField]public string helperName = "_pathFinderHelper";
        [SerializeField]public bool useMultithread = true;
        [SerializeField]public int maxThreads = 8;

        [SerializeField]public float gridSize = 10f;
        [SerializeField]public int gridLowest = -100;
        [SerializeField]public int gridHighest = 100;
        [SerializeField]public TerrainCollectorType terrainCollectionType = TerrainCollectorType.CPU;
        [SerializeField]public ColliderCollectorType colliderCollectionType = ColliderCollectorType.CPU;

        [SerializeField]public bool drawAreaEditor;
        [SerializeField]public Area[] areaLibrary;
        [SerializeField]private string lastLaunchedVersion;

        //properties to build
        [SerializeField]public AgentProperties targetProperties;

        public GUIContent[] areaNames;
        public int[] areaIDs;

        [SerializeField] public bool drawUnityAssociations = false;
        [SerializeField] public bool checkRootTag = false;

        [Serializable]
        struct TagAssociations {
            [SerializeField] public string tag;
            [SerializeField] public int area;
        }

        [SerializeField] List<TagAssociations> tagAssociationsSerialized = new List<TagAssociations>();
        public static Dictionary<string, Area> tagAssociations = new Dictionary<string, Area>();

        [SerializeField] public bool drawLayersEditor;
        [SerializeField] public string[] layers;
        private const string layerStringDefault = "Default";
        private const string layerStringIgnore = "Ignore";

        public string[] layerSellectorStrings;
        public int[] layerSellectorInts;

        //UI serrings
        #region PathFinderMenu UI settings
#if UNITY_EDITOR
        //area to build
        [SerializeField] public AreaPointer areaPointer;
        [SerializeField] public bool drawAreaPointer = true;
        [SerializeField] public bool removeAndRebuild = true;
        [SerializeField] public bool drawBuilder = true;

        public static bool isAreaPointerMoving = false;
#endif
        #endregion

        void OnEnable() {
            switch (lastLaunchedVersion) {
                default:
                    //kinda need to know last launched version but if i not use this value anywhere then unity will annoy with it
                    break;
            }
            lastLaunchedVersion = PathFinder.VERSION;
            ResetAreaPublicData();
            DeserializeTags();
            DeserializeLayers();
        }

#if UNITY_EDITOR
        private void OnDestroy() {
            SerializeTags();
        }

        private void OnDisable() {
            SerializeTags();
        }
#endif

        public static PathFinderSettings LoadSettings() {
            AssetDatabase.Refresh();
            PathFinderSettings loadedSettings = Resources.Load<PathFinderSettings>(SETTINGS_ASSET_NAME);

#if UNITY_EDITOR
            if (loadedSettings == null) {
                string settingsPath = String.Format("{0}/{1}/{2}.asset", new string[] {
                    FindProjectPath(),
                    RESOURSES_FOLDER,
                    SETTINGS_ASSET_NAME });

                Debug.LogWarningFormat("Cannot find {0}.asset in Resources folder. Attempts to search it in {1}", SETTINGS_ASSET_NAME, settingsPath);
                loadedSettings = AssetDatabase.LoadAssetAtPath<PathFinderSettings>(settingsPath);

                //wut? how is this happened? maybe some languages have different folder separator? or what?
                if (loadedSettings == null) {
                    Debug.LogWarningFormat("Cannot find {0}.asset even in that folder. Creating new one", SETTINGS_ASSET_NAME);
                    loadedSettings = CreateInstance<PathFinderSettings>();
                    AssetDatabase.CreateAsset(loadedSettings, settingsPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            if (loadedSettings.areaLibrary == null) {
                Debug.LogWarning("Pathfinder area library does not have areas for some reason. reseting areas list");
                Area[] resetedAreaLibrary = new Area[4];
                resetedAreaLibrary[0] = new Area(DEFAULT, 0, Color.green);
                resetedAreaLibrary[1] = new Area(NOT_WALKABLE, 1, Color.red) { cost = float.MaxValue };
                //if for some reason settings is null it reverted to default initial state so at least examples work fine
                resetedAreaLibrary[2] = new Area("Yellow", 2, Color.yellow) { cost = 20, overridePriority = 2 };
                resetedAreaLibrary[3] = new Area("Muddy", 3, new Color(0.5921569f, 0.5058824f, 0.4509804f)) { cost = 2, overridePriority = 3 };
                loadedSettings.areaLibrary = resetedAreaLibrary;    
                EditorUtility.SetDirty(loadedSettings);
            }
#endif

            loadedSettings.areaLibrary[1].overridePriority = -1;

            //result.areaLibrary.Clear();
            //result.areaLibrary.Add(new Area(DEFAULT, 0, Color.green));
            //result.areaLibrary.Add(new Area(NOT_WALKABLE, 1, Color.red) { cost = float.MaxValue });
            //result.areaLibrary.Add(new Area("Yellow", 2, Color.yellow) { cost = 20, overridePriority = 2 });
            //result.areaLibrary.Add(new Area("Muddy", 3, new Color(0.5921569f, 0.5058824f, 0.4509804f)) { cost = 2, overridePriority = 3 });

            ////backard version compatability
            //if (result.areaLibrary[0].name == DEFAULT) {
            //    if (result.areaLibrary[1].name != NOT_WALKABLE) {
            //        Debug.LogWarning("PathFinder: somehow default areas are not in order. resetings Areas");
            //        result.areaLibrary.Clear();
            //        result.areaLibrary.Add(new Area(NOT_WALKABLE, 0, Color.red) { cost = float.MaxValue });
            //        result.areaLibrary.Add(new Area(DEFAULT, 1, Color.green));
            //        result.areaLibrary.Add(new Area("Yellow", 2, Color.yellow) { cost = 20, overridePriority = 2 });
            //        result.areaLibrary.Add(new Area("Muddy", 3, new Color(0.5921569f, 0.5058824f, 0.4509804f)) { cost = 2, overridePriority = 3 });
            //    }
            //    else {
            //        SwapAreas(1, 0);
            //    }
            //}

            return loadedSettings;
        }

        //public void SwapAreas(int i1, int i2) {
        //    Area a1 = areaLibrary[i1];
        //    Area a2 = areaLibrary[i2];

        //    areaLibrary[i1] = a2;
        //    areaLibrary[i2] = a1;

        //    areaLibrary[i1].id = i1;
        //    areaLibrary[i2].id = i2;

        //    areaNames[i1].text = a2.name;
        //    areaNames[i2].text = a1.name;

        //    //apply to terrains
        //    var ter = GameObject.FindObjectsOfType<TerrainNavmeshSettings>();
        //    foreach (var item in ter) {
        //        var d = item.data;

        //        for (int i = 0; i < d.Length; i++) {
        //            if (d[i] == i1)
        //                d[i] = i2;
        //            else if (d[i] == i2)
        //                d[i] = i1;
        //        }
        //    }

        //    //area game object
        //    var ar = GameObject.FindObjectsOfType<AreaGameObject>();
        //    foreach (var item in ar) {
        //        if (item.areaInt == i1)
        //            item.areaInt = i2;
        //        else if (item.areaInt == i2)
        //            item.areaInt = i1;
        //    }

        //    //world mod
        //    var wm = GameObject.FindObjectsOfType<AreaWorldMod>();
        //    foreach (var item in wm) {
        //        if (item.areaInt == i1)
        //            item.areaInt = i2;
        //        else if (item.areaInt == i2)
        //            item.areaInt = i1;
        //    }
        //}


#if UNITY_EDITOR
        public static string FindProjectPath() {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue("Assets");

            string targetfolder = "/PathFinder";
            int targetFolderLength = targetfolder.Length;

            while (true) {
                if (queue.Count == 0)
                    break;

                string curFolder = queue.Dequeue();

                string[] subFolders = AssetDatabase.GetSubFolders(curFolder);
                foreach (var curSubFolder in subFolders) {    
                    if (curSubFolder.Length >= targetFolderLength) {
                        int offset = curSubFolder.Length - targetFolderLength;
                        bool isValid = true;
                        for (int i = 0; i < targetFolderLength; i++) {
                            if(curSubFolder[offset + i] != targetfolder[i]) {
                                isValid = false;
                                break;
                            }
                        }

                        if (isValid) 
                            return curSubFolder;                        
                    }

                    queue.Enqueue(curSubFolder);
                }
            }

            Debug.LogError("Could not find PathFinder folder. Did you rename it?");
            return null;
        }
#endif

        void ResetAreaPublicData() {
            areaNames = new GUIContent[areaLibrary.Length];
            areaIDs = new int[areaLibrary.Length];

            for (int i = 0; i < areaLibrary.Length; i++) {
                areaNames[i] = new GUIContent(areaLibrary[i].name);
                areaIDs[i] = areaLibrary[i].id;
            }
        }

        #region area manage



        public void AddArea() {
            Area newArea = new Area("Area " + areaLibrary.Length, areaLibrary.Length);
            Array.Resize(ref areaLibrary, areaLibrary.Length + 1);
            areaLibrary[newArea.id] = newArea;     
            PathFinder.AddAreaHash(newArea);
            ResetAreaPublicData();
        }

        public void RemoveArea(int id) {
            if (id == 0 | id == 1 | areaLibrary.Length - 1 < id)
                return;

            Area removedArea = areaLibrary[id];

            List<Area> tempList = new List<Area>();
            tempList.AddRange(areaLibrary);
            tempList.Remove(removedArea);
            areaLibrary = tempList.ToArray();
            
            for (int i = id; i < areaLibrary.Length; i++) {
                areaLibrary[i].id = i;
            }

            PathFinder.RemoveAreaHash(removedArea);
            ResetAreaPublicData();
        }

#if UNITY_EDITOR
        private void SerializeTags() {
            tagAssociationsSerialized.Clear();
            foreach (var pair in tagAssociations) {
                if (pair.Value.id != -1)
                    tagAssociationsSerialized.Add(new TagAssociations() { tag = pair.Key, area = pair.Value.id });
            }
            //foreach (var item in tagAssociationsSerialized) {
            //    Debug.LogFormat("Serialized {0} : {1}", item.tag, item.area);
            //}

            EditorUtility.SetDirty(this);
        }
#endif

        private void DeserializeTags() {
            //foreach (var item in tagAssociationsSerialized) {
            //    Debug.LogFormat("Deserialize {0} : {1}", item.tag, item.area);
            //}

            foreach (var item in tagAssociationsSerialized) {
                tagAssociations[item.tag] = areaLibrary[item.area];
            }
#if UNITY_EDITOR
            CheckTagAssociations();
#endif
        }



        //layers
        private void DeserializeLayers() {
            if (layers == null || layers.Length != 32 || layers[0] != layerStringDefault || layers[1] != layerStringIgnore) {
                layers = new string[32];
                layers[0] = layerStringDefault;
                layers[1] = layerStringIgnore;
                for (int i = 2; i < 32; i++) {
                    layers[i] = ""; //i cant use string.empty here cause it is reference type
                }
            }
            
            UpdateLayerNames();
        }

        public void UpdateLayerNames() {
            List<string> strings = new List<string>();
            List<int> ints = new List<int>();

            strings.Add(layers[0]);
            ints.Add(0);
            strings.Add(layers[1]);
            ints.Add(1);

            for (int i = 8; i < 32; i++) {
                if(string.IsNullOrEmpty(layers[i]) == false) {
                    strings.Add(layers[i]);
                    ints.Add(i);
                }
            }

            layerSellectorStrings = strings.ToArray();
            layerSellectorInts = ints.ToArray();
        }

#if UNITY_EDITOR
        public int DrawAreaSellector(int currentValue) {
            if (currentValue >= areaLibrary.Length)
                currentValue = 0;

            GUILayout.BeginHorizontal();
            GUILayout.Label(currentValue.ToString() + ":", GUILayout.MaxWidth(15));

            Color curColor = GUI.color;
            GUI.color = areaLibrary[currentValue].color;
            GUILayout.Box("", GUILayout.MaxWidth(15));
            GUI.color = curColor;
            currentValue = EditorGUILayout.IntPopup(currentValue, areaNames, areaIDs);
            GUILayout.EndHorizontal();
            return currentValue;
        }
        
        public void CheckTagAssociations() {
            foreach (var tag in InternalEditorUtility.tags) {
                if (tagAssociations.ContainsKey(tag) == false)
                    tagAssociations.Add(tag, areaLibrary[0]);
            }

            List<string> strings = Pool.GenericPool<List<string>>.Take();

            foreach (var key in tagAssociations.Keys) {
                if (InternalEditorUtility.tags.Contains(key) == false)
                    strings.Add(key);
            }

            foreach (var item in strings) {
                tagAssociations.Remove(item);
            }

            Pool.GenericPool<List<string>>.ReturnToPool(ref strings);
        }

        [CustomEditor(typeof(PathFinderSettings))]
        public class PathFinderSettingsEditor : Editor {
            public override void OnInspectorGUI() {
                EditorGUILayout.LabelField("you probably should not edit this file in inspector");
                //PathFinderSettings s = (PathFinderSettings)target;
                //if (s == null)
                //    return;

                //EditorGUILayout.LabelField("some links to important files:");
                //s.ComputeShaderRasterization2D = (ComputeShader)EditorGUILayout.ObjectField("CS Rasterization 2D", s.ComputeShaderRasterization2D, typeof(ComputeShader), false);
                //s.ComputeShaderRasterization3D = (ComputeShader)EditorGUILayout.ObjectField("CS Rasterization 3D", s.ComputeShaderRasterization3D, typeof(ComputeShader), false);        
            }
        
        }
#endif
        #endregion
    }
}
