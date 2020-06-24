#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using System;
using System.Linq;
using K_PathFinder.Graphs;
using K_PathFinder.CoverNamespace;
using System.Text;
using UnityEditor;
using K_PathFinder.GraphGeneration;
using K_PathFinder.CoolTools;
using K_PathFinder.Pool;

//huge pile of garbage code
//i dont advice reading it

namespace K_PathFinder.PFDebuger {
    public enum DebugGroup {
        line = 0,
        dot = 1,
        label = 2,
        mesh = 3
    }

    public enum DebugOptions : int {
        Cell = 0,
        CellArea = 1,
        CellEdges = 2,
        CellConnection = 3,
        CellEdgesOrder = 4,
        Cover = 5,
        Samples = 6,
        JumpBase = 7,
        Voxels = 8,
        VoxelPos = 9,
        VoxelConnection = 10,
        VoxelLayer = 11,
        VoxelRawMax = 12,
        VoxelRawMin = 13,
        VoxelRawVolume = 14,
        ChunkBounds = 15,
        ColliderBounds = 16,
        NodesAndConnections = 17,
        NodesAndConnectionsPreRDP = 18,
        WalkablePolygons = 19,
        Triangulator = 20,
        CellMap = 21
    }

    public static class Debuger_K {
        const float NODE_SIZE = 0.01f;

        public static PFDSettings settings;
        private static Vector2 debugScrollPos;
        private static bool _init = false;

        private static bool
            needGenericDotUpdate, 
            needGenericLineUpdate,
            needGenericTrisUpdate;

