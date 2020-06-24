using UnityEngine;
using System.Collections;
using K_PathFinder.Settings;
using UnityEditor;
using K_PathFinder.PFDebuger;
using K_PathFinder;
using K_PathFinder.Graphs;

//debuger and settings
namespace K_PathFinder {
    public class PathFinderMenu : EditorWindow {
        bool sellectorMove = false;

        #region properties
        SerializedObject settingsObject;

        #region upper tab
        SerializedProperty targetProperties;
        static string targetPropertiesString = "targetProperties";
        static GUIContent targetPropertiesContent = new GUIContent("Properties", "Build navmesh using this properties");

        SerializedProperty drawBuilder;
        static string drawBuilderString = "drawBuilder";
        static GUIContent drawBuilderContent = new GUIContent("General", "");

        static string buildAreaString = "Build Area Sellector";
        static GUIContent buildAreaSellectorToggleCheckboxTooltip = new GUIContent(string.Empty, "Enable or disable drawing of sellector in scene");

        static GUIContent startXContent = new GUIContent("X:", "Start X position of sellector in chunks");
        static GUIContent startZContent = new GUIContent("Z:", "Start Z position of sellector in chunks");
        static GUIContent sizeXContent = new GUIContent("X:", "Size along X axis of sellector in chunks");
        static GUIContent sizeZContent = new GUIContent("Z:", "Size along Z axis of sellector in chunks");

        static GUIContent startLablelContent = new GUIContent("Start", "Start of sellector");
        static GUIContent sizeLableContent = new GUIContent("Size", "Size of sellcetor");


        string forgotToAddPropertiesWarning = "Put some Properties into Properties object tab so PathFinder know what Properties it should use to build NavMesh";

        static GUIContent leftBoxContent = new GUIContent("NavMesh Building");
        static GUIContent rightBoxContent = new GUIContent("NavMesh Saving");

        static GUIContent buildContent = new GUIContent("Build", "Build navmesh in sellected area");
        static GUIContent removeContent = new GUIContent("Remove", "Remove navmesh from sellected area. Only target area will be removed");
        static GUIContent removeAndRebuildContent = new GUIContent("Remove & Rebuild", "Remove navmesh from sellected area and rebuild after. Only target area will be removed");
        static GUIContent rebuildToggleContent = new GUIContent("", "Queue removed again? If true then we refresh sellected chunks");
        static GUIContent clearContent = new GUIContent("Clear", "Remove all NavMesh. Also stop all work");

        static GUIContent saveContent = new GUIContent("Save", "Save all current navmesh into SceneNavmeshData. If it not exist then suggest to create one and pass reference to it into scene helper.");
        static GUIContent loadContent = new GUIContent("Load", "Load current SceneNavmeshData from scene helper");
        static GUIContent deleteContent = new GUIContent("Delete", "Remove all serialized data from current NavMesh data. Scriptable object remain in project");
        #endregion
        
        #region settings tab

        #endregion

        SerializedProperty helperName;
        static string helperNameString = "helperName";
        static GUIContent helperNameContent = new GUIContent("Helper name", "pathfinder need object in scene in order to use unity API. you can specify it's name here");
        
        SerializedProperty useMultithread;
        static string useMultithreadString = "useMultithread";
        static GUIContent useMultithreadContent = new GUIContent("Multithread", "you can on/off multithreading for debug purpose. cause debuging threads is pain");

        SerializedProperty maxThreads;
        static string maxThreadsString = "maxThreads";
        static GUIContent maxThreadsContent = new GUIContent("Max Threads", "limit how much threads are used");

        SerializedProperty terrainCollectionType;
        static string terrainCollectionTypeString = "terrainCollectionType";
        static GUIContent terrainCollectionTypeContent = new GUIContent("Terrain Collector", "UnityWay - Collect data from terrain using Terrain.SampleHeight and TerrainData.GetSteepness. It's fast but it's all in main thread.\nCPU - Collect data by some fancy math using CPU. Not that fast but fully threaded.\nComputeShader - Superfast but in big chunks can be slow cause moving data from GPU is not that fast.");

        SerializedProperty colliderCollectionType;
        static string colliderCollectionTypeString = "colliderCollectionType";
        static GUIContent colliderCollectionTypeContent = new GUIContent("Collider Collector", "CPU - Collect data using CPU rasterization. It's threaded so no FPS drops here. \nComputeShader - Collect data by ComputeShader. Superfast but in big chunks can be slow cause moving data from GPU is not that fast.");

        SerializedProperty gridSize;
        static string gridSizeString = "gridSize";
        static GUIContent gridSizeContent = new GUIContent("World grid size", "Chunk size in world space. Good values are 10, 15, 20 etc.");

        SerializedProperty gridHighest;
        static string gridHighestString = "gridHighest";
        static GUIContent gridHighestContent = new GUIContent("Max", "For autocreating chunks. World space value is grid size * this value.");

        SerializedProperty gridLowest;
        static string gridLowestString = "gridLowest";
        static GUIContent gridLowestContent = new GUIContent("Chunk Height Min", "For autocreating chunks. World space value is grid size * this value.");
        #endregion
        