        //gui stuff and debug arrays
        private const int FLAGS_AMOUNT = 22;
        private static GUIContent[] labels;
        private static GUIContent dividerBoxLabel = new GUIContent();
        private static GUILayoutOption[] dividerThing = new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) };
        private static string genericDebugToolTip = "Some options for control stuff added by Debuger.AddSomthing";

        private static Dictionary<GeneralXZData, ChunkDebugInfo> debugData = new Dictionary<GeneralXZData, ChunkDebugInfo>();

        //lock
        private static object lockObj = new object();

        //generic
        private static List<HandleThing> labelsDebug = new List<HandleThing>();
        private static List<PointData> genericDots = new List<PointData>();
        private static List<LineData> genericLines = new List<LineData>();
        private static List<TriangleData> genericTris = new List<TriangleData>();
        
        private static int cellCounter;
        private static int coversCounter;
        private static int jumpBasesCounter;
        private static int voxelsCounter;

        private static bool _stop = false;
        public static StringBuilder navmeshIntegrityDebug = new StringBuilder();
        

        public static DebugSet debugSetLocalAvoidance = new DebugSet();
        public static DebugSet debugSetChunkContentMap = new DebugSet();
        public static DebugSet debugSetDeltaCost = new DebugSet();

        public static List<PointData> tmpDots = new List<PointData>();
        public static List<LineData> tmpLines = new List<LineData>();
        public static List<TriangleData> tmpTris = new List<TriangleData>();
                
        public static void Init() {
            if (_init) return;
            _init = true;

            LoadSettings();        
        }

        public static void OnSceneUpdate() {
            //generic dots
            if (needGenericDotUpdate) {
                needGenericDotUpdate = false;
                if (settings.drawGenericDots) {
                    PathFinder.sceneInstance.UpdateGenericDots(genericDots);
                }
            }

            //generic lines
            if (needGenericLineUpdate) {
                needGenericLineUpdate = false;
                if (settings.drawGenericLines) {
                    PathFinder.sceneInstance.UpdateGenericLines(genericLines);
                }
            }

            //generic tris
            if (needGenericTrisUpdate) {
                needGenericTrisUpdate = false;
                if (settings.drawGenericMesh) {
                    PathFinder.sceneInstance.UpdateGenericTris(genericTris);
                }
            }

            bool anyDynamicUpdate = false;
            tmpDots.Clear();
            tmpLines.Clear();
            tmpTris.Clear();

            if (debugSetLocalAvoidance != null && debugSetLocalAvoidance.Collect(tmpDots, tmpLines, tmpTris))
                anyDynamicUpdate = true;


            if (settings != null) {
                if (settings.showChunkContentMap && debugSetChunkContentMap != null && debugSetChunkContentMap.Collect(tmpDots, tmpLines, tmpTris))
                    anyDynamicUpdate = true;

                if (settings.showDeltaCost && debugSetDeltaCost != null && debugSetDeltaCost.Collect(tmpDots, tmpLines, tmpTris))
                    anyDynamicUpdate = true;
            }



            if (anyDynamicUpdate)
                PathFinder.sceneInstance.UpdateDynamicData(tmpDots, tmpLines, tmpTris);
        }


        private static void LoadSettings() {
            if (settings == null)
                settings = PFDSettings.LoadSettings();
            
            if (settings.debugFlags == null || settings.debugFlags.Length != FLAGS_AMOUNT) {
                settings.debugFlags = new bool[FLAGS_AMOUNT];
                settings.debugFlags[(int)DebugOptions.Cell] = true;
                settings.debugFlags[(int)DebugOptions.CellArea] = true;
                settings.debugFlags[(int)DebugOptions.CellEdges] = true;
                settings.debugFlags[(int)DebugOptions.CellConnection] = true;
            }
        }


        public static void SetSettingsDirty() {
            if (settings == null)
                LoadSettings();
            EditorUtility.SetDirty(settings);
        }

        public static void ClearChunksDebug() {
            cellCounter = 0;
            coversCounter = 0;
            jumpBasesCounter = 0;
            voxelsCounter = 0;

            foreach (var info in debugData.Values) {
                info.Clear();
            }

            QueueUpdateSceneImportantThings();
        }

        public static void ClearAdditionalDebugSets() {
            debugSetChunkContentMap.Clear();
            debugSetLocalAvoidance.Clear();
        }

        public static void CheckNavmeshIntegrity() {
            if (settings == null)
                return;
            if (settings.showNavmeshIntegrity) {
                lock (navmeshIntegrityDebug)
                    PathFinder.CheckNavmeshIntegrity(navmeshIntegrityDebug);
            }
        }

        public static void QueueClearChunksDebug(GeneralXZData data) {
            PathFinder.AddMainThreadDelegate(() => { ClearChunksDebug(data); });
        }

        public static void ClearChunksDebug(GeneralXZData data) {
            bool changed = false;
            lock (lockObj) {
                ChunkDebugInfo info;
                if (debugData.TryGetValue(data, out info)) {
                    cellCounter -= info.cellCounter;
                    coversCounter -= info.coversCounter;
                    jumpBasesCounter -= info.jumpBasesCounter;
                    voxelsCounter -= info.voxelsCounter;
                    info.Clear();
                    changed = true;
                }
            }
            if (changed)
                QueueUpdateSceneImportantThings();
        }


        public static void DrawSceneGUI() {
            //if (PathFinder.activeCreationWork != 0 | PathFinder.haveActiveThreds | settings.showSceneGUI == false)
            //    return;

            //lock (debugData) {
            //    foreach (var chunkDictionary in debugData) {
            //        Vector3 pos = chunkDictionary.Key.centerV3;
            //        Vector3 screenPoint = Camera.current.WorldToViewportPoint(pos);
            //        if (screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1) {
            //            Vector3 screenPosition = Camera.current.WorldToScreenPoint(pos);

            //            GUILayout.BeginArea(new Rect(new Vector2(screenPosition.x, Screen.height - screenPosition.y), new Vector2(400, 400)));
            //            lock (chunkDictionary.Value) {
            //                foreach (var agentDictionary in chunkDictionary.Value) {
            //                    GUILayout.BeginHorizontal();
            //                    agentDictionary.Value.showMe = GUILayout.Toggle(agentDictionary.Value.showMe, "", GUILayout.MaxWidth(10));
            //                    GUILayout.Box(agentDictionary.Key.name);
            //                    GUILayout.EndHorizontal();
            //                }
            //            }
            //            GUILayout.EndArea();
            //        }
            //    }
            //}
        }

        public static void STOP() {
            _stop = true;
        }

        public static bool doDebug {
            get {return _init && settings.doDebug; }
        }
        public static bool debugOnlyNavMesh {
            get { return _init && settings.doDebugFull == false; }
        }
        public static bool useProfiler {
            get { return _init && settings.doProfiler; }
        }

        //rvo
        public static bool debugRVO {
            get { return _init && settings.debugRVO; }
        }
        public static bool debugRVObasic {
            get { return _init && settings.debugRVObasic; }
        }
        public static bool debugRVOKDTree {
            get { return _init && settings.debugRVODKTree; }
        }
        public static bool debugRVONeighbours {
            get { return _init && settings.debugRVONeighbours; }
        }
        public static bool debugRVOvelocityObstacles {
            get { return _init && settings.debugRVOvelocityObstacles; }
        }
        public static bool debugRVOconvexShape {
            get { return _init && settings.debugRVOconvexShape; }
        }
        public static bool debugRVOplaneIntersections {
            get { return _init && settings.debugRVOplaneIntersections; }
        }
        public static bool debugRVONavmeshClearance {
            get { return _init && settings.debugRVONavmeshClearance; }
        }

        public static void DrawDebugLabels() {
            lock (debugSetLocalAvoidance.labelsDebug) {
                foreach (var item in debugSetLocalAvoidance.labelsDebug) {
                    item.ShowHandle();
                }
            }

            if (settings != null) {
                if (settings.drawGenericLabels) {
                    lock (labelsDebug) {                
                        for (int i = 0; i < labelsDebug.Count; i++) {
                            labelsDebug[i].ShowHandle();
                        }
                    }
                }

                if (settings.showChunkContentMap) {
                    lock (debugSetChunkContentMap.labelsDebug) {
                        foreach (var item in debugSetChunkContentMap.labelsDebug) {
                            item.ShowHandle();
                        }
                    }
                }

                if (settings.showDeltaCost) {
                    lock (debugSetDeltaCost.labelsDebug) {
                        foreach (var item in debugSetDeltaCost.labelsDebug) {
                            item.ShowHandle();
                        }
                    }
                }
            }
        }

        struct ObjectPoolGenericDebugEvent {
            public Type type;
            public int poolDelta, poolCount, createdDelta;

            public ObjectPoolGenericDebugEvent(Type type, int poolDelta, int poolCount, int createdDelta) {
                this.type = type;
                this.poolDelta = poolDelta;
                this.poolCount = poolCount;
                this.createdDelta = createdDelta;
            }
        }

        struct ObjectPoolArrayDebugEvent {
            public Type type;
            public int arraySize;
            public int poolDelta, poolCount, createdDelta;

            public ObjectPoolArrayDebugEvent(Type type, int arraySize, int poolDelta, int poolCount, int createdDelta) {
                this.type = type;
                this.arraySize = arraySize;
                this.poolDelta = poolDelta;
                this.poolCount = poolCount;
                this.createdDelta = createdDelta;
            }
        }

        public struct PoolDebugStateValue {
            public int poolCount, poolDelta, created;
        }


        public static Dictionary<Type, PoolDebugStateValue> objectPoolState = new Dictionary<Type, PoolDebugStateValue>();
        public static Dictionary<Type, SortedDictionary<int, PoolDebugStateValue>> arrayPoolState = new Dictionary<Type, SortedDictionary<int, PoolDebugStateValue>>();

        static Queue<ObjectPoolGenericDebugEvent> objectPoolGenericDebugEvents = new Queue<ObjectPoolGenericDebugEvent>();
        static Queue<ObjectPoolArrayDebugEvent> objectPoolArrayDebugEvents = new Queue<ObjectPoolArrayDebugEvent>();

        public static void ProcessPoolEvents() {
            lock (objectPoolGenericDebugEvents) {
                while (objectPoolGenericDebugEvents.Count > 0) {
                    ObjectPoolGenericDebugEvent curEvent = objectPoolGenericDebugEvents.Dequeue();

                    PoolDebugStateValue val;
                    if (objectPoolState.TryGetValue(curEvent.type, out val)) {
                        val.poolCount = curEvent.poolCount;
                        val.poolDelta += curEvent.poolDelta;
                        val.created += curEvent.createdDelta;
                        objectPoolState[curEvent.type] = val;
                    }
                    else {
                        objectPoolState[curEvent.type] = new PoolDebugStateValue() {
                            poolCount = curEvent.poolCount,
                            poolDelta = curEvent.poolDelta,
                            created = curEvent.createdDelta
                        };
                    }
                }
            }

            lock (objectPoolArrayDebugEvents) {
                while (objectPoolArrayDebugEvents.Count > 0) {
                    ObjectPoolArrayDebugEvent curEvent = objectPoolArrayDebugEvents.Dequeue();
                    SortedDictionary<int, PoolDebugStateValue> typeDictionary;

                    if (arrayPoolState.TryGetValue(curEvent.type, out typeDictionary)) {
                        PoolDebugStateValue typeDictionaryValue;
                        if (typeDictionary.TryGetValue(curEvent.arraySize, out typeDictionaryValue)) {
                            typeDictionaryValue.poolCount = curEvent.poolCount;
                            typeDictionaryValue.poolDelta += curEvent.poolDelta;
                            typeDictionaryValue.created += curEvent.createdDelta;
                            typeDictionary[curEvent.arraySize] = typeDictionaryValue;
                        }
                        else {
                            typeDictionary.Add(
                              curEvent.arraySize,
                              new PoolDebugStateValue() {
                                  poolCount = curEvent.poolCount,
                                  poolDelta = curEvent.poolDelta,
                                  created = curEvent.createdDelta
                              });
                        }
                    }
                    else {
                        typeDictionary = new SortedDictionary<int, PoolDebugStateValue>();
                        typeDictionary.Add(
                            curEvent.arraySize,
                            new PoolDebugStateValue() {
                                poolCount = curEvent.poolCount,
                                poolDelta = curEvent.poolDelta,
                                created = curEvent.createdDelta
                            });
                        arrayPoolState[curEvent.type] = typeDictionary;
                    }
                }
            }
        }

        public static void ObjectPoolGenericRegisterEvent(Type type, int poolDelta, int poolCount, int createdDelta) {
            lock (objectPoolGenericDebugEvents) {
                objectPoolGenericDebugEvents.Enqueue(new ObjectPoolGenericDebugEvent(type, poolDelta, poolCount, createdDelta));
            }
        }
        public static void ObjectPoolArrayRegisterEvent(Type type, int arraySize, int poolDelta, int poolCount, int createdDelta) {
            lock (objectPoolArrayDebugEvents) {
                objectPoolArrayDebugEvents.Enqueue(new ObjectPoolArrayDebugEvent(type, arraySize, poolDelta, poolCount, createdDelta));
            }
        }


        public static void DrawDeltaCost() {
            if (settings.drawDeltaCost == false | PathFinder.cellDeltaCostArray == null)
                return;

            Debug.Log("reimplement debug if deltacost");

            //string format = "";

            //if (settings.drawDeltaCostProperties)
            //    format += "properties {0} ";

            //if (settings.drawDeltaCostGroup)
            //    format += "group: {1} ";

            //format += " {2}\n";

            //int cellIDused = PathFinderData.maxRegisteredCellID;
            //Cell[] cells = new Cell[cellIDused];
            //string[] strings = new string[cellIDused];
            //List<Graph> graphs = new List<Graph>();
            //PathFinder.GetAllGraphs(graphs);

            //foreach (var graph in graphs) {
            //    int cellCount;
            //    Cell[] cellArray;
            //    graph.GetCells(out cellArray, out cellCount);

            //    for (int i = 0; i < cellCount; i++) {
            //        Cell cell = cellArray[i];
            //        cells[cell.globalID] = cell;
            //    }
            //}



            //for (int group = 0; group < PathFinder.cellDeltaCostArray.Length; group++) {
            //    float[] array = PathFinder.cellDeltaCostArray;
            //    if (array != null) {
            //        for (int i = 0; i < cellIDused; i++) {
            //            if (cells[i] != null) {
            //                float value = array[i];
            //                if (value != 0f)
            //                    strings[i] += string.Format(format, cells[i].graph.properties.ToString(), group, value);
            //            }
            //        }
            //    }
            //}


            //for (int i = 0; i < cellIDused; i++) {
            //    if (cells[i] != null) {
            //        Handles.BeginGUI();
            //        Color color = GUI.color;
            //        GUI.color = Color.black;
            //        Handles.Label(cells[i].centerVector3, strings[i]);
            //        GUI.color = color;
            //        Handles.EndGUI();
            //    }
            //}
        }

        public static void SellectorGUI2() {
            if (labels == null || labels.Length != FLAGS_AMOUNT) {
                labels = new GUIContent[FLAGS_AMOUNT];
                labels[(int)DebugOptions.Cell] = new GUIContent("Cell", "Convex area inside navmesh that connected with other Cells");
                labels[(int)DebugOptions.CellArea] = new GUIContent("Cell Area", "Representation of Cell shape");
                labels[(int)DebugOptions.CellConnection] = new GUIContent("Cell Connection", "Representation of Cell connections to ther Cells");
                labels[(int)DebugOptions.CellEdges] = new GUIContent("Cell Edge", "Representation of Cell borders");
                labels[(int)DebugOptions.CellEdgesOrder] = new GUIContent("Cell Edges Order", "All edges in Cell chould be in clockwise pattern. this debug option for catching this type of bugs. only used in full debug");
                labels[(int)DebugOptions.Cover] = new GUIContent("Cover", "Representation of covers in Scene. Flat surfaces represet Height. lines with dot represent where it connected to existed navmesh");
                labels[(int)DebugOptions.Samples] = new GUIContent("Samples", "Representation of samples");
                labels[(int)DebugOptions.JumpBase] = new GUIContent("Jump Base", "Representation of spots siutable for jump checks");
                labels[(int)DebugOptions.Voxels] = new GUIContent("Voxels", "Mixels of data that transformed into NavMesh");
                labels[(int)DebugOptions.VoxelPos] = new GUIContent("Voxel Pos", "Upper position of Voxel");
                labels[(int)DebugOptions.VoxelConnection] = new GUIContent("Voxel Connection", "Connections of voxel grid to each other");
                labels[(int)DebugOptions.VoxelLayer] = new GUIContent("Voxel Layer", "Layer sepparations betwin voxels. All Voxels splited into 2d sheets");
                labels[(int)DebugOptions.VoxelRawMax] = new GUIContent("Voxel Raw Max", "Raw data of voxel maximum height");
                labels[(int)DebugOptions.VoxelRawMin] = new GUIContent("Voxel Raw Min", "Raw data of voxel minimum height");
                labels[(int)DebugOptions.VoxelRawVolume] = new GUIContent("Voxel Raw Volume", "Raw data that shot where minimal and maximal height are layed");
                labels[(int)DebugOptions.ChunkBounds] = new GUIContent("Chunk Bounds", "Chunk bounds. Any object outside chunk bounds are ignored");
                labels[(int)DebugOptions.ColliderBounds] = new GUIContent("Collider Bounds", "Collider bounds that participate into NavMesh generation");
                labels[(int)DebugOptions.NodesAndConnections] = new GUIContent("Nodes Info", "Basic nodes information");
                labels[(int)DebugOptions.NodesAndConnectionsPreRDP] = new GUIContent("Nodes Info Pre RDP", "Basic nodes information before they simplified");
                labels[(int)DebugOptions.WalkablePolygons] = new GUIContent("Walkable Polygons", "Graphical represenation of walkable polygons");
                labels[(int)DebugOptions.Triangulator] = new GUIContent("Triangulator", "Triangulator pipeline debug");
                labels[(int)DebugOptions.CellMap] = new GUIContent("Cell Map");
            }

            settings.autoUpdateSceneView = GUILayout.Toggle(settings.autoUpdateSceneView, "auto update scene view");

            if (GUILayout.Button("Update")) {
                UpdateSceneImportantThings();
            }
            GUILayout.Box(dividerBoxLabel, dividerThing);
            var flags = settings.debugFlags;

            GUILayout.Label(string.Format(
                "Cells: {0}\nVoxels :{1}\nCovers: {2}\nJump Bases: {3}", cellCounter, voxelsCounter, coversCounter, jumpBasesCounter
                ), GUILayout.ExpandWidth(false));
            GUILayout.Box(dividerBoxLabel, dividerThing);
            //cells
            flags[(int)DebugOptions.Cell] = EditorGUILayout.Toggle(labels[(int)DebugOptions.Cell], flags[(int)DebugOptions.Cell]);
            if (flags[(int)DebugOptions.Cell]) {
                flags[(int)DebugOptions.CellArea] = EditorGUILayout.Toggle(labels[(int)DebugOptions.CellArea], flags[(int)DebugOptions.CellArea]);
                flags[(int)DebugOptions.CellConnection] = EditorGUILayout.Toggle(labels[(int)DebugOptions.CellConnection], flags[(int)DebugOptions.CellConnection]);
                flags[(int)DebugOptions.CellEdges] = EditorGUILayout.Toggle(labels[(int)DebugOptions.CellEdges], flags[(int)DebugOptions.CellEdges]);
                flags[(int)DebugOptions.CellEdgesOrder] = EditorGUILayout.Toggle(labels[(int)DebugOptions.CellEdgesOrder], flags[(int)DebugOptions.CellEdgesOrder]);
            }

            GUILayout.Box(dividerBoxLabel, dividerThing);

            //voxels
            flags[(int)DebugOptions.Voxels] = EditorGUILayout.Toggle(labels[(int)DebugOptions.Voxels], flags[(int)DebugOptions.Voxels]);
            if (flags[(int)DebugOptions.Voxels]) {
                flags[(int)DebugOptions.VoxelPos] = EditorGUILayout.Toggle(labels[(int)DebugOptions.VoxelPos], flags[(int)DebugOptions.VoxelPos]);
                flags[(int)DebugOptions.VoxelConnection] = EditorGUILayout.Toggle(labels[(int)DebugOptions.VoxelConnection], flags[(int)DebugOptions.VoxelConnection]);
                flags[(int)DebugOptions.VoxelLayer] = EditorGUILayout.Toggle(labels[(int)DebugOptions.VoxelLayer], flags[(int)DebugOptions.VoxelLayer]);

                flags[(int)DebugOptions.VoxelRawMax] = EditorGUILayout.Toggle(labels[(int)DebugOptions.VoxelRawMax], flags[(int)DebugOptions.VoxelRawMax]);
                flags[(int)DebugOptions.VoxelRawMin] = EditorGUILayout.Toggle(labels[(int)DebugOptions.VoxelRawMin], flags[(int)DebugOptions.VoxelRawMin]);
                flags[(int)DebugOptions.VoxelRawVolume] = EditorGUILayout.Toggle(labels[(int)DebugOptions.VoxelRawVolume], flags[(int)DebugOptions.VoxelRawVolume]);
            }

            GUILayout.Box(dividerBoxLabel, dividerThing);
            flags[(int)DebugOptions.Cover] = EditorGUILayout.Toggle(labels[(int)DebugOptions.Cover], flags[(int)DebugOptions.Cover]);
            flags[(int)DebugOptions.Samples] = EditorGUILayout.Toggle(labels[(int)DebugOptions.Samples], flags[(int)DebugOptions.Samples]);
            flags[(int)DebugOptions.JumpBase] = EditorGUILayout.Toggle(labels[(int)DebugOptions.JumpBase], flags[(int)DebugOptions.JumpBase]);
            flags[(int)DebugOptions.ChunkBounds] = EditorGUILayout.Toggle(labels[(int)DebugOptions.ChunkBounds], flags[(int)DebugOptions.ChunkBounds]);
            flags[(int)DebugOptions.ColliderBounds] = EditorGUILayout.Toggle(labels[(int)DebugOptions.ColliderBounds], flags[(int)DebugOptions.ColliderBounds]);
            flags[(int)DebugOptions.NodesAndConnections] = EditorGUILayout.Toggle(labels[(int)DebugOptions.NodesAndConnections], flags[(int)DebugOptions.NodesAndConnections]);
            flags[(int)DebugOptions.NodesAndConnectionsPreRDP] = EditorGUILayout.Toggle(labels[(int)DebugOptions.NodesAndConnectionsPreRDP], flags[(int)DebugOptions.NodesAndConnectionsPreRDP]);
            flags[(int)DebugOptions.WalkablePolygons] = EditorGUILayout.Toggle(labels[(int)DebugOptions.WalkablePolygons], flags[(int)DebugOptions.WalkablePolygons]);
            flags[(int)DebugOptions.Triangulator] = EditorGUILayout.Toggle(labels[(int)DebugOptions.Triangulator], flags[(int)DebugOptions.Triangulator]);
            flags[(int)DebugOptions.CellMap] = EditorGUILayout.Toggle(labels[(int)DebugOptions.CellMap], flags[(int)DebugOptions.CellMap]);
            GUILayout.Box(dividerBoxLabel, dividerThing);

            if (GUI.changed && settings.autoUpdateSceneView)
                UpdateSceneImportantThings();
        }

        public static void QueueUpdateSceneImportantThings() {
            PathFinder.AddMainThreadDelegate(UpdateSceneImportantThings);
        }

        public static void UpdateSceneImportantThings() {  
            List<PointData> newPointData = new List<PointData>();
            List<LineData> newLineData = new List<LineData>();
            List<TriangleData> newTrisData = new List<TriangleData>();

            if (settings.debugFlags.Length != FLAGS_AMOUNT)
                LoadSettings();

            var flags = settings.debugFlags;

            lock (lockObj) {
                foreach (var info in debugData.Values) {
                    if (!info.showMe)
                        continue;

                    if (flags[(int)DebugOptions.Cell]) {
                        if (flags[(int)DebugOptions.CellArea])
                            newTrisData.AddRange(info.cellsArea);
                        if (flags[(int)DebugOptions.CellEdges])
                            newLineData.AddRange(info.cellEdges);
                        if (flags[(int)DebugOptions.CellEdgesOrder])
                            newTrisData.AddRange(info.cellEdgesOrder);
                        if (flags[(int)DebugOptions.CellConnection])
                            newLineData.AddRange(info.cellConnections);
                    }

                    if (flags[(int)DebugOptions.Voxels]) {
                        if (flags[(int)DebugOptions.VoxelPos])
                            newPointData.AddRange(info.voxelPos);
                        if (flags[(int)DebugOptions.VoxelConnection])
                            newLineData.AddRange(info.voxelConnections);
                        if (flags[(int)DebugOptions.VoxelLayer])
                            newPointData.AddRange(info.voxelLayer);

                        if (flags[(int)DebugOptions.VoxelRawMax])
                            newPointData.AddRange(info.voxelRawMax);
                        if (flags[(int)DebugOptions.VoxelRawMin])
                            newPointData.AddRange(info.voxelRawMin);
                        if (flags[(int)DebugOptions.VoxelRawVolume])
                            newLineData.AddRange(info.voxelRawVolume);
                    }

                    if (flags[(int)DebugOptions.JumpBase]) {
                        newLineData.AddRange(info.jumpBasesLines);
                        newPointData.AddRange(info.jumpBasesDots);
                    }

                    if (flags[(int)DebugOptions.Cover]) {
                        newPointData.AddRange(info.coverDots);
                        newLineData.AddRange(info.coverLines);
                        newTrisData.AddRange(info.coverSheets);
                    }

                    if (flags[(int)DebugOptions.Samples]) {
                        newPointData.AddRange(info.cellContentDots);
                        newLineData.AddRange(info.cellContentLines);
                    }

                    if (flags[(int)DebugOptions.ChunkBounds])
                        newLineData.AddRange(info.chunkBounds);

                    if (flags[(int)DebugOptions.ColliderBounds])
                        newLineData.AddRange(info.colliderBounds);

                    if (flags[(int)DebugOptions.NodesAndConnections]) {
                        newLineData.AddRange(info.nodesLines);
                        newPointData.AddRange(info.nodesPoints);
                    }

                    if (flags[(int)DebugOptions.NodesAndConnectionsPreRDP]) {
                        newLineData.AddRange(info.nodesLinesPreRDP);
                        newPointData.AddRange(info.nodesPointsPreRDP);
                    }

                    if (flags[(int)DebugOptions.WalkablePolygons]) {
                        newLineData.AddRange(info.walkablePolygonLine);
                        newTrisData.AddRange(info.walkablePolygonSheet);
                    }

                    if (flags[(int)DebugOptions.Triangulator]) {
                        newLineData.AddRange(info.triangulator);
                    }

                    if (flags[(int)DebugOptions.CellMap]) {
                        newLineData.AddRange(info.cellMap);
                    }
                }
            }

            PathFinder.sceneInstance.UpdateStaticData(newPointData, newLineData, newTrisData);
        }

        public static void GenericGUI() {
            lock (lockObj) {
                bool tempBool;

                tempBool = settings.drawGenericLines;
                settings.drawGenericLines = EditorGUILayout.Toggle(new GUIContent("Lines " + genericLines.Count, genericDebugToolTip), settings.drawGenericLines);
                if (tempBool != settings.drawGenericLines)
                    needGenericLineUpdate = true;

                tempBool = settings.drawGenericDots;
                settings.drawGenericDots = EditorGUILayout.Toggle(new GUIContent("Dots " + genericDots.Count, genericDebugToolTip), settings.drawGenericDots);
                if (tempBool != settings.drawGenericDots)
                    needGenericDotUpdate = true;

                tempBool = settings.drawGenericMesh;
                settings.drawGenericMesh = EditorGUILayout.Toggle(new GUIContent("Meshes " + genericTris.Count, genericDebugToolTip), settings.drawGenericMesh);
                if (tempBool != settings.drawGenericMesh)
                    needGenericTrisUpdate = true;

                settings.drawDeltaCost = EditorGUILayout.Toggle(new GUIContent("Delta cost"), settings.drawDeltaCost);
                if (settings.drawDeltaCost) {
                    settings.drawDeltaCostProperties = EditorGUILayout.Toggle(new GUIContent("* Properties"), settings.drawDeltaCostProperties);
                    settings.drawDeltaCostGroup = EditorGUILayout.Toggle(new GUIContent("* Group"), settings.drawDeltaCostGroup);
                }

                //update on it's own
                settings.drawGenericLabels = EditorGUILayout.Toggle(new GUIContent("labels " + labelsDebug.Count, genericDebugToolTip), settings.drawGenericLabels);
            }
        }

        #region generic
        public static void AddLabel(Vector3 pos, string text, DebugGroup group = DebugGroup.label) {
            lock (labelsDebug) {
                labelsDebug.Add(new DebugLabel(pos, text));
            }
        }
        public static void AddLabel(Vector3 pos, double number, int digitsRound = 2, DebugGroup group = DebugGroup.label) {
            AddLabel(pos, Math.Round(number, digitsRound).ToString(), group);
        }
        public static void AddLabel(Vector3 pos, object obj, DebugGroup group = DebugGroup.label) {
            AddLabel(pos, obj.ToString(), group);
        }
        public static void AddLabelFormat(Vector3 pos, string format, params object[] data) {
            AddLabel(pos, string.Format(format, data));
        }

        //add things to lists
        private static void AddGenericDot(PointData data) {
            lock (genericDots)
                genericDots.Add(data);
            if (settings.drawGenericDots)
                needGenericDotUpdate = true;
        }
        private static void AddGenericDot(IEnumerable<PointData> datas) {
            lock (genericDots)
                genericDots.AddRange(datas);
            if (settings.drawGenericDots)
                needGenericDotUpdate = true;
        }
        private static void AddGenericLine(LineData data) {
            lock (genericLines)
                genericLines.Add(data);
            if (settings.drawGenericLines)
                needGenericLineUpdate = true;
        }
        private static void AddGenericLine(IEnumerable<LineData> datas) {
            lock (genericLines)
                genericLines.AddRange(datas);
            if (settings.drawGenericLines)
                needGenericLineUpdate = true;
        }
        private static void AddGenericLine(params LineData[] datas) {
            lock (genericLines)
                genericLines.AddRange(datas);
            if (settings.drawGenericLines)
                needGenericLineUpdate = true;
        }
        private static void AddGenericTriangle(TriangleData data) {
            lock (genericTris)
                genericTris.Add(data);
            if (settings.drawGenericMesh)
                needGenericTrisUpdate = true;

        }
        private static void AddGenericTriangle(IEnumerable<TriangleData> datas) {
            lock (genericTris)
                genericTris.AddRange(datas);
            if (settings.drawGenericMesh)
                needGenericTrisUpdate = true;
        }

        //dot
        public static void AddDot(Vector3 pos, Color color, float size = 0.02f) {
            AddGenericDot(new PointData(pos, color, size));
        }
        public static void AddDot(IEnumerable<Vector3> pos, Color color, float size = 0.02f) {
            List<PointData> pd = new List<PointData>();
            foreach (var item in pos) {
                pd.Add(new PointData(item, color, size));
            }
            AddGenericDot(pd);
        }
        public static void AddDot(Vector3 pos, float size = 0.02f) {
            AddGenericDot(new PointData(pos, Color.black, size));
        }

        public static void AddDot(float x, float y, float z, Color color, float size = 0.02f) {
            AddGenericDot(new PointData(new Vector3(x, y, z), color, size));
        }
        public static void AddDot(float x, float y, float z, float size = 0.02f) {
            AddGenericDot(new PointData(new Vector3(x, y, z), Color.black, size));
        }


        public static void AddDot(IEnumerable<Vector3> pos, float size = 0.02f) {
            List<PointData> pd = new List<PointData>();
            foreach (var item in pos) {
                pd.Add(new PointData(item, Color.black, size));
            }
            AddGenericDot(pd);
        }

        public static void AddDot(Color color, float size = 0.02f, params Vector3[] values) {
            AddDot(values, color, size);
        }
        public static void AddDot(float size = 0.02f, params Vector3[] values) {
            AddDot(values, Color.black, size);
        }
        public static void AddDot(Color color, params Vector3[] values) {
            AddDot(values, color, 0.02f);
        }
        public static void AddDot(params Vector3[] values) {
            AddDot(values, Color.black, 0.02f);
        }

        //line
        public static void AddLine(Vector3 v1, Vector3 v2, Color color, float addOnTop = 0f, float width = 0.001f) {
            AddGenericLine(new LineData(v1 + V3small(addOnTop), v2 + V3small(addOnTop), color, width));
        }

        public static void AddLine(float v1x, float v1y, float v1z, float v2x, float v2y, float v2z, float addOnTop = 0f, float width = 0.001f) {
            AddLine(new Vector3(v1x, v1y, v1z), new Vector3(v2x, v2y, v2z), Color.black, addOnTop, width);
        }

        public static void AddLine(Vector3 v1, Vector3 v2, float addOnTop = 0f, float width = 0.001f) {
            AddLine(v1, v2, Color.black, addOnTop, width);
        }
        public static void AddLine(CellContentData data, Color color, float addOnTop = 0f, float width = 0.001f) {
            AddLine(data.leftV3 + V3small(addOnTop), data.rightV3 + V3small(addOnTop), color, width);
        }
        public static void AddLine(CellContentData data, float addOnTop = 0f, float width = 0.001f) {
            AddLine(data.leftV3 + V3small(addOnTop), data.rightV3 + V3small(addOnTop), Color.black, width);
        }
        public static void AddLine(Vector3[] chain, Color color, bool chainClosed = false, float addOnTop = 0f, float width = 0.001f) {
            int length = chain.Length;
            if (length < 2)
                return;
            if (chainClosed) {
                LineData[] ld = new LineData[length];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                ld[length - 1] = new LineData(chain[length - 1] + V3small(addOnTop), chain[0] + V3small(addOnTop), color, width);
                AddGenericLine(ld);
            }
            else {
                LineData[] ld = new LineData[length - 1];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                AddGenericLine(ld);
            }
        }
        public static void AddLine(List<Vector3> chain, bool chainClosed = false, float addOnTop = 0f, float width = 0.001f) {
            AddLine(chain, Color.black, chainClosed, addOnTop, width);
        }



        public static void AddLine(List<Vector3> chain, Color color, bool chainClosed = false, float addOnTop = 0f, float width = 0.001f) {
            int length = chain.Count;
            if (length < 2)
                return;
            if (chainClosed) {
                LineData[] ld = new LineData[length];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                ld[length - 1] = new LineData(chain[length - 1] + V3small(addOnTop), chain[0] + V3small(addOnTop), color, width);
                AddGenericLine(ld);
            }
            else {
                LineData[] ld = new LineData[length - 1];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                AddGenericLine(ld);
            }
        }

        public static void AddLine(List<Vector2> chain, Color color, bool chainClosed = false, float width = 0.001f) {
            int length = chain.Count;
            if (length < 2)
                return;
            if (chainClosed) {
                LineData[] ld = new LineData[length];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i], chain[i + 1], color, width);
                }
                ld[length - 1] = new LineData(chain[length - 1], chain[0], color, width);
                AddGenericLine(ld);
            }
            else {
                LineData[] ld = new LineData[length - 1];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i], chain[i + 1], color, width);
                }
                AddGenericLine(ld);
            }
        }

        public static void AddLine(Vector3[] chain, bool chainClosed = false, float addOnTop = 0f, float width = 0.001f) {
            AddLine(chain, Color.black, chainClosed, addOnTop, width);
        }



        public static void AddLine(float addOnTop = 0f, float width = 0.001f, bool chainClosed = false, params Vector3[] chain) {
            AddLine(chain, Color.black, chainClosed, addOnTop, width);
        }
        public static void AddLine(Color color, float addOnTop = 0f, float width = 0.001f, bool chainClosed = false, params Vector3[] chain) {
            AddLine(chain, color, chainClosed, addOnTop, width);
        }
        //some fancy expensive stuff when no colors left
        public static void AddLine(Vector3 v1, Vector3 v2, Color color1, Color color2, int subdivisions, float addOnTop = 0f, float width = 0.001f) {
            List<LineData> ld = new List<LineData>();
            float step = 1f / subdivisions;
            bool flip = false;
            for (int i = 0; i < subdivisions; i++) {
                ld.Add(new LineData(Vector3.Lerp(v1, v2, Mathf.Clamp01(step * i)) + V3small(addOnTop), Vector3.Lerp(v1, v2, Mathf.Clamp01(step * (i + 1))) + V3small(addOnTop), flip ? color1 : color2, width));
                flip = !flip;
            }
            AddGenericLine(ld);
        }
        public static void AddLine(Vector3 v1, Vector3 v2, Color color1, Color color2, float subdivisionLength, float addOnTop = 0f, float width = 0.001f) {
            AddLine(v1, v2, color1, color2, Mathf.FloorToInt(Vector3.Distance(v1, v2) / subdivisionLength), addOnTop, width);
        }
        public static void AddLine(Vector3 v1, Vector3 v2, Color color1, Color color2, float addOnTop = 0f, float width = 0.001f) {
            Vector3 mid = SomeMath.MidPoint(v1, v2);
            AddLine(v1, mid, color1, addOnTop, width);
            AddLine(mid, v2, color2, addOnTop, width);
        }
        public static void AddCross(Vector3 v, Color color, float size, float lineWidth = 0.001f) {
            AddGenericLine(
                new LineData(new Vector3(v.x - size, v.y, v.z), new Vector3(v.x + size, v.y, v.z), color, lineWidth),
                new LineData(new Vector3(v.x, v.y - size, v.z), new Vector3(v.x, v.y + size, v.z), color, lineWidth),
                new LineData(new Vector3(v.x, v.y, v.z - size), new Vector3(v.x, v.y, v.z + size), color, lineWidth));
        }
        public static void AddRay(Vector3 point, Vector3 direction, Color color, float length = 1f, float width = 0.001f) {
            AddLine(point, point + (direction.normalized * length), color, width);
        }
        public static void AddRay(Vector3 point, Vector3 direction, float length = 1f, float width = 0.001f) {
            AddRay(point, direction, Color.black, width);
        }

        public static void AddBounds(Bounds b, Color color, float width = 0.001f) {
            AddGenericLine(BuildParallelepiped(b.center - b.extents, b.center + b.extents, color, width));
        }
        public static void AddBounds(Bounds b, float width = 0.001f) {
            AddBounds(b, Color.blue, width);
        }

        public static void AddBounds(Bounds2D b, Color color, float height = 0f, float width = 0.001f) {
            Vector3 A1 = new Vector3(b.minX, height, b.minY);
            Vector3 A2 = new Vector3(b.minX, height, b.maxY);
            Vector3 A3 = new Vector3(b.maxX, height, b.minY);
            Vector3 A4 = new Vector3(b.maxX, height, b.maxY);

            AddLine(A1, A2, color);
            AddLine(A1, A3, color);
            AddLine(A2, A4, color);
            AddLine(A3, A4, color);
        }
        public static void AddBounds(Bounds2D b, float height = 0f, float width = 0.001f) {
            AddBounds(b, Color.blue, height, width);
        }

        //geometry
        public static void AddTriangle(Vector3 A, Vector3 B, Vector3 C, Color color, bool outline = true, float outlineWidth = 0.001f) {
            AddGenericTriangle(new TriangleData(A, B, C, color));
            if (outline) {
                Color oColor = new Color(color.r, color.g, color.b, 1f);
                AddGenericLine(new LineData[]{
                    new LineData(A, B, oColor, outlineWidth),
                    new LineData(B, C, oColor, outlineWidth),
                    new LineData(C, A, oColor, outlineWidth)
                });
            }
        }
        public static void AddQuad(Vector3 bottomLeft, Vector3 upperLeft, Vector3 bottomRight, Vector3 upperRight, Color color, bool outline = true, float outlineWidth = 0.001f) {
            AddGenericTriangle(new TriangleData(bottomLeft, upperLeft, bottomRight, color));
            AddGenericTriangle(new TriangleData(upperLeft, bottomRight, upperRight, color));
            if (outline) {
                Color oColor = new Color(color.r, color.g, color.b, 1f);
                AddGenericLine(new LineData[]{
                    new LineData(bottomLeft, upperLeft, oColor, outlineWidth),
                    new LineData(upperLeft, upperRight, oColor, outlineWidth),
                    new LineData(upperRight, bottomRight, oColor, outlineWidth),
                    new LineData(bottomRight, bottomLeft, oColor, outlineWidth)
                });
            }
        }
        public static void AddMesh(Vector3[] verts, int[] tris, Color color, bool outline = true, float outlineWidth = 0.001f) {
            TriangleData[] td = new TriangleData[tris.Length / 3];
            for (int i = 0; i < tris.Length; i += 3) {
                td[i / 3] = new TriangleData(verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]], color);
            }
            AddGenericTriangle(td);

            if (outline) {
                Color oColor = new Color(color.r, color.g, color.b, 1f);
                LineData[] ld = new LineData[tris.Length];

                for (int i = 0; i < tris.Length; i += 3) {
                    ld[i] = new LineData(verts[tris[i]], verts[tris[i + 1]], oColor, outlineWidth);
                    ld[i + 1] = new LineData(verts[tris[i + 1]], verts[tris[i + 2]], oColor, outlineWidth);
                    ld[i + 2] = new LineData(verts[tris[i + 2]], verts[tris[i]], oColor, outlineWidth);
                }
                AddGenericLine(ld);
            }
        }

        public static void ClearGeneric(DebugGroup group) {
            if (_stop)
                return;
            switch (group) {
                case DebugGroup.line:
                    lock (genericLines) {
                        genericLines.Clear();
                    }
                    needGenericLineUpdate = true;
                    break;
                case DebugGroup.dot:
                    lock (genericDots) {
                        genericDots.Clear();
                    }
                    needGenericDotUpdate = true;
                    break;
                case DebugGroup.label: //labels are dont need update
                    lock (labelsDebug) {
                        labelsDebug.Clear();
                    }
                    break;
                case DebugGroup.mesh:
                    lock (genericTris) {
                        genericTris.Clear();
                    }
                    needGenericTrisUpdate = true;
                    break;
            }
        }

        public static void ClearGeneric() {   
            lock (genericLines) {
                if (genericLines.Count != 0) {
                    genericLines.Clear();
                    needGenericLineUpdate = true;
                }
            }
            lock (genericDots) {
                if (genericDots.Count != 0) {
                    genericDots.Clear();
                    needGenericDotUpdate = true;
                }
            }
            lock (genericTris) {
                if (genericTris.Count != 0) {
                    genericTris.Clear();
                    needGenericTrisUpdate = true;
                }
            }

            lock (labelsDebug) {
                if (labelsDebug.Count != 0)
                    labelsDebug.Clear();
            }
        }

        public static void ClearLabels() {
            ClearGeneric(DebugGroup.label);
        }
        public static void ClearLines() {
            ClearGeneric(DebugGroup.line);
        }
        public static void ClearDots() {
            ClearGeneric(DebugGroup.dot);
        }
        public static void ClearMeshes() {
            ClearGeneric(DebugGroup.mesh);
        }

        //error shortcuts //currently generic
        public static void AddErrorDot(Vector3 pos, Color color, float size = 0.1f) {
            AddDot(pos, color, size);
        }
        public static void AddErrorDot(Vector3 pos, float size = 0.1f) {
            AddErrorDot(pos, Color.red, 0.1f);
        }

        public static void AddErrorLine(Vector3 v1, Vector3 v2, Color color, float add = 0f) {
            AddLine(v1, v2, color, add);
        }
        public static void AddErrorLine(Vector3 v1, Vector3 v2, float add = 0f) {
            AddErrorLine(v1, v2, Color.red, add);
        }

        public static void AddErrorLabel(Vector3 pos, string text) {
            AddLabel(pos, text);
        }
        public static void AddErrorLabel(Vector3 pos, object text) {
            AddErrorLabel(pos, text.ToString());
        }
        #endregion

        #region add important
        //important
        private static ChunkDebugInfo GetInfo(GeneralXZData key) {
            lock (lockObj) {
                ChunkDebugInfo info;
                if (debugData.TryGetValue(key, out info) == false) {
                    info = new ChunkDebugInfo(key.x, key.z, key.properties);
                    debugData.Add(key, info);
                    //Bounds bounds = chunk.bounds;
                    //info.chunkBounds.AddRange(BuildParallelepiped(bounds.center - bounds.size, bounds.center + bounds.size, Color.gray, 0.001f));
                }
                return info;
            }
        }

        private static ChunkDebugInfo GetInfo(int x, int z, AgentProperties properties) {
            return GetInfo(new GeneralXZData(x, z, properties));
        }

        public static void UpdateCellMap(params Graph[] graphs) {
            UpdateCellMap(graphs as IEnumerable<Graph>);
        }

        public static void UpdateCellMap(IEnumerable<Graph> graphs) {
            foreach (var graph in graphs) {
                if (graph == null)
                    continue;

                ChunkDebugInfo chunkDebugInfo = GetInfo(graph.x, graph.z, graph.properties);

                List<LineData> list = chunkDebugInfo.cellMap;
                list.Clear();
                float graphRealX = graph.chunk.realX;
                float graphRealZ = graph.chunk.realZ;
                float graphSize = PathFinder.gridSize;

                int resolution;
                Cell[] cellRichMap;
                IndexLengthInt[] cellRiсhMapLayout;

                graph.GetCellMapToDebug(out resolution, out cellRichMap, out cellRiсhMapLayout);

                float step = graphSize / resolution;

                //draw lines
                for (int i = 0; i < resolution + 1; i++) {
                    list.Add(new LineData(new Vector3(graphRealX, 0, graphRealZ + (i * step)), new Vector3(graphRealX + graphSize, 0, graphRealZ + (i * step)), Color.blue, 0.001f));
                    list.Add(new LineData(new Vector3(graphRealX + (i * step), 0, graphRealZ), new Vector3(graphRealX + (i * step), 0, graphRealZ + graphSize), Color.blue, 0.001f));
                }

                for (int x = 0; x < resolution; x++) {
                    for (int z = 0; z < resolution; z++) {
                        float tX = graphRealX + (x * step) + (step * 0.5f);
                        float tZ = graphRealZ + (z * step) + (step * 0.5f);
                        int index = (z * resolution) + x;
                        IndexLengthInt IL = cellRiсhMapLayout[index];
                        for (int i = 0; i < IL.length; i++) {
                            list.Add(new LineData(new Vector3(tX, 0, tZ), cellRichMap[IL.index + i].centerVector3, Color.cyan, 0.001f));
                        }
                    }
                }
            }

            QueueUpdateSceneImportantThings();
        }

        public static void AddCells(Graph graph) {
            int x = graph.gridPosition.x;
            int z = graph.gridPosition.z;
            AgentProperties properties = graph.properties;

            Cell[] cellsArray;
            int cellsCount;
            graph.GetCells(out cellsArray, out cellsCount);

            Vector3 offsetLD = new Vector3(-0.015f, 0f, -0.015f);
            Vector3 offsetRT = new Vector3(0.015f, 0f, 0.015f);

            List<TriangleData> cellsAreaNewData = new List<TriangleData>();
            List<LineData> cellEdgesNewData = new List<LineData>();
            List<LineData> cellConnectionsNewData = new List<LineData>();
            List<PointData> cellValuesPoints = new List<PointData>();
            List<LineData> cellValuesLines = new List<LineData>();
            List<TriangleData> cellEdgesOrderNewData = new List<TriangleData>();

            bool debugEdgesSide = settings.doDebugFull;

            for (int cellID = 0; cellID < cellsCount; cellID++) {
                Cell cell = cellsArray[cellID];
                Color areaColor = cell.area.color;
                Vector3 center = cell.centerVector3;


                if (cell.passability == Passability.Crouchable)
                    areaColor *= 0.2f;

                areaColor = new Color(areaColor.r, areaColor.g, areaColor.b, 0.1f);
                var originalEdges = cell.originalEdges;
                int originalEdgesCount = cell.originalEdgesCount;

                for (int e = 0; e < originalEdgesCount; e++) {
                    var edge = originalEdges[e];
                    cellEdgesNewData.Add(new LineData(edge.a, edge.b, Color.black, 0.001f));
                    cellsAreaNewData.Add(new TriangleData(edge.a, edge.b, center, areaColor));

                    if (debugEdgesSide) {
                        Vector3 mid = edge.midV3;
                        cellEdgesOrderNewData.Add(new TriangleData(center, mid, edge.a, new Color(1f, 0f, 0f, 0.1f)));
                        cellEdgesOrderNewData.Add(new TriangleData(center, mid, edge.b, new Color(0f, 0f, 1f, 0.1f)));
                    }
                }
                
                lock (cell) {
                    int cellConnectionsCount = cell.connectionsCount;
                    CellConnection[] cellConnections = cell.connections;
                    CellContentData[] cellDatas = cell.connectionsDatas;

                    for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                        CellConnection connection = cellConnections[connectionIndex];
                        CellContentData data = cellDatas[connectionIndex];

                        if (connection.type == CellConnectionType.Generic) {
                            cellConnectionsNewData.Add(new LineData(center, connection.intersection, Color.white, 0.0008f));
                        }
                        else if (connection.type == CellConnectionType.jumpUp | connection.type == CellConnectionType.jumpDown) {
                            Color color;
                            Vector3 enter = data.leftV3;
                            Vector3 axis = connection.intersection;
                            Vector3 exit = data.rightV3;

                            if (connection.type == CellConnectionType.jumpUp) {
                                color = Color.yellow;
                                cellConnectionsNewData.Add(new LineData(enter + offsetLD, axis + offsetLD, color, 0.001f));
                                cellConnectionsNewData.Add(new LineData(axis + offsetLD, exit + offsetLD, color, 0.001f));
                            }
                            else {
                                color = Color.blue;
                                cellConnectionsNewData.Add(new LineData(enter + offsetRT, axis + offsetRT, color, 0.001f));
                                cellConnectionsNewData.Add(new LineData(axis + offsetRT, exit + offsetRT, color, 0.001f));
                            }
                        }
                    }

                    foreach (var content in cell.cellContentValues) {
                        if (content is CellSamplePoint) {
                            //AddLine(content.position, center, Color.blue);
                            cellValuesPoints.Add(new PointData(content.position, Color.blue, NODE_SIZE * 3));
                            cellValuesLines.Add(new LineData(center, content.position, new Color(0, 0, 1, 0.25f), 0.001f));
                        }
                    }
                }
            }

            ChunkDebugInfo info = GetInfo(x, z, properties);

            lock (lockObj) {
                cellCounter += cellsCount;
                info.cellCounter = cellsCount;
                info.cellsArea.Clear();
                info.cellsArea.AddRange(cellsAreaNewData);
                info.cellEdges.Clear();
                info.cellEdges.AddRange(cellEdgesNewData);
                info.cellEdgesOrder.Clear();
                info.cellEdgesOrder.AddRange(cellEdgesOrderNewData);
                info.cellConnections.Clear();
                info.cellConnections.AddRange(cellConnectionsNewData);
                info.cellContentDots.Clear();
                info.cellContentDots.AddRange(cellValuesPoints);
                info.cellContentLines.Clear();
                info.cellContentLines.AddRange(cellValuesLines);
            }

        }

        public static void AddEdgesInterconnected(int x, int z, AgentProperties properties, CellConnection connection) {
            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {                
                info.cellConnections.Add(new LineData(PathFinderData.cells[connection.from].centerVector3, connection.intersection, Color.white, 0.0008f));
            }
        }

        public static void AddVolumes(NavMeshTemplateCreation template, VolumeContainerNew volumeContainer) {
            float fragmentSize = 0.02f;

            bool doCover = template.doCover;
            int sizeX = volumeContainer.sizeX;
            int sizeZ = volumeContainer.sizeZ;

            //////////////
            List<PointData> voxelPosNewData = new List<PointData>();
            List<LineData> voxelConnectionsNewData = new List<LineData>();
            List<PointData> voxelLayerNewData = new List<PointData>();

            List<PointData> voxelRawMaxNewData = new List<PointData>();
            List<PointData> voxelRawMinNewData = new List<PointData>();
            List<LineData> voxelRawVolumeNewData = new List<LineData>();
            /////////////

            var collums = volumeContainer.collums;
            var data = volumeContainer.data;

            var hashData = template.hashData;

            //for raw data
            var rawShapeData = volumeContainer.shape;
            var rawData = rawShapeData.arrayData;

            for (int x = 0; x < sizeX; x++) {
                for (int z = 0; z < sizeZ; z++) {
                    int index = volumeContainer.GetIndex(x, z);
                    var currentColum = collums[index];

                    for (int collumIndex = 0; collumIndex < currentColum.length; collumIndex++) {
                        var value = data[currentColum.index + collumIndex];

                        Vector3 pos = volumeContainer.GetPos(x, z, value.y);
                        Color color;

                        switch ((Passability)value.pass) {
                            case Passability.Unwalkable:
                                color = Color.red;
                                break;
                            case Passability.Slope:
                                color = Color.magenta;
                                break;
                            case Passability.Crouchable:
                                color = SetAlpha(hashData.areaByIndex[value.area].color * 0.2f, 1f);
                                break;
                            case Passability.Walkable:
                                color = hashData.areaByIndex[value.area].color;
                                break;
                            default:
                                color = new Color();
                                break;
                        }

                        voxelPosNewData.Add(new PointData(pos, color, fragmentSize));
                        voxelLayerNewData.Add(new PointData(pos, IntegerToColor(value.layer), fragmentSize * 0.75f));

                        if (value.xPlus != -1) {
                            var connection = data[collums[volumeContainer.GetIndex(x + 1, z)].index + value.xPlus];
                            Vector3 p2 = volumeContainer.GetPos(x + 1, z, connection.y);
                            voxelConnectionsNewData.Add(new LineData(pos, p2, GetSomeColor(0), 0.001f));
                        }

                        if (value.xMinus != -1) {
                            var connection = data[collums[volumeContainer.GetIndex(x - 1, z)].index + value.xMinus];
                            Vector3 p2 = volumeContainer.GetPos(x - 1, z, connection.y);
                            voxelConnectionsNewData.Add(new LineData(new Vector3(pos.x, pos.y + (0.01f), pos.z), new Vector3(p2.x, p2.y + (0.01f), p2.z), GetSomeColor(1), 0.001f));
                        }

                        if (value.zPlus != -1) {
                            var connection = data[collums[((z + 1) * sizeX) + x].index + value.zPlus];
                            Vector3 p2 = volumeContainer.GetPos(x, z + 1, connection.y);
                            voxelConnectionsNewData.Add(new LineData(new Vector3(pos.x, pos.y + (0.01f * 2), pos.z), new Vector3(p2.x, p2.y + (0.01f * 2), p2.z), GetSomeColor(2), 0.001f));
                        }

                        if (value.zMinus != -1) {
                            var connection = data[collums[volumeContainer.GetIndex(x, z - 1)].index + value.zMinus];
                            Vector3 p2 = volumeContainer.GetPos(x, z - 1, connection.y);
                            voxelConnectionsNewData.Add(new LineData(new Vector3(pos.x, pos.y + (0.01f * 3), pos.z), new Vector3(p2.x, p2.y + (0.01f * 3), p2.z), GetSomeColor(3), 0.001f));
                        }
                    }

                    if (rawData[index].next != -2) {
                        for (; index != -1; index = rawData[index].next) {
                            var arrData = rawData[index];

                            Vector3 posMax = volumeContainer.GetPos(x, z, arrData.max);
                            Vector3 posMin = volumeContainer.GetPos(x, z, arrData.min);

                            Color color;

                            switch ((Passability)arrData.pass) {
                                case Passability.Unwalkable:
                                    color = Color.red;
                                    break;
                                case Passability.Slope:
                                    color = Color.magenta;
                                    break;
                                case Passability.Walkable:
                                    color = hashData.areaByIndex[arrData.area].color;
                                    break;
                                case Passability.Crouchable:
                                    color = hashData.areaByIndex[arrData.area].color * 0.2f;
                                    break;
                                default:
                                    color = Color.white;
                                    break;
                            }

                            voxelRawMaxNewData.Add(new PointData(posMax, color, fragmentSize));
                            voxelRawMinNewData.Add(new PointData(posMin, Color.black, fragmentSize));
                            voxelRawVolumeNewData.Add(new LineData(posMax, posMin, Color.gray, 0.001f));
                        }
                    }
                }
            }

            ChunkDebugInfo info = GetInfo(new GeneralXZData(template.gridPosition.x, template.gridPosition.z, template.properties));

            lock (lockObj) {
                voxelsCounter += voxelPosNewData.Count;
                info.voxelsCounter = voxelPosNewData.Count;
                info.voxelPos.Clear();
                info.voxelPos.AddRange(voxelPosNewData);
                info.voxelConnections.Clear();
                info.voxelConnections.AddRange(voxelConnectionsNewData);
                info.voxelLayer.Clear();
                info.voxelLayer.AddRange(voxelLayerNewData);
                info.voxelRawMax.Clear();
                info.voxelRawMax.AddRange(voxelRawMaxNewData);
                info.voxelRawMin.Clear();
                info.voxelRawMin.AddRange(voxelRawMinNewData);
                info.voxelRawVolume.Clear();
                info.voxelRawVolume.AddRange(voxelRawVolumeNewData);
            }
        }

        public static Color IntegerToColor(int i) {
            //Debug.LogFormat("val {0}, {1}, {2}, {3}", i,
            //    (i & 3) + ((i >> 6) & 3) / 6f, 
            //    ((i >> 2) & 3) + ((i >> 8) & 3) / 6f, 
            //    ((i >> 4) & 3) + ((i >> 10) & 3) / 6f);

            return new Color(
                (i & 3) + ((i >> 6) & 3) / 6f,
                ((i >> 2) & 3) + ((i >> 8) & 3) / 6f,
                ((i >> 4) & 3) + ((i >> 10) & 3) / 6f);

        }

        public static void AddSamplesData(int x, int z, AgentProperties properties, IEnumerable<CellSamplePoint_Internal> points) {
            float fragmentSize = 0.04f;
            List<PointData> newPointData = new List<PointData>();
            foreach (var item in points) {
                newPointData.Add(new PointData(new Vector3(item.x, item.y, item.z), Color.blue, fragmentSize));
            }

            ChunkDebugInfo info = GetInfo(x, z, properties);

            lock (lockObj) {
                info.cellContentDots.AddRange(newPointData);
            }
        }
        public static void AddCovers(int x, int z, AgentProperties properties, IEnumerable<Cover> covers) {
            List<PointData> coverDotsNewData = new List<PointData>();
            List<LineData> coverLinesNewData = new List<LineData>();
            List<TriangleData> coverSheetsNewData = new List<TriangleData>();

            Color hardColor = Color.magenta;
            Color softColor = new Color(hardColor.r, hardColor.g, hardColor.b, 0.2f);

            float slickLine = 0.0008f;
            float fatLine = 0.0015f;
            float dotSize = 0.04f;

            foreach (var cover in covers) {
                if (cover.coverPoints.Count == 0)
                    continue;

                //bootom
                Vector3 BL = cover.right;
                Vector3 BR = cover.left;

                float height = 0;
                switch (cover.coverType) {
                    case 1:
                        height = properties.halfCover;
                        break;
                    case 2:
                        height = properties.fullCover;
                        break;
                    default:
                        break;
                }

                //top
                Vector3 TL = BL + (Vector3.up * height);
                Vector3 TR = BR + (Vector3.up * height);

                //top and bottom
                coverLinesNewData.Add(new LineData(BL, BR, hardColor, fatLine));
                coverLinesNewData.Add(new LineData(TL, TR, hardColor, fatLine));

                //sides
                coverLinesNewData.Add(new LineData(BL, TL, hardColor, slickLine));
                coverLinesNewData.Add(new LineData(BR, TR, hardColor, slickLine));

                coverSheetsNewData.Add(new TriangleData(BL, BR, TR, softColor));
                coverSheetsNewData.Add(new TriangleData(BL, TL, TR, softColor));

                foreach (var point in cover.coverPoints) {
                    coverDotsNewData.Add(new PointData(point.positionV3, hardColor, dotSize));
                    coverDotsNewData.Add(new PointData(point.cellPos, hardColor, dotSize));

                    coverLinesNewData.Add(new LineData(point.positionV3, point.cellPos, hardColor, slickLine));
                    coverLinesNewData.Add(new LineData(TL, TR, hardColor, slickLine));
                }
            }

            ChunkDebugInfo info = GetInfo(x, z, properties);

            lock (lockObj) {
                coversCounter += covers.Count();
                info.coversCounter = covers.Count();
                info.coverDots.AddRange(coverDotsNewData);
                info.coverLines.AddRange(coverLinesNewData);
                info.coverSheets.AddRange(coverSheetsNewData);
            }
        }
        public static void AddPortalBases(Graph graph) {
            int x = graph.gridPosition.x;
            int z = graph.gridPosition.z;
            AgentProperties properties = graph.properties;

            JumpPortalBase[] portalBaseArray;
            int portalBaseCount;
            graph.GetPortalBases(out portalBaseArray, out portalBaseCount);

            List<PointData> jumpBasesDotsNewData = new List<PointData>();
            List<LineData> jumpBasesLinesNewData = new List<LineData>();

            for (int i = 0; i < portalBaseCount; i++) {
                JumpPortalBase portalBase = portalBaseArray[i];
                foreach (var cellPoint in portalBase.cellMountPoints.Values) {
                    jumpBasesLinesNewData.Add(new LineData(portalBase.positionV3, cellPoint, Color.black, 0.001f));
                    jumpBasesDotsNewData.Add(new PointData(portalBase.positionV3, Color.black, 0.04f));
                    jumpBasesDotsNewData.Add(new PointData(cellPoint, Color.black, 0.04f));
                }
            }


            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {
                jumpBasesCounter += portalBaseCount;
                info.jumpBasesCounter = portalBaseCount;
                info.jumpBasesDots.AddRange(jumpBasesDotsNewData);
                info.jumpBasesLines.AddRange(jumpBasesLinesNewData);
            }
        }

        private static void GetNodesThings(GraphGeneratorNew graphGenerator, out List<PointData> nodesPos, out List<LineData> nodesConnectins, out List<HandleThing> nodesLabels) {
            nodesPos = new List<PointData>();
            nodesConnectins = new List<LineData>();
            nodesLabels = new List<HandleThing>();

            int INVALID_VALUE = GraphGeneratorNew.INVALID_VALUE;
        
            int nodesCreated = graphGenerator.nodesCreated;
            //int nodeChunkSize = graphGenerator.nodeChunkSize;
            //int nodeLayerCount = graphGenerator.nodeLayerCount;
            //int nodeHashCount = graphGenerator.nodeHashCount;

            GraphGeneratorNew.NodeStruct[] nodesArray = graphGenerator.nodesArray;
            StackedList<GraphGeneratorNew.NodeEdgePair> nodePairs = graphGenerator.nodePairs;
            GraphGeneratorNew.NodeEdgeValue[] edgesArray = graphGenerator.edgesArray;


            GraphGeneratorNew.NodeEdgePair[] oData;
            IndexLengthInt[] oLayout;
            nodePairs.GetOptimizedData(out oData, out oLayout);

            //Debug.Log("nodesCreated " + nodesCreated);
            //Debug.Log("nodeChunkSize " + nodeChunkSize);
            //Debug.Log("nodeLayerCount " + nodeLayerCount);
            //Debug.Log("nodeHashCount " + nodeHashCount);

            StringBuilder sb = new StringBuilder();
            for (int nodeID = 0; nodeID < nodesCreated; nodeID++) {
                var node = nodesArray[nodeID];
                if (node.ID != INVALID_VALUE) {
                    Vector3 nodePos = node.positionV3;
                    nodesPos.Add(new PointData(nodePos, Color.blue, NODE_SIZE));

                    IndexLengthInt curLayout = oLayout[nodeID];

                    for (int i = 0; i < curLayout.length; i++) {
                        var pair = oData[curLayout.index + i];
                        var edge = edgesArray[pair.index];
                        sb.AppendFormat("L: {0} H: {1} C: {2}\n", pair.layer, pair.hash, edge.connection);

                        Vector3 otherNodePos = nodesArray[edge.connection].positionV3;

                        Vector3 midPoint = SomeMath.MidPoint(nodePos, otherNodePos);

                        nodesConnectins.Add(
                            new LineData(node.positionV3, midPoint,
                            edge.GetFlag(EdgeTempFlags.DouglasPeukerMarker) ? Color.green : Color.blue, 0.001f));

                        nodesConnectins.Add(new LineData(midPoint, otherNodePos, Color.red, 0.001f));
                    }
                    
                    sb.AppendFormat("ID: {0}\n", node.ID);

                    //AddLabel(nodePos, sb.ToString());
                    nodesLabels.Add(new DebugLabel(nodePos, sb.ToString()));
                    sb.Length = 0;
                }
            }

            GenericPoolArray<GraphGeneratorNew.NodeEdgePair>.ReturnToPool(ref oData);
            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref oLayout);
        }

        public static void AddNodesAfterRDP(int x, int z, AgentProperties properties, GraphGeneratorNew graphGenerator) {
            List<HandleThing> nodesLabels;
            List<PointData> nodesPos;
            List<LineData> nodesConnectins;
            GetNodesThings(graphGenerator, out nodesPos, out nodesConnectins, out nodesLabels);
    
            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {
                info.nodesPoints.Clear();
                info.nodesPoints.AddRange(nodesPos);
                info.nodesLines.Clear();
                info.nodesLines.AddRange(nodesConnectins);             
            }
        }
        public static void AddNodesPreRDP(int x, int z, AgentProperties properties, GraphGeneratorNew graphGenerator) {
            List<HandleThing> nodesLabels;
            List<PointData> nodesPos;
            List<LineData> nodesConnectins;
            GetNodesThings(graphGenerator, out nodesPos, out nodesConnectins, out nodesLabels);

            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {
                info.nodesPointsPreRDP.Clear();
                info.nodesPointsPreRDP.AddRange(nodesPos);
                info.nodesLinesPreRDP.Clear();
                info.nodesLinesPreRDP.AddRange(nodesConnectins);
            }
        }

        //not important
        public static void AddColliderBounds(int x, int z, AgentProperties properties, Collider collider) {
            Bounds bounds = collider.bounds;
            var debugedBounds = BuildParallelepiped(bounds.center - bounds.extents, bounds.center + bounds.extents, Color.green, 0.001f);

            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {
                info.colliderBounds.AddRange(debugedBounds);
            }
        }
        public static void AddTreeCollider(int x, int z, AgentProperties properties, Bounds bounds, Vector3[] verts, int[] tris) {
            //AddHandle(chunk, properties, PFDOptionEnum.BoundsCollider, new DebugBounds(bounds));

            //List<HandleThing> mesh = new List<HandleThing>();
            //for (int i = 0; i < tris.Length; i += 3) {
            //    mesh.Add(new DebugLine(verts[tris[i]], verts[tris[i + 1]]));
            //    mesh.Add(new DebugLine(verts[tris[i]], verts[tris[i + 2]]));
            //    mesh.Add(new DebugLine(verts[tris[i + 1]], verts[tris[i + 2]]));
            //}

            //AddHandle(chunk, properties, PFDOptionEnum.TreeWireMesh, mesh);

            var debugedBounds = BuildParallelepiped(bounds.center - bounds.size, bounds.center + bounds.size, Color.green, 0.001f);

            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {
                info.colliderBounds.AddRange(debugedBounds);
            }
        }
        public static void AddWalkablePolygon(int x, int z, AgentProperties properties, Vector3 a, Vector3 b, Vector3 c) {
            List<LineData> walkablePolygonLineNewData = new List<LineData>();
            List<TriangleData> walkablePolygonSheetNewData = new List<TriangleData>();

            Color solidColor = Color.cyan;
            Color lightColor = new Color(solidColor.r, solidColor.g, solidColor.b, 0.2f);

            walkablePolygonLineNewData.Add(new LineData(a, b, solidColor, 0.001f));
            walkablePolygonLineNewData.Add(new LineData(b, c, solidColor, 0.001f));
            walkablePolygonLineNewData.Add(new LineData(c, a, solidColor, 0.001f));
            walkablePolygonSheetNewData.Add(new TriangleData(a, b, c, lightColor));

            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {
                info.walkablePolygonLine.AddRange(walkablePolygonLineNewData);
                info.walkablePolygonSheet.AddRange(walkablePolygonSheetNewData);
            }
        }

        //triangulator important 
        public static void AddTriangulatorDebugLine(int x, int z, AgentProperties properties, Vector3 v1, Vector3 v2, Color color, float width = 0.001f) {
            ChunkDebugInfo info = GetInfo(x, z, properties);
            lock (lockObj) {
                info.triangulator.Add(new LineData(v1, v2, color, width));
            }
        }
        #endregion

        #region other
        public static List<LineData> BuildParallelepiped(Vector3 A, Vector3 B, Color color, float width) {
            List<LineData> result = new List<LineData>();
            result.Add(new LineData(new Vector3(A.x, A.y, A.z), new Vector3(A.x, A.y, B.z), color, width));
            result.Add(new LineData(new Vector3(A.x, A.y, B.z), new Vector3(B.x, A.y, B.z), color, width));
            result.Add(new LineData(new Vector3(B.x, A.y, B.z), new Vector3(B.x, A.y, A.z), color, width));
            result.Add(new LineData(new Vector3(B.x, A.y, A.z), new Vector3(A.x, A.y, A.z), color, width));

            result.Add(new LineData(new Vector3(A.x, A.y, A.z), new Vector3(A.x, B.y, A.z), color, width));
            result.Add(new LineData(new Vector3(A.x, A.y, B.z), new Vector3(A.x, B.y, B.z), color, width));
            result.Add(new LineData(new Vector3(B.x, A.y, B.z), new Vector3(B.x, B.y, B.z), color, width));
            result.Add(new LineData(new Vector3(B.x, A.y, A.z), new Vector3(B.x, B.y, A.z), color, width));

            result.Add(new LineData(new Vector3(A.x, B.y, A.z), new Vector3(A.x, B.y, B.z), color, width));
            result.Add(new LineData(new Vector3(A.x, B.y, B.z), new Vector3(B.x, B.y, B.z), color, width));
            result.Add(new LineData(new Vector3(B.x, B.y, B.z), new Vector3(B.x, B.y, A.z), color, width));
            result.Add(new LineData(new Vector3(B.x, B.y, A.z), new Vector3(A.x, B.y, A.z), color, width));
            return result;
        }
        //private static List<HandleThing> BuildWireMesh(Vector3[] verts, int[] tris) {
        //    List<HandleThing> result = new List<HandleThing>();
        //    for (int i = 0; i < tris.Length; i += 3) {
        //        result.Add(new DebugLine(verts[tris[i]], verts[tris[i + 1]]));
        //        result.Add(new DebugLine(verts[tris[i]], verts[tris[i + 2]]));
        //        result.Add(new DebugLine(verts[tris[i + 1]], verts[tris[i + 2]]));
        //    }
        //    return result;
        //}
        //private static List<HandleThing> BuildWireMesh(Vector3[] verts, int[] tris, Color color) {
        //    List<HandleThing> result = new List<HandleThing>();
        //    for (int i = 0; i < tris.Length; i += 3) {
        //        result.Add(new DebugLineAAColored(verts[tris[i]], verts[tris[i + 1]], color));
        //        result.Add(new DebugLineAAColored(verts[tris[i]], verts[tris[i + 2]], color));
        //        result.Add(new DebugLineAAColored(verts[tris[i + 1]], verts[tris[i + 2]], color));
        //    }
        //    return result;
        //}

        public static Color GetSomeColor(int index) {
            switch (index) {
                case 0:
                    return Color.blue;
                case 1:
                    return Color.red;
                case 2:
                    return Color.green;
                case 3:
                    return Color.magenta;
                case 4:
                    return Color.yellow;
                case 5:
                    return Color.cyan;
                default:
                    return Color.white;

            }
        }
        private static Vector3 V3small(float val) {
            return new Vector3(0, val, 0);
        }
        private static Vector3 AngleToDirection(float angle, float length) {
            return new Vector3(Mathf.Sin(Mathf.Deg2Rad * angle) * length, 0, Mathf.Cos(Mathf.Deg2Rad * angle) * length);
        }

        private static Color SetAlpha(Color color, float alpha) {
            color.a = alpha;
            return color;
        }
        #endregion



    }

    public class DebugSet {
        public List<PointData> genericDots = new List<PointData>();
        public List<LineData> genericLines = new List<LineData>();
        public List<TriangleData> genericTris = new List<TriangleData>();
        public List<HandleThing> labelsDebug = new List<HandleThing>();
        bool updated = false;

        public bool Collect(List<PointData> points, List<LineData> lines, List<TriangleData> tris) {
            lock (this) {             
                points.AddRange(genericDots);
                lines.AddRange(genericLines);
                tris.AddRange(genericTris);

                bool result = updated;
                updated = false;
                return result;
            }
        }

        public void AddDot(IEnumerable<PointData> dots) {      
            lock (this) {
                genericDots.AddRange(dots);
                updated = true;
            }
        }
        public void AddDot(PointData dot) {
            lock (this) {
                genericDots.Add(dot);
                updated = true;
            }
        }

        public void AddLine(IEnumerable<LineData> lines) {
            lock (this) {
                genericLines.AddRange(lines);
                updated = true;
            }
        }
        public void AddLine(LineData line) {
            lock (this) {
                genericLines.Add(line);
                updated = true;
            }
        }

        public void AddTriangle(IEnumerable<TriangleData> tris) {
            lock (this) {
                genericTris.AddRange(tris);
                updated = true;
            }
        }
        public void AddTriangle(TriangleData tri) {
            lock (this) {
                genericTris.Add(tri);
                updated = true;
            }
        }
        public void AddTriangle(Vector3 A, Vector3 B, Vector3 C, Color color, bool outline = true, float outlineWidth = 0.001f) {
            AddTriangle(new TriangleData(A, B, C, color));
            if (outline) {
                Color oColor = new Color(color.r, color.g, color.b, 1f);
                AddLine(new LineData[]{
                    new LineData(A, B, oColor, outlineWidth),
                    new LineData(B, C, oColor, outlineWidth),
                    new LineData(C, A, oColor, outlineWidth)
                });
            }
        }

        public void AddLabels(IEnumerable<HandleThing> labels) {
            lock (this) {
                labelsDebug.AddRange(labels);
                updated = true;
            }
        }
        public void AddLabel(HandleThing label) {
            lock (this) {
                labelsDebug.Add(label);
                updated = true;
            }
        }
        public void AddLabel(Vector3 pos, string text, DebugGroup group = DebugGroup.label) {
            lock (labelsDebug) {
                labelsDebug.Add(new DebugLabel(pos, text));
            }
        }
        public void AddLabel(Vector3 pos, double number, int digitsRound = 2, DebugGroup group = DebugGroup.label) {
            AddLabel(pos, Math.Round(number, digitsRound).ToString(), group);
        }
        public void AddLabel(Vector3 pos, object obj, DebugGroup group = DebugGroup.label) {
            AddLabel(pos, obj.ToString(), group);
        }
        public void AddLabelFormat(Vector3 pos, string format, params object[] data) {
            AddLabel(pos, string.Format(format, data));
        }


        public void Clear() {
            lock (this) {
                genericDots.Clear();
                genericLines.Clear();
                genericTris.Clear();
                labelsDebug.Clear();
            }
        }

        //dot
        public void AddDot(Vector3 pos, Color color, float size = 0.02f) {
            AddDot(new PointData(pos, color, size));
        }
        public void AddDot(IEnumerable<Vector3> pos, Color color, float size = 0.02f) {
            List<PointData> pd = new List<PointData>();
            foreach (var item in pos) {
                pd.Add(new PointData(item, color, size));
            }
            AddDot(pd);
        }
        public void AddDot(Vector3 pos, float size = 0.02f) {
            AddDot(new PointData(pos, Color.black, size));
        }

        public void AddDot(float x, float y, float z, Color color, float size = 0.02f) {
            AddDot(new PointData(new Vector3(x, y, z), color, size));
        }
        public void AddDot(float x, float y, float z, float size = 0.02f) {
            AddDot(new PointData(new Vector3(x, y, z), Color.black, size));
        }


        public void AddDot(IEnumerable<Vector3> pos, float size = 0.02f) {
            List<PointData> pd = new List<PointData>();
            foreach (var item in pos) {
                pd.Add(new PointData(item, Color.black, size));
            }
            AddDot(pd);
        }

        public void AddDot(Color color, float size = 0.02f, params Vector3[] values) {
            AddDot(values, color, size);
        }
        public void AddDot(float size = 0.02f, params Vector3[] values) {
            AddDot(values, Color.black, size);
        }
        public void AddDot(Color color, params Vector3[] values) {
            AddDot(values, color, 0.02f);
        }
        public void AddDot(params Vector3[] values) {
            AddDot(values, Color.black, 0.02f);
        }

        //line
        public void AddLine(Vector3 v1, Vector3 v2, Color color, float addOnTop = 0f, float width = 0.001f) {
            AddLine(new LineData(v1 + V3small(addOnTop), v2 + V3small(addOnTop), color, width));
        }

        public void AddLine(float v1x, float v1y, float v1z, float v2x, float v2y, float v2z, float addOnTop = 0f, float width = 0.001f) {
            AddLine(new Vector3(v1x, v1y, v1z), new Vector3(v2x, v2y, v2z), Color.black, addOnTop, width);
        }

        public void AddLine(Vector3 v1, Vector3 v2, float addOnTop = 0f, float width = 0.001f) {
            AddLine(v1, v2, Color.black, addOnTop, width);
        }
        public void AddLine(CellContentData data, Color color, float addOnTop = 0f, float width = 0.001f) {
            AddLine(data.leftV3 + V3small(addOnTop), data.rightV3 + V3small(addOnTop), color, width);
        }
        public void AddLine(CellContentData data, float addOnTop = 0f, float width = 0.001f) {
            AddLine(data.leftV3 + V3small(addOnTop), data.rightV3 + V3small(addOnTop), Color.black, width);
        }
        public void AddLine(Vector3[] chain, Color color, bool chainClosed = false, float addOnTop = 0f, float width = 0.001f) {
            int length = chain.Length;
            if (length < 2)
                return;
            if (chainClosed) {
                LineData[] ld = new LineData[length];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                ld[length - 1] = new LineData(chain[length - 1] + V3small(addOnTop), chain[0] + V3small(addOnTop), color, width);
                AddLine(ld);
            }
            else {
                LineData[] ld = new LineData[length - 1];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                AddLine(ld);
            }
        }

        public void AddLine(List<Vector3> chain, Color color, bool chainClosed = false, float addOnTop = 0f, float width = 0.001f) {
            int length = chain.Count;
            if (length < 2)
                return;
            if (chainClosed) {
                LineData[] ld = new LineData[length];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                ld[length - 1] = new LineData(chain[length - 1] + V3small(addOnTop), chain[0] + V3small(addOnTop), color, width);
                AddLine(ld);
            }
            else {
                LineData[] ld = new LineData[length - 1];
                for (int i = 0; i < length - 1; i++) {
                    ld[i] = new LineData(chain[i] + V3small(addOnTop), chain[i + 1] + V3small(addOnTop), color, width);
                }
                AddLine(ld);
            }
        }
        public void AddLine(List<Vector3> chain, bool chainClosed = false, float addOnTop = 0f, float width = 0.001f) {
            AddLine(chain, Color.black, chainClosed, addOnTop, width);
        }
        public void AddRay(Vector3 point, Vector3 direction, Color color, float length = 1f, float width = 0.001f) {
            AddLine(point, point + (direction.normalized * length), color, width);
        }
        public void AddRay(Vector3 point, Vector3 direction, float length = 1f, float width = 0.001f) {
            AddRay(point, direction, Color.black, width);
        }
        public void AddBounds(Bounds b, Color color, float width = 0.001f) {
            AddLine(Debuger_K.BuildParallelepiped(b.center - b.extents, b.center + b.extents, color, width));
        }
        public void AddBounds(Bounds b, float width = 0.001f) {
            AddBounds(b, Color.blue, width);
        }

        public void AddBounds(Bounds2D b, Color color, float height = 0f, float width = 0.001f) {
            Vector3 A1 = new Vector3(b.minX, height, b.minY);
            Vector3 A2 = new Vector3(b.minX, height, b.maxY);
            Vector3 A3 = new Vector3(b.maxX, height, b.minY);
            Vector3 A4 = new Vector3(b.maxX, height, b.maxY);

            AddLine(A1, A2, color);
            AddLine(A1, A3, color);
            AddLine(A2, A4, color);
            AddLine(A3, A4, color);
        }
        public void AddBounds(Bounds2D b, float height = 0f, float width = 0.001f) {
            AddBounds(b, Color.blue, height, width);
        }

        private static Vector3 V3small(float val) {
            return new Vector3(0, val, 0);
        }
    }
}

namespace K_PathFinder.PFDebuger {
    public class ChunkDebugInfo {
        public bool showMe = true;
        public int x, z;
        public AgentProperties properties;

        public ChunkDebugInfo(int x, int z, AgentProperties properties) {
            this.x = x;
            this.z = z;
            this.properties = properties;
        }

        #region long list of list with important debuged stuff
        //Cells
        public int cellCounter;
        public List<TriangleData> cellsArea = new List<TriangleData>();
        public List<LineData> cellEdges = new List<LineData>();
        public List<LineData> cellConnections = new List<LineData>();
        public List<TriangleData> cellEdgesOrder = new List<TriangleData>();

        //covers
        public int coversCounter;
        public List<PointData> coverDots = new List<PointData>();
        public List<LineData> coverLines = new List<LineData>();
        public List<TriangleData> coverSheets = new List<TriangleData>();

        //samples
        public List<PointData> cellContentDots = new List<PointData>();
        public List<LineData> cellContentLines = new List<LineData>();

        //jump bases
        public int jumpBasesCounter;
        public List<LineData> jumpBasesLines = new List<LineData>();
        public List<PointData> jumpBasesDots = new List<PointData>();

        //voxels
        public int voxelsCounter;
        public List<PointData> voxelPos = new List<PointData>();
        public List<LineData> voxelConnections = new List<LineData>();
        public List<PointData> voxelLayer = new List<PointData>();