        const float LABEL_WIDTH = 105f;
        Vector2 scroll;

        //settings
        PathFinderSettings settings;
        private float desiredLabelWidth;

        [SerializeField]
        bool _showSettings = true;

        //debuger
        Vector3 start, end;
        bool sellectStart, sellectEnd;
        //Vector3 pointer = Vector3.zero;

        [SerializeField]
        bool _showDebuger = true;
        //[SerializeField]
        //bool _redoRemovedGraphs = true;

        GUILayoutOption[] guiLayoutForNiceLine = new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) };
        
        Vector3 s_Center;
        Vector3 s_p_Right;

        //SettingsDrawer settingsDrawer;

        //pool debug
        string objectPoolFormat1 = "{0}";
        string objectPoolFormat2 = "{0}<{1}>";
        string objectPoolFormat3 = ",";

        string objectPoolFormat6 = "(struct) {0} Marshal.SizeOf({1})";
        string objectPoolFormat7 = "(class) {0}";

        //string objectPoolFormat4 = "(struct) Marshal.SizeOf({0})";
        //string objectPoolFormat5 = "(class)";

        float poolTypeSpacing = 200f;
        float poolGenericNumberSpacing = 55f;
        float poolArrayNumberSpacing = 55f;
        GUIContent objectPoolContent1 = new GUIContent("Type");
        GUIContent objectPoolContent2 = new GUIContent("Pool", "Pool. Current objects acessable in pool");
        GUIContent objectPoolContent3 = new GUIContent("Delta", "Current taken/returned count. Take: +1, Return: -1");
        GUIContent objectPoolContent4 = new GUIContent("Created", "Total objects created");
        GUIContent objectPoolContent5 = new GUIContent("Size", "Array Size");
        GUIContent objectPoolContent6 = new GUIContent("Bytes", "Array Size * Marshal.SizeOf(this array type)");

 
        string lengthZeroIntegrityLog = "Navmesh is alright";
        GUIContent navmeshIntegrityLabel = new GUIContent("Navmesh Integrity", "if unfolded then additional tests will be performed to troubleshoot navmesh");

        GUIContent pathfinderThreadLog = new GUIContent("PathFinder thread log", "show state of pathfinder main thread. if something realy wrong with it and everything freezed up then it is best place to fugure out why");
        GUIContent pathfinderThreadLogReach = new GUIContent("<color=green>Reach</color>", "This part of code was successfuly finished");
        GUIContent pathfinderThreadLogNotReach = new GUIContent("<color=red>Not Reach</color>", "Maybe nothing happens just yet. If nothing happens after you call PathFinder.Update something went realy wrong and PathFinder main thread probably freezed out at this point. It will be handy if you tell developer how to reproduse this");
        string pathfinderThreadLogWorkCountTooltip = "Amount of work done";
        string timeFormat000 = "{0}:000{1}";
        string timeFormat00 = "{0}:00{1}";
        string timeFormat0= "{0}:0{1}";
        string timeFormat = "{0}:{1}";

        //layer editor stuff
        string buildInFromat = "Builtin Layer {0}";
        string userFormat = "User Layer {0}";

        [MenuItem(PathFinderSettings.UNITY_TOP_MENU_FOLDER + "/Menu", false, 0)]
        public static void OpenWindow() {
            GetWindow<PathFinderMenu>("PathFinderMenu").Show();
        }

        void OnEnable() {
            SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
            SceneView.onSceneGUIDelegate += this.OnSceneGUI;
            Repaint();
            this.autoRepaintOnSceneChange = true;
            //float gs = settings.gridSize;
            //s_Center = new Vector3((settings.startX + (settings.sizeX * 0.5f)) * gs, settings.pointerY, (settings.startX + (settings.sizeX * 0.5f)) * gs);
            PathFinder.PathFinderInit();
        }

        void OnDestroy() {
            EditorUtility.SetDirty(settings);
            SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
            Debuger_K.SetSettingsDirty();
        }
        
        void OnGUI() {
            scroll = GUILayout.BeginScrollView(scroll);
            float curLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LABEL_WIDTH;    

            if (settingsObject == null | settings == null) {
                if (settings == null)
                    settings = PathFinderSettings.LoadSettings();

                settingsObject = new SerializedObject(settings);
                targetProperties = settingsObject.FindProperty(targetPropertiesString);
                drawBuilder = settingsObject.FindProperty(drawBuilderString);

                helperName = settingsObject.FindProperty(helperNameString);
                useMultithread = settingsObject.FindProperty(useMultithreadString);
                maxThreads = settingsObject.FindProperty(maxThreadsString);

                terrainCollectionType = settingsObject.FindProperty(terrainCollectionTypeString);
                colliderCollectionType = settingsObject.FindProperty(colliderCollectionTypeString);

                gridSize = settingsObject.FindProperty(gridSizeString);
                gridHighest = settingsObject.FindProperty(gridHighestString);
                gridLowest = settingsObject.FindProperty(gridLowestString);

                CheckPointer();
            }
            
            settingsObject.Update();

            try {         
                ImportantButtons();
            }
            catch (System.Exception e) {
                GUILayout.Label(string.Format("Exception has ocured in importand buttons.\n\nException:\n{0}", e));
            }

            UITools.LineHorizontal();

            _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings");
            if (_showSettings) {
                try {           
                    ShowSettings();
                }
                catch (System.Exception e) {
                    GUILayout.Label(string.Format("Exception has ocured while showing settings.\n\nException:\n{0}", e));      
                }
            }

            UITools.LineHorizontal();
            

            _showDebuger = EditorGUILayout.Foldout(_showDebuger, "Debuger");
            if (_showDebuger) {
                try {
                    ShowDebuger();
                }
                catch (System.Exception e) {
                    GUILayout.Label(string.Format("Exception has ocured while showing debuger.\n\nException:\n{0}", e));
                }
            }

            UITools.LineHorizontal();

            EditorGUIUtility.labelWidth = curLabelWidth;

            GUILayout.EndScrollView();

            if (GUI.changed) {
                EditorUtility.SetDirty(settings);
                Debuger_K.SetSettingsDirty();
                Repaint();
            }

            settingsObject.ApplyModifiedProperties();
        }
        
        void OnSceneGUI(SceneView sceneView) {
            Event curEvent = Event.current;
            Color col = Handles.color;
            Handles.color = Color.red;

            if (settings == null)
                settings = PathFinderSettings.LoadSettings();

            if (settings.drawAreaPointer) {
                AreaPointer targetData = settings.areaPointer * settings.gridSize;
                bool isMoved;
                targetData = MyHandles.DrawData(targetData, 2.5f, settings.gridSize, out isMoved);

                if (PathFinderSettings.isAreaPointerMoving) {
                    settings.areaPointer = targetData / settings.gridSize;
                    EditorUtility.SetDirty(settings);
                    SceneView.RepaintAll();
                }

                if (curEvent.type == EventType.Used) {
                    PathFinderSettings.isAreaPointerMoving = isMoved;
                }

                Handles.color = col;

                if (sellectorMove) {
                    XZPosInt pointerPos;
                    RaycastHit hit;
                    Ray ray = HandleUtility.GUIPointToWorldRay(curEvent.mousePosition);
                    if (Physics.Raycast(ray, out hit)) {
                        pointerPos = PathFinder.ToChunkPosition(hit.point);
                    }
                    else {
                        Vector3 intersection;
                        if (Math3d.LinePlaneIntersection(out intersection, ray.origin, ray.direction, Vector3.up, new Vector3())) {
                            pointerPos = PathFinder.ToChunkPosition(intersection);
                        }
                        else {
                            pointerPos = new XZPosInt();
                        }
                    }

                    MovePointer(new AreaPointer(pointerPos.x, pointerPos.z, pointerPos.x + 1, pointerPos.z + 1, hit.point.y));

                    if (curEvent.type == EventType.MouseDown && curEvent.button == 0) {
                        sellectorMove = false;
                    }

                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            Debuger_K.DrawDebugLabels();
            Debuger_K.DrawDeltaCost();

            Handles.BeginGUI();
            Debuger_K.DrawSceneGUI();
            Handles.EndGUI();
        }

        private void ImportantButtons() {
            EditorGUI.BeginChangeCheck();
            bool someBool = EditorGUILayout.Foldout(drawBuilder.boolValue, drawBuilderContent);

            if (EditorGUI.EndChangeCheck()) 
                drawBuilder.boolValue = someBool;            

            if (drawBuilder.boolValue == false)
                return;

            float rightOffset = 30;
            float singleLineHeight = EditorGUIUtility.singleLineHeight;

            EditorGUILayout.PropertyField(targetProperties, targetPropertiesContent);
            
            #region sellector
            bool drawPointer = settings.drawAreaPointer;
            Rect buildAreaRect = GUILayoutUtility.GetRect(Screen.width - rightOffset, drawPointer ? singleLineHeight * 3 : singleLineHeight + 2);
            Rect buildAreaCheckboxRect = new Rect(buildAreaRect.x, buildAreaRect.y, buildAreaRect.width, singleLineHeight);

            GUI.Box(buildAreaRect, buildAreaString);
            EditorGUI.BeginChangeCheck();
            someBool = GUI.Toggle(buildAreaCheckboxRect, settings.drawAreaPointer, buildAreaSellectorToggleCheckboxTooltip);
            if (EditorGUI.EndChangeCheck()) {
                settings.drawAreaPointer = someBool;
                Repaint();
            }

            if (drawPointer) {    
                Rect baLeftRect = new Rect(buildAreaRect.x, buildAreaRect.y + singleLineHeight, buildAreaRect.width * 0.5f, buildAreaRect.height - singleLineHeight);
                Rect baRightRect = new Rect(buildAreaRect.x + (buildAreaRect.width * 0.5f), buildAreaRect.y + singleLineHeight, buildAreaRect.width * 0.5f, buildAreaRect.height - singleLineHeight);

                AreaPointer areaPointer = settings.areaPointer;

                float tLabelSize = 40;
                float tRemainSize = Mathf.Max(baRightRect.width - tLabelSize, 0);
                int pStartX, pStartZ, pSizeX, pSizeZ;
                Rect rectStartLablel = new Rect(baRightRect.x, baRightRect.y, tLabelSize, singleLineHeight);
                Rect rectStartX = new Rect(baRightRect.x + tLabelSize, baRightRect.y, tRemainSize * 0.5f, singleLineHeight);
                Rect rectStartZ = new Rect(baRightRect.x + tLabelSize + (tRemainSize * 0.5f), baRightRect.y, tRemainSize * 0.5f, singleLineHeight);

                Rect rectSizeLablel = new Rect(baRightRect.x, baRightRect.y + singleLineHeight, tLabelSize, singleLineHeight);
                Rect rectSizeX = new Rect(baRightRect.x + tLabelSize, baRightRect.y + singleLineHeight, tRemainSize * 0.5f, singleLineHeight);
                Rect rectSizeZ = new Rect(baRightRect.x + tLabelSize + (tRemainSize * 0.5f), baRightRect.y + singleLineHeight, tRemainSize * 0.5f, singleLineHeight);

                EditorGUIUtility.labelWidth = tLabelSize;
                EditorGUI.LabelField(rectStartLablel, startLablelContent);
                EditorGUI.LabelField(rectSizeLablel, sizeLableContent);

                EditorGUIUtility.labelWidth = 15;
                EditorGUI.BeginChangeCheck();
                pStartX = EditorGUI.IntField(rectStartX, startXContent, areaPointer.roundStartX);
                pStartZ = EditorGUI.IntField(rectStartZ, startZContent, areaPointer.roundStartZ);
                pSizeX = EditorGUI.IntField(rectSizeX, sizeXContent, areaPointer.roundSizeX);
                if (pSizeX < 1)
                    pSizeX = 1;
                pSizeZ = EditorGUI.IntField(rectSizeZ, sizeZContent, areaPointer.roundSizeZ);
                if (pSizeZ < 1)
                    pSizeZ = 1;

                if (EditorGUI.EndChangeCheck())
                    settings.areaPointer = new AreaPointer(pStartX, pStartZ, pStartX + pSizeX, pStartZ + pSizeZ, settings.areaPointer.y);

                EditorGUIUtility.labelWidth = LABEL_WIDTH;

                if (sellectorMove)
                    GUI.Box(new Rect(baLeftRect.x, baLeftRect.y, baLeftRect.width, singleLineHeight), "Move");
                else {
                    if (GUI.Button(new Rect(baLeftRect.x, baLeftRect.y, baLeftRect.width, singleLineHeight), "Move")) {
                        sellectorMove = true;
                    }
                }

                if (GUI.Button(new Rect(baLeftRect.x, baLeftRect.y + singleLineHeight, baLeftRect.width, singleLineHeight), "Reset")) {
                    ResetAreaPointer();
                }
            }

            #endregion

            GUILayout.Space(5);

            //control buttons
            #region control buttons
            Rect things = GUILayoutUtility.GetRect(Screen.width - rightOffset, singleLineHeight * 4);

            Rect left = new Rect(things.x, things.y, things.width * 0.5f, things.height);
            Rect right = new Rect(things.x + (things.width * 0.5f), things.y, things.width * 0.5f, things.height);
            
            GUI.Box(left, leftBoxContent);
            GUI.Box(right, rightBoxContent);

            #region navmesh building
            Rect rectBuild = new Rect(left.x, left.y + singleLineHeight, left.width, singleLineHeight);

            if (GUI.Button(rectBuild, buildContent)) {
                if (settings.targetProperties != null) 
                    PathFinder.QueueGraph(
                        settings.areaPointer.roundStartX, 
                        settings.areaPointer.roundStartZ, 
                        settings.targetProperties,
                        settings.areaPointer.roundSizeX, 
                        settings.areaPointer.roundSizeZ);          
                else
                    Debug.LogWarning(forgotToAddPropertiesWarning);
            }

            GUIContent targetRemoveContent = settings.removeAndRebuild ? removeAndRebuildContent : removeContent;
            Rect rectRemove = new Rect(left.x, left.y + (singleLineHeight * 2), left.width - singleLineHeight, singleLineHeight);
            Rect rectRemoveToggle = new Rect(left.x + (left.width - singleLineHeight), left.y + (singleLineHeight * 2), singleLineHeight, singleLineHeight);

            if (GUI.Button(rectRemove, targetRemoveContent)) {
                if (settings.targetProperties != null)
                    PathFinder.RemoveGraph(
                        settings.areaPointer.roundStartX,
                        settings.areaPointer.roundStartZ,
                        settings.targetProperties,
                        settings.areaPointer.roundSizeX,
                        settings.areaPointer.roundSizeZ, 
                        settings.removeAndRebuild);
                else
                    Debug.LogWarning(forgotToAddPropertiesWarning);           
            }

            EditorGUI.BeginChangeCheck();
            someBool = GUI.Toggle(rectRemoveToggle, settings.removeAndRebuild, rebuildToggleContent);
            if (EditorGUI.EndChangeCheck()) settings.removeAndRebuild = someBool;

            Rect rectClear = new Rect(left.x, left.y + (singleLineHeight * 3), left.width, singleLineHeight);

            if (GUI.Button(rectClear, clearContent)) {
                PathFinder.ChangeTargetState(PathFinder.MainThreadState.ClearNavmeshAndGeneration);
            }
            #endregion

            if (GUI.Button(new Rect(right.x, right.y + singleLineHeight, right.width, singleLineHeight), saveContent))
                PathFinder.SaveCurrentSceneData();
            if (GUI.Button(new Rect(right.x, right.y + (singleLineHeight * 2), right.width, singleLineHeight), loadContent))
                PathFinder.LoadCurrentSceneData();
            if (GUI.Button(new Rect(right.x, right.y + (singleLineHeight * 3), right.width, singleLineHeight), deleteContent))
                PathFinder.ClearCurrenSceneData();
            #endregion
            
        }

        private void ShowSettings() {
            if(settings == null)
                settings = PathFinderSettings.LoadSettings();

            EditorGUILayout.PropertyField(helperName, helperNameContent);

            if (useMultithread.boolValue) {
                GUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(useMultithread, useMultithreadContent);
                EditorGUIUtility.labelWidth = 80;
                EditorGUILayout.PropertyField(maxThreads, maxThreadsContent);
                EditorGUIUtility.labelWidth = LABEL_WIDTH;
                GUILayout.EndHorizontal();
            }
            else {
                EditorGUILayout.PropertyField(useMultithread, useMultithreadContent);
            }
            
            EditorGUILayout.PropertyField(terrainCollectionType, terrainCollectionTypeContent);
            EditorGUILayout.PropertyField(colliderCollectionType, colliderCollectionTypeContent);

            float someFloat;
            EditorGUI.BeginChangeCheck();
            someFloat = EditorGUILayout.FloatField(gridSizeContent, gridSize.floatValue);
            if (EditorGUI.EndChangeCheck()) {
                settings.gridSize = someFloat;
                PathFinder.gridSize = someFloat;
            }

            GUILayout.BeginHorizontal();    
            EditorGUILayout.PropertyField(gridLowest, gridLowestContent);
            EditorGUIUtility.labelWidth = 30;
            EditorGUILayout.PropertyField(gridHighest, gridHighestContent);          
            EditorGUIUtility.labelWidth = LABEL_WIDTH;
            GUILayout.EndHorizontal();

            if (gridHighest.intValue < gridLowest.intValue)
                gridHighest.intValue = gridLowest.intValue;

            if (gridLowest.intValue > gridHighest.intValue)
                gridLowest.intValue = gridHighest.intValue;

            UITools.LineHorizontal();
            DrawAreaEditor();
            UITools.LineHorizontal();
            DrawLayerEditor();
        }

        private string GetNiceTypeName(System.Type t) {
            var genericParameters = t.GetGenericArguments();

            if (genericParameters.Length == 0)
                return t.Name;

            string[] genericNames = new string[genericParameters.Length];
            for (int i = 0; i < genericParameters.Length; i++) {
                genericNames[i] = GetNiceTypeName(genericParameters[i]);
            }
            return string.Format(objectPoolFormat2, t.Name, string.Join(objectPoolFormat3, genericNames));
        }

        private void ShowDebuger() {
            Debuger_K.settings.showPoolDebug = EditorGUILayout.Foldout(Debuger_K.settings.showPoolDebug, new GUIContent("Pool Debug"));
            if (Debuger_K.settings.showPoolDebug) {
                Debuger_K.settings.showGenericPoolDebug = EditorGUILayout.Foldout(Debuger_K.settings.showGenericPoolDebug, new GUIContent("Generic"));
                if (Debuger_K.settings.showGenericPoolDebug) {
                    Debuger_K.ProcessPoolEvents();
                    GUILayout.BeginHorizontal();
                    GUILayout.Box(objectPoolContent1, GUILayout.MaxWidth(poolTypeSpacing));
                    GUILayout.Box(objectPoolContent2, GUILayout.MaxWidth(poolGenericNumberSpacing));
                    GUILayout.Box(objectPoolContent3, GUILayout.MaxWidth(poolGenericNumberSpacing));
                    GUILayout.Box(objectPoolContent4, GUILayout.MaxWidth(poolGenericNumberSpacing));
                    GUILayout.EndHorizontal();
                    foreach (var item in Debuger_K.objectPoolState) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Box(GetNiceTypeName(item.Key), GUILayout.MaxWidth(poolTypeSpacing));
                        GUILayout.Box(string.Format(objectPoolFormat1, item.Value.poolCount), GUILayout.MaxWidth(poolGenericNumberSpacing));
                        GUILayout.Box(string.Format(objectPoolFormat1, item.Value.poolDelta), GUILayout.MaxWidth(poolGenericNumberSpacing));
                        GUILayout.Box(string.Format(objectPoolFormat1, item.Value.created), GUILayout.MaxWidth(poolGenericNumberSpacing));
                        GUILayout.EndHorizontal();
                    }
                    UITools.LineHorizontal();
                }

                Debuger_K.settings.showArrayPoolDebug = EditorGUILayout.Foldout(Debuger_K.settings.showArrayPoolDebug, new GUIContent("Array"));
                if (Debuger_K.settings.showArrayPoolDebug) {
                    GUILayout.BeginHorizontal();
                    //GUILayout.Box(objectPoolContent1, GUILayout.MaxWidth(poolTypeSpacing));
                    GUILayout.Box(objectPoolContent5, GUILayout.MaxWidth(poolArrayNumberSpacing));
                    GUILayout.Box(objectPoolContent2, GUILayout.MaxWidth(poolArrayNumberSpacing));
                    GUILayout.Box(objectPoolContent3, GUILayout.MaxWidth(poolArrayNumberSpacing));
                    GUILayout.Box(objectPoolContent4, GUILayout.MaxWidth(poolArrayNumberSpacing));
                    GUILayout.Box(objectPoolContent6, GUILayout.MaxWidth(poolArrayNumberSpacing));
                    GUILayout.EndHorizontal();

                    foreach (var typePair in Debuger_K.arrayPoolState) {
                        bool isClass = typePair.Key.IsClass;

                        GUILayout.BeginHorizontal();
                        int mSize = 0;

                        if (isClass) {
                            GUILayout.Box(string.Format(objectPoolFormat7, typePair.Key.Name));
                        }
                        else {
                            mSize = System.Runtime.InteropServices.Marshal.SizeOf(typePair.Key);
                            GUILayout.Box(string.Format(objectPoolFormat6, typePair.Key.Name, mSize));
                        }
                      
                        GUILayout.EndHorizontal();
                        foreach (var value in typePair.Value) {
                            GUILayout.BeginHorizontal();
                            GUILayout.Box(string.Format(objectPoolFormat1, value.Key), GUILayout.MaxWidth(poolArrayNumberSpacing));
                            GUILayout.Box(string.Format(objectPoolFormat1, value.Value.poolCount), GUILayout.MaxWidth(poolArrayNumberSpacing));
                            GUILayout.Box(string.Format(objectPoolFormat1, value.Value.poolDelta), GUILayout.MaxWidth(poolArrayNumberSpacing));
                            GUILayout.Box(string.Format(objectPoolFormat1, value.Value.created), GUILayout.MaxWidth(poolArrayNumberSpacing));

                            if(isClass == false)
                                GUILayout.Box(string.Format(objectPoolFormat1, value.Key * mSize), GUILayout.MaxWidth(poolArrayNumberSpacing));

                            GUILayout.EndHorizontal();
                        }
                    }
                }                
            }
            UITools.LineHorizontal();

            Debuger_K.settings.showNavmeshIntegrity = EditorGUILayout.Foldout(Debuger_K.settings.showNavmeshIntegrity, navmeshIntegrityLabel);
            if (Debuger_K.settings.showNavmeshIntegrity) {
                lock (Debuger_K.navmeshIntegrityDebug) {
                    if (Debuger_K.navmeshIntegrityDebug.Length == 0) {
                        GUILayout.Label(lengthZeroIntegrityLog);
                    }
                    else {
                        GUILayout.Label(Debuger_K.navmeshIntegrityDebug.ToString());
                    }
                }
            }
            UITools.LineHorizontal();

            Debuger_K.settings.showNavmeshThreadState = EditorGUILayout.Foldout(Debuger_K.settings.showNavmeshThreadState, pathfinderThreadLog);
            if (Debuger_K.settings.showNavmeshThreadState) {
                //time : state : count : action

                PathFinder.PathFinderActionState[] mainThreadState = PathFinder.navmeshThreadState;
                GUILayout.BeginHorizontal();
                lock (mainThreadState) {
                    //time
                    GUILayout.BeginVertical();
                    for (int i = 0; i < mainThreadState.Length; i++) {
                        var time = mainThreadState[i].time;
                        int ms = time.Millisecond;
                        if(ms < 10)
                            GUILayout.Label(string.Format(timeFormat000, time.Minute, time.Millisecond));
                        else if (ms < 100)
                            GUILayout.Label(string.Format(timeFormat00, time.Minute, time.Millisecond));
                        else if (ms < 1000)
                            GUILayout.Label(string.Format(timeFormat0, time.Minute, time.Millisecond));
                        else
                            GUILayout.Label(string.Format(timeFormat, time.Minute, time.Millisecond));
                    }
                    GUILayout.EndVertical();

                    //state
                    GUILayout.BeginVertical();
                    bool curRuchTextState = GUI.skin.label.richText;
                    GUI.skin.label.richText = true;
                    for (int i = 0; i < mainThreadState.Length; i++) {
                        bool finished = mainThreadState[i].finished;
                        if (finished) 
                            GUILayout.Label(pathfinderThreadLogReach);
                        else 
                            GUILayout.Label(pathfinderThreadLogNotReach);                                      
                    }
                    GUI.skin.label.richText = curRuchTextState;
                    GUILayout.EndVertical();

                    //work count
                    GUILayout.BeginVertical();
                    for (int i = 0; i < mainThreadState.Length; i++) {             
                        GUILayout.Label(new GUIContent(mainThreadState[i].workCount.ToString(), pathfinderThreadLogWorkCountTooltip));
                    }
                    GUILayout.EndVertical();

                    //work count
                    GUILayout.BeginVertical();
                    for (int i = 0; i < mainThreadState.Length; i++) {
                        PathFinder.PathFinderActions val = (PathFinder.PathFinderActions)i;
                        GUILayout.Label(val.ToString());
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            UITools.LineHorizontal();

            Debuger_K.settings.doDebug = EditorGUILayout.Toggle(new GUIContent("Do debug", "enable debuging. debuged values you can enable down here. generic values will be debuged anyway"), Debuger_K.settings.doDebug);
            if (Debuger_K.settings.doDebug) {
                Debuger_K.settings.doDebugFull = EditorGUILayout.Toggle(new GUIContent("Full Debug", "if false will debug only resulted navmesh. prefer debuging only navmesh. and do not use unity profiler if you enable this option or else unity will die in horribly way. also do not enable it if area are too big. memory expensive stuff here!"), Debuger_K.settings.doDebugFull);
            }

            Debuger_K.settings.doProfiler = EditorGUILayout.Toggle(new GUIContent("Do profiler", "are we using some simple profiling? cause unity dont really profile threads. if true will write lots of stuff to console"), Debuger_K.settings.doProfiler);            
            //Debuger_K.settings.showSceneGUI = EditorGUILayout.Toggle(new GUIContent("Scene GUI", "Enable or disable checkboxes in scene to on/off debug of certain chunks and properties. To apply changes push Update button"), Debuger_K.settings.showSceneGUI);
            Debuger_K.settings.showChunkContentMap = EditorGUILayout.Toggle(new GUIContent("Content Map", "Shows some additional data that participate (or not) in navmesh generation"), Debuger_K.settings.showChunkContentMap);
            Debuger_K.settings.showDeltaCost = EditorGUILayout.Toggle(new GUIContent("Delta Cost", "Shows delta cost"), Debuger_K.settings.showDeltaCost);

            GUILayout.Box(string.Empty, guiLayoutForNiceLine);
            Debuger_K.settings.debugRVO = EditorGUILayout.Toggle(new GUIContent("Debug Velocity Obstacles"), Debuger_K.settings.debugRVO);
            if (Debuger_K.settings.debugRVO) {
                Debuger_K.settings.debugRVODKTree = EditorGUILayout.Toggle(new GUIContent("VO KD-Tree", "Shows agent tree where agent search each others"), Debuger_K.settings.debugRVODKTree);
                Debuger_K.settings.debugRVONeighbours = EditorGUILayout.Toggle(new GUIContent("VO Neighbours", "Shows agent current neighbours"), Debuger_K.settings.debugRVONeighbours);
                Debuger_K.settings.debugRVObasic = EditorGUILayout.Toggle(new GUIContent("VO Basic", "Draws basic information about agent state. It radius, current velocity, target velocity, etc"), Debuger_K.settings.debugRVObasic);
                Debuger_K.settings.debugRVOvelocityObstacles = EditorGUILayout.Toggle(new GUIContent("VO Vel Cones", "Draw velocity cones. Directions where agent forbidden to move"), Debuger_K.settings.debugRVOvelocityObstacles);
                Debuger_K.settings.debugRVOconvexShape = EditorGUILayout.Toggle(new GUIContent("VO Vel Shape", "Draw avaiable delta-velocity"), Debuger_K.settings.debugRVOconvexShape);
                Debuger_K.settings.debugRVOplaneIntersections = EditorGUILayout.Toggle(new GUIContent("VO Plane Intersections", "Debug process of solving zero-space delta-velocity"), Debuger_K.settings.debugRVOplaneIntersections);
                Debuger_K.settings.debugRVONavmeshClearance = EditorGUILayout.Toggle(new GUIContent("VO Navmesh Obstacles", "Debug nearest navmesh borders where Agent forbdden to move"), Debuger_K.settings.debugRVONavmeshClearance);
            }
            GUILayout.Box(string.Empty, guiLayoutForNiceLine);

            Debuger_K.GenericGUI();

            Debuger_K.settings.showSelector = EditorGUILayout.Foldout(Debuger_K.settings.showSelector, "Debug options");
            if (Debuger_K.settings.showSelector) {
                Debuger_K.SellectorGUI2();
                //Debuger_K.SellectorGUI();
            }
        }

        //drawing settings
        public void CheckPointer() {
            if (settings.areaPointer.sizeX < 1 | settings.areaPointer.sizeZ < 1) {
                ResetAreaPointer();
            }
        }
        public void ResetAreaPointer() {
            settings.areaPointer = new AreaPointer(0, 0, 1, 1, 0);
            EditorUtility.SetDirty(settings);
        }
        public void MovePointer(AreaPointer pointer) {
            settings.areaPointer = pointer;
            EditorUtility.SetDirty(settings);
        }
        public void DrawAreaEditor() {
            settings.drawAreaEditor = EditorGUILayout.Foldout(settings.drawAreaEditor, "Area Editor");

            if (!settings.drawAreaEditor)
                return;

            if (GUILayout.Button("Add Area")) {
                settings.AddArea();
            }

            int removeArea = -1;

            for (int i = 0; i < settings.areaLibrary.Length; i++) {
                Area area = settings.areaLibrary[i];

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(area.id.ToString(), GUILayout.MaxWidth(10f));
                if (area.id == 0 || area.id == 1)  //cant choose name to default areas to avoit confussion
                    EditorGUILayout.LabelField(new GUIContent(area.name, "area name"), GUILayout.MaxWidth(80));
                else
                    area.name = EditorGUILayout.TextField(area.name, GUILayout.MaxWidth(80));
                
#if UNITY_2018_1_OR_NEWER
                area.color = EditorGUILayout.ColorField(GUIContent.none, area.color, false, false, false, GUILayout.MaxWidth(12f));
#else
                area.color = EditorGUILayout.ColorField(GUIContent.none, area.color, GUILayout.MaxWidth(12f));
#endif

                EditorGUILayout.LabelField(new GUIContent("Cost", "move cost"), GUILayout.MaxWidth(30));

                if (area.id == 1)
                    EditorGUILayout.LabelField("max", GUILayout.MaxWidth(30f));
                else
                    area.cost = EditorGUILayout.FloatField(area.cost, GUILayout.MaxWidth(30f));


                EditorGUILayout.LabelField(new GUIContent("Priority", "z-fighting cure. if one layer on another than this number matter"), GUILayout.MaxWidth(45f));

                if (area.id == 1)
                    EditorGUILayout.LabelField(new GUIContent("-1", "clear area it always should be -1"), GUILayout.MaxWidth(30f));
                else {
                    area.overridePriority = EditorGUILayout.IntField(area.overridePriority, GUILayout.MaxWidth(30f));
                    if (area.overridePriority < 0)
                        area.overridePriority = 0;
                }

                if (area.id != 0 && area.id != 1 && GUILayout.Button("X", GUILayout.MaxWidth(18f))) {
                    removeArea = i;               
                }

                EditorGUILayout.EndHorizontal();
            }


            if (removeArea != -1) {
                settings.RemoveArea(removeArea);
            }

            UITools.LineHorizontal();
            settings.drawUnityAssociations = EditorGUILayout.Foldout(settings.drawUnityAssociations, "Tag Associations");

            if (settings.drawUnityAssociations) {
                settings.checkRootTag = EditorGUILayout.ToggleLeft("Use Root Tag", settings.checkRootTag);

                settings.CheckTagAssociations();

                TextAnchor curAnchor = GUI.skin.box.alignment;
                GUI.skin.box.alignment = TextAnchor.MiddleLeft;
                Color curColor = GUI.color;
                foreach (var pair in PathFinderSettings.tagAssociations) {
                    EditorGUILayout.BeginHorizontal();

                    GUILayout.Box(pair.Key, GUILayout.ExpandWidth(true), GUILayout.MaxWidth(EditorGUIUtility.labelWidth));
                    GUI.color = settings.areaLibrary[pair.Value.id].color;
                    GUILayout.Box(string.Empty, GUILayout.MaxWidth(15));
                    GUI.color = curColor;

                    EditorGUI.BeginChangeCheck();
                    int tempValue = EditorGUILayout.IntPopup(
                        pair.Value.id, PathFinder.settings.areaNames, PathFinder.settings.areaIDs);
                    if (EditorGUI.EndChangeCheck()) {
                        PathFinderSettings.tagAssociations[pair.Key] = settings.areaLibrary[tempValue];
                    }
                    EditorGUILayout.EndHorizontal();
                }
                GUI.color = curColor;
                GUI.skin.box.alignment = curAnchor;
            }
        }

        public void DrawLayerEditor() {
            settings.drawLayersEditor = EditorGUILayout.Foldout(settings.drawLayersEditor, "Layer Editor");

            if (!settings.drawLayersEditor)
                return;

            EditorGUI.BeginChangeCheck();
            GUI.enabled = false;
            for (int i = 0; i < settings.layers.Length; i++) {
                if(i < 8)
                    settings.layers[i] = EditorGUILayout.TextField(string.Format(buildInFromat, i), settings.layers[i]);
                else
                    settings.layers[i] = EditorGUILayout.TextField(string.Format(userFormat, i), settings.layers[i]);

                if (i == 7)
                    GUI.enabled = true;
            }
            GUI.enabled = true;
            if (EditorGUI.EndChangeCheck())
                settings.UpdateLayerNames();            
        }

    }
}