        public List<PointData> voxelRawMax = new List<PointData>();
        public List<PointData> voxelRawMin = new List<PointData>();
        public List<LineData> voxelRawVolume = new List<LineData>();

        //nodes
        public List<PointData> nodesPoints = new List<PointData>();
        public List<LineData> nodesLines = new List<LineData>();
        public List<PointData> nodesPointsPreRDP = new List<PointData>();
        public List<LineData> nodesLinesPreRDP = new List<LineData>();

        //bounds
        public List<LineData> colliderBounds = new List<LineData>();
        public List<LineData> chunkBounds = new List<LineData>();

        //walkable polygons
        public List<LineData> walkablePolygonLine = new List<LineData>();
        public List<TriangleData> walkablePolygonSheet = new List<TriangleData>();

        //triangulator
        public List<LineData> triangulator = new List<LineData>();

        //cellMap
        public List<LineData> cellMap = new List<LineData>();
        #endregion

        public void Clear() {
            cellsArea.Clear();
            cellEdges.Clear();
            cellEdgesOrder.Clear();
            cellConnections.Clear();
            coverDots.Clear();
            coverLines.Clear();
            coverSheets.Clear();
            cellContentDots.Clear();
            cellContentLines.Clear();
            jumpBasesLines.Clear();
            jumpBasesDots.Clear();
            voxelPos.Clear();
            voxelConnections.Clear();
            voxelLayer.Clear();
            voxelRawMax.Clear();
            voxelRawMin.Clear();
            voxelRawVolume.Clear();
            nodesPoints.Clear();
            nodesLines.Clear();
            nodesPointsPreRDP.Clear();
            nodesLinesPreRDP.Clear();
            colliderBounds.Clear();
            chunkBounds.Clear();
            walkablePolygonLine.Clear();
            walkablePolygonSheet.Clear();
            triangulator.Clear();
            cellMap.Clear();
        }
    }

    #region HandlThings
    public abstract class HandleThing {
        public abstract void ShowHandle();
    }
    public class DebugLabel : HandleThing {
        public Vector3 pos;
        public string text;
        public DebugLabel(Vector3 pos, string text) {
            this.pos = pos;
            this.text = text;
        }

        public override void ShowHandle() {
            Handles.BeginGUI();
            Color color = GUI.color;
            GUI.color = Color.black;
            Handles.Label(pos, text);
            GUI.color = color;
            Handles.EndGUI();
        }
    }



    //currently no use outside labels



    //public class DebugLine : HandleThing {
    //    protected Vector3 v1, v2;
    //    public DebugLine(Vector3 v1, Vector3 v2) {
    //        this.v1 = v1;
    //        this.v2 = v2;
    //    }
    //    public override void ShowHandle() {
    //        Handles.DrawLine(v1, v2);
    //    }
    //}
    //public class DebugLineColored : DebugLine {
    //    Color color;
    //    public DebugLineColored(Vector3 from, Vector3 to, Color color) : base(from, to) {
    //        this.color = color;
    //    }
    //    public override void ShowHandle() {
    //        Handles.color = new Color(color.r, color.g, color.b, Handles.color.a);
    //        base.ShowHandle();
    //    }
    //}

    //public class DebugLineAA: DebugLine {
    //    public DebugLineAA(Vector3 from, Vector3 to) : base(from, to) {}
    //    public override void ShowHandle() {
    //        Handles.DrawAAPolyLine(v1, v2);
    //    }
    //}
    //public class DebugLineAAColored : DebugLine {
    //    protected Color color;
    //    public DebugLineAAColored(Vector3 from, Vector3 to, Color color) : base(from, to) {
    //        this.color = color;
    //    }
    //    public override void ShowHandle() {
    //        Handles.color = new Color(color.r, color.g, color.b, Handles.color.a);
    //        Handles.DrawAAPolyLine(v1, v2);
    //    }
    //}

    //public class DebugLineAASolid : DebugLine {
    //    public DebugLineAASolid(Vector3 from, Vector3 to) : base(from, to) { }

    //    public override void ShowHandle() {
    //        Color c = Handles.color;
    //        Handles.color = new Color(c.r, c.g, c.b, 1f);            
    //        Handles.DrawAAPolyLine(v1, v2);
    //        Handles.color = c;
    //    }
    //}
    //public class DebugLineAAColoredSolid : DebugLineAAColored {
    //    public DebugLineAAColoredSolid(Vector3 from, Vector3 to, Color color) : base(from, to, color) { }

    //    public override void ShowHandle() {
    //        Color c = Handles.color;
    //        Handles.color = base.color;
    //        Handles.DrawAAPolyLine(v1, v2);
    //        Handles.color = c;
    //    }
    //}
    //public class DebugBounds : HandleThing {
    //    Bounds bounds;
    //    bool haveColor;
    //    Color color; 

    //    public DebugBounds(Bounds bounds) {
    //        this.bounds = bounds;
    //        haveColor = false;
    //    }
    //    public DebugBounds(Bounds bounds, Color color) {
    //        this.bounds = bounds;
    //        haveColor = false;
    //        this.color = color;
    //    }

    //    public override void ShowHandle() {
    //        Color tColor = Handles.color;
    //        if (haveColor) 
    //            Handles.color = color;

    //        DrawParallelepiped(bounds.center - bounds.extents, bounds.center + bounds.extents);

    //        Handles.color = tColor;
    //    }

    //    private static void DrawParallelepiped(Vector3 A, Vector3 B) {    
    //        Handles.DrawAAPolyLine(new Vector3(A.x, A.y, A.z), new Vector3(A.x, A.y, B.z));
    //        Handles.DrawAAPolyLine(new Vector3(A.x, A.y, B.z), new Vector3(B.x, A.y, B.z));
    //        Handles.DrawAAPolyLine(new Vector3(B.x, A.y, B.z), new Vector3(B.x, A.y, A.z));
    //        Handles.DrawAAPolyLine(new Vector3(B.x, A.y, A.z), new Vector3(A.x, A.y, A.z));

    //        Handles.DrawAAPolyLine(new Vector3(A.x, A.y, A.z), new Vector3(A.x, B.y, A.z));
    //        Handles.DrawAAPolyLine(new Vector3(A.x, A.y, B.z), new Vector3(A.x, B.y, B.z));
    //        Handles.DrawAAPolyLine(new Vector3(B.x, A.y, B.z), new Vector3(B.x, B.y, B.z));
    //        Handles.DrawAAPolyLine(new Vector3(B.x, A.y, A.z), new Vector3(B.x, B.y, A.z));

    //        Handles.DrawAAPolyLine(new Vector3(A.x, B.y, A.z), new Vector3(A.x, B.y, B.z));
    //        Handles.DrawAAPolyLine(new Vector3(A.x, B.y, B.z), new Vector3(B.x, B.y, B.z));
    //        Handles.DrawAAPolyLine(new Vector3(B.x, B.y, B.z), new Vector3(B.x, B.y, A.z));
    //        Handles.DrawAAPolyLine(new Vector3(B.x, B.y, A.z), new Vector3(A.x, B.y, A.z));
    //    }
    //}

    //public abstract class DebugPosSize : HandleThing {
    //    protected Vector3 pos;
    //    protected float size;
    //    public DebugPosSize(Vector3 pos, float size) {
    //        this.pos = pos;
    //        this.size = size;
    //    }
    //}
    //public class DebugDotCap : DebugPosSize {
    //    public DebugDotCap(Vector3 pos, float size) : base(pos, size) { }

    //    public override void ShowHandle() {
    //        Handles.DotHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
    //    }
    //}

    //public class DebugDotCapSolid : DebugPosSize {
    //    public DebugDotCapSolid(Vector3 pos, float size) : base(pos, size) { }

    //    public override void ShowHandle() {
    //        Color c = Handles.color;
    //        Handles.color = new Color(c.r, c.g, c.b, 1f);
    //        Handles.DotHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
    //        Handles.color = c;
    //    }
    //}
    //public class DebugDisc : DebugPosSize {
    //    Vector3 normal;
    //    public DebugDisc(Vector3 pos, Vector3 normal, float radius) : base(pos, radius) {
    //        this.normal = normal;
    //    }
    //    public override void ShowHandle() {
    //        Handles.DrawSolidDisc(pos, normal, size);
    //    }
    //}
    //public class DebugDiscCameraFaced : DebugPosSize {
    //    public DebugDiscCameraFaced(Vector3 pos, float radius) : base(pos, radius) { }
    //    public override void ShowHandle() {
    //        Handles.DrawSolidDisc(pos, (pos - Camera.current.gameObject.transform.position), size);
    //    }
    //}
    //public class DebugSphere : DebugPosSize {
    //    public DebugSphere(Vector3 pos, float size) : base(pos, size) { }
    //    public override void ShowHandle() {
    //        Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
    //    }
    //}
    //public class DebugPolygon : HandleThing {
    //    Vector3 a, b, c;
    //    public DebugPolygon(Vector3 a, Vector3 b, Vector3 c) {
    //        this.a = a;
    //        this.b = b;
    //        this.c = c;
    //    }
    //    public override void ShowHandle() {
    //        Handles.DrawAAPolyLine(a, b, c, a);
    //    }
    //}
    //public class DebugCross3D : DebugPosSize {
    //    public DebugCross3D(Vector3 pos, float size) : base(pos, size) { }
    //    public override void ShowHandle() {
    //        Handles.DrawLine(new Vector3(pos.x - size, pos.y, pos.z), new Vector3(pos.x + size, pos.y, pos.z));
    //        Handles.DrawLine(new Vector3(pos.x, pos.y - size, pos.z), new Vector3(pos.x, pos.y + size, pos.z));
    //        Handles.DrawLine(new Vector3(pos.x, pos.y, pos.z - size), new Vector3(pos.x, pos.y, pos.z + size));
    //    }
    //}
    //public class DebugDotColored : DebugDotCap {
    //    Color color;
    //    public DebugDotColored(Vector3 pos, float size, Color color) : base(pos, size) {
    //        this.color = color;
    //    }

    //    public override void ShowHandle() {
    //        Handles.color = new Color(color.r, color.g, color.b, Handles.color.a);
    //        base.ShowHandle();
    //    }
    //}

    //public class DebugMeshFancy : HandleThing {
    //    Color color;
    //    Vector3[] points;
    //    public DebugMeshFancy(Vector3[] points, Color color) {
    //        this.color = color;
    //        this.points = points;
    //    }

    //    public override void ShowHandle() {
    //        Handles.color = new Color(color.r, color.g, color.b, Handles.color.a);
    //        Handles.DrawAAConvexPolygon(points);
    //    }
    //}
    //public class DebugMesh : HandleThing {
    //    Vector3[] points;
    //    public DebugMesh(params Vector3[] points) {
    //        this.points = points;
    //    }

    //    public override void ShowHandle() {
    //        Handles.DrawAAConvexPolygon(points);
    //    }
    //}

    //public class DebugWireArc : HandleThing {
    //    Vector3 center, normal, from;
    //    float radius, angle;

    //    bool colored;
    //    Color color;

    //    public DebugWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius) {
    //        this.center = center;
    //        this.normal = normal;
    //        this.from = from;
    //        this.angle = angle;
    //        this.normal = normal;
    //        this.radius = radius;
    //    }

    //    public DebugWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius, Color color) :this(center, normal, from, angle, radius) {
    //        this.colored = true;
    //        this.color = color;
    //    }

    //    public override void ShowHandle() {
    //        if (colored) {
    //            Color handlesColor = Handles.color;
    //            Handles.color = new Color(color.r, color.g, color.b, handlesColor.a);

    //            Handles.DrawWireArc(center, normal, from, angle, radius);
    //            Handles.color = handlesColor;
    //        }
    //        else {
    //            Handles.DrawWireArc(center, normal, from, angle, radius);
    //        }    
    //    }
    //}

    //public class DebugWireDisc : HandleThing {
    //    Vector3 position;
    //    Vector3 normal;
    //    float radius;

    //    bool colored;
    //    Color color;

    //    public DebugWireDisc(Vector3 position, Vector3 normal, float radius) {
    //        this.position = position;
    //        this.normal = normal;
    //        this.radius = radius;
    //    }

    //    public DebugWireDisc(Vector3 position, Vector3 normal, float radius, Color color) :this(position, normal, radius) {
    //        this.colored = true;
    //        this.color = color;
    //    }


    //    public override void ShowHandle() {
    //        if (colored) {
    //            Color handlesColor = Handles.color;
    //            Handles.color = new Color(color.r, color.g, color.b, handlesColor.a);

    //            Handles.DrawWireDisc(position, normal, radius);
    //            Handles.color = handlesColor;
    //        }
    //        else {
    //            Handles.DrawWireDisc(position, normal, radius);
    //        }
    //    }
    //}
    //public class DebugPolyLine : HandleThing {
    //    Vector3[] positions;
    //    bool colored, solid;
    //    Color color;

    //    public DebugPolyLine(bool solid = false, params Vector3[] positions) {
    //        this.solid = solid;
    //        this.positions = positions;
    //    }
    //    public DebugPolyLine(Color color, bool solid = false, params Vector3[] positions) {
    //        this.positions = positions;
    //        this.solid = solid;
    //        this.colored = true;
    //        this.color = color;
    //    }

    //    public override void ShowHandle() {
    //        if (colored) {
    //            Color handlesColor = Handles.color;
    //            if (solid)
    //                Handles.color = color;
    //            else
    //                Handles.color = new Color(color.r, color.g, color.b, handlesColor.a);

    //            Handles.DrawPolyLine(positions);
    //            Handles.color = handlesColor;
    //        }
    //        else {
    //            Handles.DrawPolyLine(positions);
    //        }
    //    }
    //}

    //public class DebugLineAAAwesome: HandleThing {
    //    Vector3 a,b;
    //    bool colored, solid;
    //    Color color;

    //    public DebugLineAAAwesome(Vector3 a, Vector3 b) {
    //        this.a = a;
    //        this.b = b;
    //    }
    //    public DebugLineAAAwesome(Vector3 a, Vector3 b, Color color, bool solid = false) {
    //        this.a = a;
    //        this.b = b;
    //        this.solid = solid;
    //        this.colored = true;
    //        this.color = color;
    //    }

    //    public override void ShowHandle() {
    //        if (colored) {
    //            Color handlesColor = Handles.color;
    //            if (solid)
    //                Handles.color = color;
    //            else
    //                Handles.color = new Color(color.r, color.g, color.b, handlesColor.a);

    //            Handles.DrawAAPolyLine(a,b);
    //            Handles.color = handlesColor;
    //        }
    //        else {
    //            Handles.DrawAAPolyLine(a, b);
    //        }
    //    }
    //}
    //public class DebugMesh : HandleThing {
    //    //Color color;
    //    Mesh mesh;
    //    Matrix4x4 matrix;
    //    public DebugMesh(Vector3[] verts, int[] tris, Color color) {
    //        //this.color = color;
    //        this.mesh = new Mesh();
    //        mesh.vertices = verts;
    //        mesh.triangles = tris;
    //    }

    //    public DebugMesh(Mesh mesh, Color color) {
    //        //this.color = color;
    //        this.mesh = mesh;
    //        matrix = Matrix4x4.identity;
    //    }

    //    public override void ShowHandle() {
    //        Graphics.DrawMeshNow(mesh, matrix, 2);
    //    }
    //}
    #endregion
}
#endif