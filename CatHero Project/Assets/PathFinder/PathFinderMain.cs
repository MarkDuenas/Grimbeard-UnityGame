using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using K_PathFinder.Settings;
using K_PathFinder.Graphs;
using K_PathFinder.Serialization2;
using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using K_PathFinder.Collector;

#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using K_PathFinder.PFDebuger;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace K_PathFinder {
    //**********************************************//
    //***********PathFinder Main class**************//
    //**********************************************//
    //******This is where all things happening******//
    //**********************************************//
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static partial class PathFinder {
        public const string VERSION = "0.50";
        public const int CELL_GRID_SIZE = 10; //hardcoded value. this value tell density of cell library in graph. 10x10 is good enough for now

        //important resources
        public static PathFinderScene sceneInstance;
        public static PathFinderSettings settings { get; private set; }
        private static ComputeShader rasterization2D, rasterization3D;
        private static bool resourcesInit = false;

        private static bool init = false;
        private static Dictionary<GeneralXZData, Graph> chunkData = new Dictionary<GeneralXZData, Graph>(); //actual navmesh
        private static Dictionary<XZPosInt, YRangeInt> chunkRange = new Dictionary<XZPosInt, YRangeInt>();  //chunk height difference
        private static AreaPassabilityHashData hashData = new AreaPassabilityHashData(); //little thing to avoid accessing area library all time.  just send copy of it in every thread so it a lot less locked
        private static List<AgentProperties> allAgentProperties = new List<AgentProperties>();
        
        //main thread control
        private static object threadLock = new object();  
        private static Thread pathfinderMainThread;
        private static ManualResetEvent pathfinderMainThreadEvent = new ManualResetEvent(true);
        
        //state that define how navmesh should be cleared
        private static MainThreadState threadState = MainThreadState.NormalWork; 
        private static Queue<MainThreadState> mainThreadChange = new Queue<MainThreadState>(); //state change is queued.

        //additional threads control
        private static int activeThreads = 0;

        //spagetti that connect different threads. adding things to that queues dont executed imediadly. this is low priority queue
        public static Queue<Action> mainThreadDelegateQueue = new Queue<Action>();//unity thread
        public static Queue<Action> pathFinderDelegateQueue = new Queue<Action>();//pathdinder thread
        
        //public values
        public static float gridSize;     

        //queues. that where all type of tasks are
        private static Dictionary<GeneralXZData, NavMeshTemplateCreation> currentWorkDictionary = new Dictionary<GeneralXZData, NavMeshTemplateCreation>();//dictionary with queued work
        private static int previousWorkDictionaryCounter;
        
        //pipeline:
        //stage1: populate template in Unity Thread and queue to stage2
        //stage2: populate template in PathFinder Thread and move to sepparated thread
        //calculate in sepparated thread and move to stage3
        //stage3: finalize in PathFinder thread and move to stage4
        //stage4: finalize in Unity thread
        //Unity (1) > Pathfinder (2) > random thread > PathFinder (3) > Unity (4)
        private static Queue<NavMeshTemplateCreation> templateQueueStage1 = new Queue<NavMeshTemplateCreation>();//queue to populate template in unity thread and then move to pathfinder thread
        private static Queue<NavMeshTemplateCreation> templateQueueStage2 = new Queue<NavMeshTemplateCreation>();//queue to populate template in PathFinder thread
        private static Queue<NavMeshTemplateCreation> templateQueueStage3 = new Queue<NavMeshTemplateCreation>();//queue to finalize graph in pathfinder thread
        private static Queue<NavMeshTemplateCreation> templateQueueStage4 = new Queue<NavMeshTemplateCreation>();//queue to finalize graph in unity thread
        private static WorkBatcher<NavMeshTemplateDestruction> destructQueue = new WorkBatcher<NavMeshTemplateDestruction>();
        
        //general purpose work batcher for less optimized queries
        public static ThreadPoolWorkBatcher<IThreadPoolWorkBatcherMember> queryBatcher = new ThreadPoolWorkBatcher<IThreadPoolWorkBatcherMember>();

        private static bool flagRVO = false; //if true then local avoidance should be updated

        private static Action onNavmeshGenerationFinished;

        #region default bitmask values
        public static readonly BitMaskPF defaultLayer = 1;
        public static readonly BitMaskPF ignoreLayer = 2;
        public static readonly BitMaskPF allLayers = new BitMaskPF(
            true, true, false, false, false, false, false, false,  //8
            true, true, true, true, true, true, true, true,        //16
            true, true, true, true, true, true, true, true,        //24
            true, true, true, true, true, true, true, true         //32
        );
        public static readonly BitMaskPF allLayersExceptIgnore = new BitMaskPF(
            true, false, false, false, false, false, false, false, //8
            true, true, true, true, true, true, true, true,        //16
            true, true, true, true, true, true, true, true,        //24
            true, true, true, true, true, true, true, true         //32
        );
        #endregion

#if UNITY_EDITOR
        private static StringBuilder sb = new StringBuilder();
        public static Shader dotShader;
        public static Shader lineShader;
        public static Shader trisShader;
#endif
        public enum MainThreadState : int {
            NormalWork = 0,                                     //do nothing special
            ClearNavmeshAndGeneration = 1,                      //clear only data
            ClearNavmeshAndGenerationAndMetadataAndStopWork = 2 //clear data and prepare for scene change and prevent further work      
        }

        //main thread state cause debuging where it stuck is pain sometimes    
        public enum PathFinderActions : int {
            DestroyingNavmesh = 0,
            ConnectingNavmesh = 1,
            QueryBeforePositionUpdate = 2,
            LABeforePositionUpdate = 3,
            NavmeshSampled = 4,
            ProcessSellContent = 5,
            RecalculateDeltaCost = 6,
            Queries = 7,
            LocalAvoidance = 8
        }
        public struct PathFinderActionState {
            public bool finished;
            public int workCount;
            public DateTime time;

            public void Set(int workCount, DateTime time) {
                this.workCount = workCount;
                this.time = time;
                finished = true;
            }

            public void Reset() {
                finished = false;
                workCount = 0;
            }
        }
        public static PathFinderActionState[] navmeshThreadState = new PathFinderActionState[9];

#if UNITY_EDITOR        
        static bool firstUpdate = true;

        static PathFinder() {
#if UNITY_2018_1_OR_NEWER
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
#else
            EditorApplication.playmodeStateChanged += PlayModeStateChanged;
#endif
            EditorApplication.update += OnEditorUpdate;

#if UNITY_2018_1_OR_NEWER
            EditorSceneManager.activeSceneChangedInEditMode += EditorSceneManagerOnSceneChangedEditmode;
#else
            EditorSceneManager.activeSceneChanged += EditorSceneManagerOnSceneChangedEditmode;
#endif
            EditorSceneManager.sceneOpened += EditorSceneManagerOnSceneOpened;

            SceneManager.sceneLoaded += SceneManagerOnSceneLoaded;
            SceneManager.sceneUnloaded += SceneManagerOnSceneUnloaded;
            SceneManager.activeSceneChanged += SceneManagerOnSceneChanged;      
        }


#if UNITY_2018_1_OR_NEWER
        //controling clearing on pathfinder when editor change state
        static void PlayModeStateChanged(PlayModeStateChange change) {
            //Debug.Log(change);
            switch (change) {
                case PlayModeStateChange.EnteredEditMode:
                    ChangeTargetState(MainThreadState.NormalWork);
                    InitCurrentScene();
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    ChangeTargetState(MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork);
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    ChangeTargetState(MainThreadState.NormalWork);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    ChangeTargetState(MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork);
                    break;
            }
        }
#else
        //controling clearing on pathfinder when editor change state
        static void PlayModeStateChanged() {
            if (!EditorApplication.isCompiling & !EditorApplication.isUpdating)
                InitCurrentScene();

            ChangeTargetState(MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork);
            ChangeTargetState(MainThreadState.NormalWork);
        }
#endif

        // called second
        static void SceneManagerOnSceneLoaded(Scene scene, LoadSceneMode mode) {
            //Debug.Log("OnSceneLoaded: " + scene.name);
            InitCurrentScene();
        }
        static void SceneManagerOnSceneUnloaded(Scene scene) {
            //Debug.Log("OnSceneUnloaded");
        }
        static void SceneManagerOnSceneChanged(Scene scene1, Scene scene2) {
            //Debug.LogFormat("OnSceneChanged from {0}, to {1}", scene1.name, scene2.name);
        }
        
        static void OnEditorUpdate() {
            if (sceneInstance != null)
                sceneInstance.MoveNext();

            if (firstUpdate) {
                firstUpdate = false;
                PathFinderInit();
                InitCurrentScene();
                Update(); //force pathfinder to exit blocked state
            }
        }

        //only worked delegate in editor mode that called on scene change
        static void EditorSceneManagerOnSceneChangedEditmode(Scene scene1, Scene scene2) {
            //Debug.Log("OnSceneChangedEditmode");        
            OnSceneClosed();
        }

        static void EditorSceneManagerOnSceneOpened(Scene scene, OpenSceneMode mode) {
            //Debug.Log("EditorSceneManagerOnSceneOpened");
            InitCurrentScene();  
        }
#else
        static PathFinder() {
            Update(); //force pathfinder to exit blocked state
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoaded() {
            //Debug.Log("OnBeforeSceneLoaded");        
            PathFinderInit();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoaded() {
            //Debug.Log("OnAfterSceneLoaded");
            InitCurrentScene();
        }

        public static void CallThisWhenSceneObjectIsGone() {
            OnSceneClosed();      
            Update();
        }

        public static void PathFinderInit() {
            if (init) return;
            init = true;

            //Debug.Log("PathFinderInit");

            InitResources();
            InitManagers();

            if (pathfinderMainThread == null) {
                pathfinderMainThreadEvent = new ManualResetEvent(false);
                pathfinderMainThread = new Thread(PathFinderMainThread);                                   
                pathfinderMainThread.Name = "Pathfinder Main Thread";
                pathfinderMainThread.Start();
            }

            gridSize = settings.gridSize;
            foreach (var item in settings.areaLibrary) {
                hashData.AddAreaHash(item);
            }

#if UNITY_EDITOR
            Debuger_K.Init();
#endif
            ChangeTargetState(MainThreadState.NormalWork);
            Update();
        }

        public static void Update() {
            pathfinderMainThreadEvent.Set();
        }

        public static void UpdateRVO() {
            if (multithread) {
                lock (threadLock) {
                    flagRVO = true;
                    Update();
                }
            }
            else {
                flagRVO = true;
                Update();
            }
        }

        static void OnSceneClosed() {       
            sceneInstance = null;
            ChangeTargetState(MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork);
        }
        
        static void InitCurrentScene() {
            //init all mods
            foreach (var awm in UnityEngine.Object.FindObjectsOfType<AreaWorldMod>()) {
                awm.OnPathFinderSceneInit();
            }
            
            //check all terrains
            foreach (var item in UnityEngine.Object.FindObjectsOfType<TerrainNavmeshSettings>()) {
                item.OnPathFinderSceneInit();
            }

            ChangeTargetState(MainThreadState.NormalWork);

            //try find gameobject
            GameObject helperGameObject = GameObject.Find(settings.helperName);

            //create if there is none
            if (helperGameObject == null)
                helperGameObject = new GameObject(settings.helperName);

            //try find component on that gameobject
            sceneInstance = helperGameObject.GetComponent<PathFinderScene>();

            //create if there is none
            if (sceneInstance == null)
                sceneInstance = helperGameObject.AddComponent<PathFinderScene>();

            sceneInstance.SetCoroutine(PathfinderUnityThread());
            sceneInstance.InitComputeShaderRasterization3D(rasterization3D);
            sceneInstance.InitComputeShaderRasterization2D(rasterization2D);
            sceneInstance.Init();
            LoadCurrentSceneData();
        }

        //return if scene object was loaded
        //public static void Init(string comment = null) {
        //    lock (mainThreadChange) {
        //        if (threadState == MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork)
        //            return;
        //    }
        //}

        static void InitResources() {
            if (resourcesInit) return;
            resourcesInit = true;

            //Debug.Log("InitResources");
            AssetDatabase.Refresh();

            settings = PathFinderSettings.LoadSettings();
            rasterization2D = Resources.Load<ComputeShader>("ComputeShaderRasterization2D");
            rasterization3D = Resources.Load<ComputeShader>("ComputeShaderRasterization3D");

            //Debug.LogFormat("settings == null {0}", settings == null);
            //Debug.LogFormat("rasterization2D == null {0}", rasterization2D == null);
            //Debug.LogFormat("rasterization3D == null {0}", rasterization3D == null);


#if UNITY_EDITOR
            string debugerShaderFolder =
                string.Format("{0}/{1}/{2}/", new string[] {
                        PathFinderSettings.FindProjectPath(),
                        PathFinderSettings.EDITOR_FOLDER,
                        PathFinderSettings.SHADERS_FOLDER});

            dotShader = AssetDatabase.LoadAssetAtPath<Shader>(debugerShaderFolder + "DotShader.shader");
            lineShader = AssetDatabase.LoadAssetAtPath<Shader>(debugerShaderFolder + "LineShader.shader");
            trisShader = AssetDatabase.LoadAssetAtPath<Shader>(debugerShaderFolder + "TrisShader.shader");

            if (dotShader == null)
                Debug.LogError("for some reason dot shader is null");
            if (lineShader == null)
                Debug.LogError("for some reason line shader is null");
            if (trisShader == null)
                Debug.LogError("for some reason thiangle shader is null");
#endif
        }

        static void InitManagers() {
            PathFinderData.CellIDManagerInit();//setting up thingy that assign cell global IDs
            ColliderCollector.InitCollector();//creating primitives for collector
            InitCellContent();
        }

        //adds some delegate to be executed in main thread
        public static void AddMainThreadDelegate(Action action) {
            lock (mainThreadDelegateQueue)
                mainThreadDelegateQueue.Enqueue(action);
        }

        //adds some delegate to be executed in pathfinder thread
        public static void AddPathfinderThreadDelegate(Action action) {
            lock (pathFinderDelegateQueue)
                pathFinderDelegateQueue.Enqueue(action);
        }

        /// <summary>
        /// Internal function that called in OnEnable inside AgentProperties so PathFinder know all properties list
        /// </summary>
        public static void AddAgentProperties(AgentProperties prop) {
            lock (allAgentProperties) {
                allAgentProperties.Add(prop);
            }
        }

        /// <summary>
        /// Internal function that called in OnDestroy inside AgentProperties so PathFinder know all properties list
        /// </summary>
        public static void RemoveAgentProperties(AgentProperties prop) {
            lock (allAgentProperties) {
                allAgentProperties.Remove(prop);
            }
        }

        public static void ResetAgentPropertiesFlags() {
            lock (allAgentProperties) {
                foreach (var ap in allAgentProperties) {
                    lock (ap)
                        ap.internal_flagList = 0;
                }
            }
        }

#region pipelines
        //Pipeline:      
        //1) populate template in UNITY main thread
        //2) continue in whatever thread
        //3) return to connection in pathfinder thread
        //4) finish unnesesary things in unity thread
        //unity thread > pathfinder thread > whatever thread > pathfinder thread > unity thread

        private static void PathFinderMainThread() {
            int workCount;//used for log
            int threads;

            while (true) {
                //in case multithread disabled 
                if (threadLock == null)
                    Debug.Log("threadLock == null");

                lock (threadLock) {
                    if (settings.useMultithread == false) {
                        pathfinderMainThreadEvent.Reset();
                        pathfinderMainThreadEvent.WaitOne();
                        Thread.Sleep(100);
                        continue;
                    }
                }
 
                //wait for event
                pathfinderMainThreadEvent.WaitOne();

                threads = settings.maxThreads;
                ResetWorkLog(); //reset debug of state

#region navmesh clearing state mashine
                //under this region in implemented navmesh clearing.
                //PathFinder can potentialu stuck if this message is not called. 
                //to unstuck pathfinder call PathFinder.ChangeTargetState(PathFinder.MainThreadState.NormalWork);

                MainThreadState curState, targetState;
                lock (mainThreadChange) {
                    curState = threadState;
                    targetState = mainThreadChange.Count > 0 ? mainThreadChange.Dequeue() : threadState;
                    //Debug.Log(curState + "\n" + targetState);
                }
                
                if (curState == 0 && targetState > 0) {
                    if (targetState == MainThreadState.ClearNavmeshAndGeneration) {
                        ClearDataAndReset(false);
                        //current state can continue normal work
                    }
                    if(targetState == MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork) {
                        ClearDataAndReset(true);
                        lock (mainThreadChange)
                            threadState = targetState;             
                    }
                    pathfinderMainThreadEvent.Reset();
                    continue;
                }
                else if(curState == MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork) {
                    if(targetState == MainThreadState.NormalWork) {
                        lock (mainThreadChange)
                            threadState = MainThreadState.NormalWork;
                        //re init(?)
                    }
                    if (targetState == MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork) {
                        ClearDataAndReset(true);           
                        pathfinderMainThreadEvent.Reset();
                        continue;
                    }
                }
#endregion

                //callbacks from main thread
                lock (pathFinderDelegateQueue) {
                    while (pathFinderDelegateQueue.Count > 0) {
                        pathFinderDelegateQueue.Dequeue().Invoke();
                    }
                }

#region navmesh finishing events handling
                //removing old navmesh
                Queue<NavMeshTemplateDestruction> curDestruction = destructQueue.currentBatch; //taking current batch of work
                destructQueue.Flip();//locking so all new work go to next batch

                workCount = curDestruction.Count;
                if (curDestruction.Count > 0) {
                    while (curDestruction.Count > 0) {
                        var current = curDestruction.Dequeue();
                        GeneralXZData currentXZData = current.data;

                        Graph graph;
                        lock (chunkData) {
                            if (chunkData.TryGetValue(currentXZData, out graph) == false)
                                continue;
                            chunkData.Remove(currentXZData);
                        }

                        RemoveFromGraphExternalCellContent(graph);
                        graph.OnDestroyGraph(false);

                        if (current.queueNewGraphAfter)
                            QueueNavMeshTemplateToPopulation(currentXZData);
                    }
                }
                LogWork(PathFinderActions.DestroyingNavmesh, workCount);

                //stage 2 is where pathfinder populate template after it populated in Unity thread and moved to population in PathFinder thread and send to whatever thread
                lock (templateQueueStage2) {
                    if (templateQueueStage2.Count > 0) {
                        while (templateQueueStage2.Count > 0) {
                            NavMeshTemplateCreation template = templateQueueStage2.Dequeue();
                            template.PopulateTemplatePathFinderThread();
                            template.graphGenerationWaitCallback = new WaitCallback(NavMeshTemplateCreation.ThreadWorker);
                            template.callbackAfterGraphGeneration = (NavMeshTemplateCreation value) => {
                                lock (threadLock)
                                    activeThreads--;

                                PushTaskStage3Template(value);//push to pathfinder main thread this template after it finished
                            };

                            ThreadPool.QueueUserWorkItem(template.graphGenerationWaitCallback, template);//sending template to thread pool
                        }
                    }
                }

                //stage 3 is where finished templates with finished graphs are ready to be assembled into navmesh
                //they do this one by one to avoid misconnections
                lock (templateQueueStage3) {
                    workCount = templateQueueStage3.Count;
                    if (templateQueueStage3.Count > 0) {
                        while (templateQueueStage3.Count > 0) {
                            NavMeshTemplateCreation template = templateQueueStage3.Dequeue();

                            Graph graph = template.graph;
                            SetGraph(graph); //add graph to navmesh
                            graph.FunctionsToFinishGraphInPathfinderMainThread();
                            AddToGraphExternalCellContent(graph);

                            lock (templateQueueStage4) {
                                templateQueueStage4.Enqueue(template);
                            }
                        }
                    }
                }
                LogWork(PathFinderActions.ConnectingNavmesh, workCount);
#endregion

                queryBatcher.SwichBatch();
                workCount = queryBatcher.PerformBeforeNavmeshPositionUpdate();
                LogWork(PathFinderActions.QueryBeforePositionUpdate, workCount);

                bool rvo = flagRVO;
                if(rvo)
                    UpdateLocalAvoidanceBeforePositionSample();
                LogWork(PathFinderActions.LABeforePositionUpdate, LAagents.Count);
                RecalculateDeltaCostBeforePositionUpdate();

                UpdatePositionSamples(threads);
                LogWork(PathFinderActions.NavmeshSampled, lastTimeNavmeshPositionsSampled);

                workCount = ProcessCellContentEvents();
                LogWork(PathFinderActions.ProcessSellContent, workCount);

                workCount = RecalculateDeltaCostAfterPositionUpdate();
                LogWork(PathFinderActions.RecalculateDeltaCost, workCount);

                workCount = queryBatcher.PerformCurrentBatch(threads);
                LogWork(PathFinderActions.Queries, workCount);

                //rvo
                if (rvo) {
                    flagRVO = false;
                    UpdateLocalAvoidance();
                }
                LogWork(PathFinderActions.LocalAvoidance, LAagents.Count);

                if (queryBatcher.haveWork == false & eventCellContentHaveWork == false) {
                    lock (threadLock) {
                        workCount = 0;
                        workCount += destructQueue.currentBatch.Count;

                        lock (templateQueueStage2)
                            workCount += templateQueueStage2.Count;
                        lock (templateQueueStage3)
                            workCount += templateQueueStage3.Count;

                        lock (mainThreadChange) {
                            curState = threadState;
                            targetState = mainThreadChange.Count > 0 ? mainThreadChange.Dequeue() : threadState;
                        }

                        if (workCount == 0)
                            pathfinderMainThreadEvent.Reset();
                    }
                }
                lock (currentWorkDictionary) {
                    if (previousWorkDictionaryCounter > 0 && currentWorkDictionary.Count == 0) {
                        if (onNavmeshGenerationFinished != null)
                            onNavmeshGenerationFinished.Invoke();
                    }
                    previousWorkDictionaryCounter = currentWorkDictionary.Count;
                }

#if UNITY_EDITOR
                Debuger_K.CheckNavmeshIntegrity();
#endif
            }
        }


        //version of main pipeline for debuging issues. designed to be run in pmain thread
        private static void PathFinderMainThreadForDebug() {
            //*******************NOT MULTITHREAD*******************//
            lock (mainThreadDelegateQueue) {
                while (mainThreadDelegateQueue.Count > 0) {
                    mainThreadDelegateQueue.Dequeue().Invoke();
                }
            }

            lock (pathFinderDelegateQueue) {
                while (pathFinderDelegateQueue.Count > 0) {
                    pathFinderDelegateQueue.Dequeue().Invoke();
                }
            }
            
            int workCount;//used for log
            ResetWorkLog(); //reset debug of state

            #region navmesh clearing state mashine
            MainThreadState curState, targetState;
            lock (mainThreadChange) {
                curState = threadState;
                targetState = mainThreadChange.Count > 0 ? mainThreadChange.Dequeue() : threadState;
            }

            if (curState == 0 && targetState > 0) {
                if (targetState == MainThreadState.ClearNavmeshAndGeneration) {
                    ClearDataAndReset(false);
                    //current state can continue normal work
                    return;
                }
                if (targetState == MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork) {
                    ClearDataAndReset(true);
                    curState = targetState;
                    return;
                }

            }
            else if (curState == MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork) {
                if (targetState == MainThreadState.NormalWork) {
                    curState = MainThreadState.NormalWork;
                    //re init(?)
                }
                if (targetState == MainThreadState.ClearNavmeshAndGenerationAndMetadataAndStopWork) {
                    ClearDataAndReset(true);
                    return;
                }
            }
            #endregion

            //removing graphs
            var curDestruction = destructQueue.currentBatch;
            destructQueue.Flip();
            workCount = curDestruction.Count;
            if (curDestruction.Count > 0) {
                while (curDestruction.Count > 0) {
                    var current = curDestruction.Dequeue();
                    GeneralXZData currentXZData = current.data;

                    Graph graph;
                    if (chunkData.TryGetValue(currentXZData, out graph) == false)
                        continue;

                    chunkData.Remove(currentXZData);
                    graph.OnDestroyGraph(false);

                    if (current.queueNewGraphAfter)
                        QueueNavMeshTemplateToPopulation(currentXZData);
                }
            }
            LogWork(PathFinderActions.DestroyingNavmesh, workCount);

            //creating graphs
            workCount = templateQueueStage1.Count;
            if (templateQueueStage1.Count > 0) {
                while (templateQueueStage1.Count > 0) {
                    NavMeshTemplateCreation template = templateQueueStage1.Dequeue();
                    template.PopulateTemplateUnityThread();
                    template.PopulateTemplatePathFinderThread();
                    template.GenerateGraph();
                    Graph graph = template.graph;
                    SetGraph(graph); //add graph to navmesh
                    graph.FunctionsToFinishGraphInPathfinderMainThread();
                    AddToGraphExternalCellContent(graph);
                    graph.FunctionsToFinishGraphInUnityThread();   
                    graph.OnFinishGraph();
                    template.OnFinishingGeneration();
                    currentWorkDictionary.Remove(new GeneralXZData(graph.gridPosition, graph.properties));

#if UNITY_EDITOR
                    if (Debuger_K.doDebug)
                        graph.DebugGraph(false); //cause it already updated from generation
#endif
                }
            }
            LogWork(PathFinderActions.ConnectingNavmesh, workCount);

            queryBatcher.SwichBatch();
            workCount = queryBatcher.PerformBeforeNavmeshPositionUpdate();
            LogWork(PathFinderActions.QueryBeforePositionUpdate, workCount);

            bool rvo = flagRVO;
            if (rvo)
                UpdateLocalAvoidanceBeforePositionSample();
            LogWork(PathFinderActions.LABeforePositionUpdate, LAagents.Count);
            RecalculateDeltaCostBeforePositionUpdate();

            UpdatePositionSamples(0);
            LogWork(PathFinderActions.NavmeshSampled, lastTimeNavmeshPositionsSampled);

            workCount = ProcessCellContentEvents();
            LogWork(PathFinderActions.ProcessSellContent, workCount);

            workCount = RecalculateDeltaCostAfterPositionUpdate();
            LogWork(PathFinderActions.RecalculateDeltaCost, workCount);

            workCount = queryBatcher.PerformCurrentBatchThreadSafe();
            LogWork(PathFinderActions.Queries, workCount);

            //rvo
            if (rvo) {
                flagRVO = false;
                UpdateLocalAvoidance();
            }
            LogWork(PathFinderActions.LocalAvoidance, LAagents.Count);

            if (previousWorkDictionaryCounter > 0 && currentWorkDictionary.Count == 0) {
                if (onNavmeshGenerationFinished != null)
                    onNavmeshGenerationFinished.Invoke();
            }
            previousWorkDictionaryCounter = currentWorkDictionary.Count;

#if UNITY_EDITOR
            Debuger_K.CheckNavmeshIntegrity();
#endif
            //*******************NOT MULTITHREAD*******************//
        }

        private static IEnumerator PathfinderUnityThread() {
            while (true) {
                MainThreadState curState;
                lock (mainThreadChange) {
                    curState = threadState;
                }

                if (curState > 0) {
                    //Debug.Log(curState);
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                if (multithread) {
                    //populate new batch of work with colliders 
                    if (templateQueueStage1.Count != 0 && activeThreads <= settings.maxThreads) {//if current active work count lesser than maximum count
                        while (templateQueueStage1.Count != 0 && activeThreads <= settings.maxThreads) {//add new work while have work or exeed maximum number of work    
                            lock (threadLock)
                                activeThreads++;

                            NavMeshTemplateCreation template = templateQueueStage1.Dequeue();//take template
                            template.PopulateTemplateUnityThread();//populate it with colliders
                            PushTaskStage2Template(template);
                        }//unity really strugle doing this so find better way 
                    }

                    //finalize finished work
                    lock (templateQueueStage4) {//lock since template added to it in another thread
                        if (templateQueueStage4.Count != 0) {//if it have work at all
                            while (templateQueueStage4.Count != 0) {
                                NavMeshTemplateCreation template = templateQueueStage4.Dequeue();//take template

                                Graph graph = template.graph;
                                graph.FunctionsToFinishGraphInUnityThread();//finish work which use unity API

                                AddPathfinderThreadDelegate(() => {
                                    graph.OnFinishGraph();//set flag so graph finaly ready to work
                                    template.OnFinishingGeneration();
                                });              

                                //Debug.Log("Lock templateQueueStage3");
                                lock (currentWorkDictionary)
                                    currentWorkDictionary.Remove(new GeneralXZData(graph.gridPosition, graph.properties));//finaly remove template from dictionary
                                
#if UNITY_EDITOR
                                //jump check cause otherwise new connections will be generated in main thread and debug will be added at the end of that
                                if(graph.jumpPortalBasesCount == 0 && Debuger_K.doDebug)
                                    graph.DebugGraph(false); //cause it already updated from generation
#endif
                            }
                        }
                    }

                    lock (mainThreadDelegateQueue) {
                        while (mainThreadDelegateQueue.Count > 0) {
                            mainThreadDelegateQueue.Dequeue().Invoke();
                        }
                    }
                }
                else {
                    PathFinderMainThreadForDebug();
                }
#if UNITY_EDITOR
                Debuger_K.OnSceneUpdate();
#endif
                yield return new WaitForEndOfFrame();
                continue;
            }
        }
#endregion
        
        public static void GetAllGraphs(List<Graph> listToPopulate) {
            lock (chunkData) 
                listToPopulate.AddRange(chunkData.Values);            
        }
        
        private static void LogWork(PathFinderActions action, int work) {
            lock (navmeshThreadState) {
                navmeshThreadState[(int)action].Set(work, DateTime.Now);
            }
        }

        private static void ResetWorkLog() {
            lock (navmeshThreadState) {
                for (int i = 0; i < navmeshThreadState.Length; i++) {
                    navmeshThreadState[i].Reset();
                }
            }
        }
        


        public static void SetOnNavmeshGenerationCompleteDelegate(Action action) {
            lock (threadLock)
                onNavmeshGenerationFinished = action;
        }

        private static void PushTaskStage2Template(NavMeshTemplateCreation input) {
            lock (templateQueueStage2)
                templateQueueStage2.Enqueue(input);
            Update();
        }
        private static void PushTaskStage3Template(NavMeshTemplateCreation input) {
            lock (templateQueueStage3)
                templateQueueStage3.Enqueue(input);
            Update();
        }


                
        private static void SetGraph(Graph graph) {
            //Debug.Log("set " + graph.gridPosition);
            lock (chunkData) {
                chunkData.Add(new GeneralXZData(graph.gridPosition, graph.properties), graph);
            }
        }

#region editor
#if UNITY_EDITOR
        public static int DrawAreaSellector(int current) {
            return settings.DrawAreaSellector(current);
        }
#endif
#endregion

#region PathFinder management
        public static void SetMaxThreads(int value) {
            settings.maxThreads = Math.Max(value, 1);
        }
        public static void SetCurrentTerrainMethod(TerrainCollectorType type) {
            settings.terrainCollectionType = type;
        }
      

        public static void ChangeTargetState(MainThreadState state) {
            lock (mainThreadChange) {
                mainThreadChange.Enqueue(state);
            }
            Update();
        }
        
        //clearing all navmesh generation, queries and debug
        //if clearOnlyNavmeshData false then metadata like chunk map will not be cleared and only 
        private static void ClearDataAndReset(bool removeSceneObjectsFromUsedData) {  
            lock (pathFinderDelegateQueue)
                pathFinderDelegateQueue.Clear();
            lock (mainThreadDelegateQueue)
                mainThreadDelegateQueue.Clear();

            //queries for info extraction             
            queryBatcher.ClearAndReset();

            //clearing current work dictionary and stops it
            lock (currentWorkDictionary) {
                foreach (var item in currentWorkDictionary.Values) {
                    item.Stop();
                }
                currentWorkDictionary.Clear();
            }

            //destroy current graphs
            lock (chunkData) {
                List<KeyValuePair<GeneralXZData, Graph>> data = new List<KeyValuePair<GeneralXZData, Graph>>(chunkData);
                foreach (var item in data) {
                    item.Value.OnDestroyGraph(true);
                    chunkData.Remove(item.Key);
                }
                chunkData.Clear();
            }
            ResetAgentPropertiesFlags();

            //this should be cleared in main thread
            lock(templateQueueStage1)
                templateQueueStage1.Clear();
            lock (templateQueueStage4)
                templateQueueStage4.Clear();

            //and this is in pathfinder thread
            lock (templateQueueStage2)
                templateQueueStage2.Clear();
            lock (templateQueueStage3)
                templateQueueStage3.Clear();

            destructQueue.Clear();

            //reset cell IDs so it's start from 0
            PathFinderData.CellIDManagerClear();

#if UNITY_EDITOR
            Debuger_K.ClearGeneric();
            Debuger_K.ClearChunksDebug();
#endif

            if (removeSceneObjectsFromUsedData) {
                //local avoidance agents
                //probably dont need cause they remove itself anyway
                //ClearLocalAvoidanceAgents();
                //clear sampler
                ClearPositionSampling();
                //remove all current cost modifyers
                ClearDeltaCost();
                //remove all data stored for further navmesh generation
                ClearChunkContentMap();
                ClearCellContent();

#if UNITY_EDITOR
                Debuger_K.ClearAdditionalDebugSets();
#endif
            }
        }


#endregion
                
#region area stuff
        /// <summary>
        /// Return area from global dictionary by it's ID.
        /// ID is writen on the left side in PathFinder menu.
        /// 0 = Default, 1 = Not Walkble.
        /// </summary>
        public static Area GetArea(int id) {
            if (id >= 0 && id < settings.areaLibrary.Length)
                return settings.areaLibrary[id];
            else {
                Debug.LogWarning("Requested Area index are higher than maximum index. Returned Default");
                return settings.areaLibrary[0];
            }
        }
        public static Area GetArea(string name) {
            foreach (var area in settings.areaLibrary) {
                if (area.name == name)
                    return area;
            }
            return null;
        }
        /// <summary>
        /// Return area index if it presented in global area list
        /// </summary>
        public static bool TryGetAreaIndex(Area area, out int index) {
            Area[] areas = settings.areaLibrary;

            for (int i = 0; i < areas.Length; i++) {
                if(areas[i] == area) {
                    index = i;
                    return true;
                }
            }

            index = 0;
            return false;
        }        
        /// <summary>
        /// Return area index if it presented in global area list
        /// </summary>
        public static bool TryGetAreaIndex(string areaName, out int index) {
            Area[] areas = settings.areaLibrary;

            for (int i = 0; i < areas.Length; i++) {
                if (areas[i].name == areaName) {
                    index = i;
                    return true;
                }
            }

            index = 0;
            return false;
        }
        /// <summary>
        /// return amount of areas in global area list
        /// </summary>
        public static int areaCount {
            get { return settings.areaLibrary.Length; }
        }
        /// <summary>
        /// return Default area which have id 0
        /// </summary>
        public static Area getDefaultArea {
            get { return settings.areaLibrary[0]; }
        }
        /// <summary>
        /// return Unwalkable area which have id 1
        /// </summary>
        public static Area getUnwalkableArea {
            get { return settings.areaLibrary[1]; }
        }
#endregion
        
#region raycasting
        //Personal guideline:
        //1) Make use of GetCellForRaycast. It is pain when raycast started at corner or edge of chunk and only bugs comes from this. 
        //   It will slightly offset XZ and it is much better then gazzilion other problems. Much better consistent offset then unexpected results.
        //2) Main parameters for raycastings is "position", "direction" and "properties". Everything else is optional. So all raycast functions take firstly this three and THEN everything else

        //Notes:
        //* Starting position can be altered not only in GetCellForRaycast but also in raycasting itself. Cause it also can be started on cell Edge. 
        //  There is actualy myltiple ways to fix that but when start position are at exact start or end point of edge then it is disaster.
        //  So if starting point are too close to cell edge then it will be offseted to direction of cell center. Which guarantee that it will be still enclosed by Cell.

        //TODO:
        // * Variations of raycasting without range parameter

        //EXAMPLE ON HOWTO PREVENT ISSUES
        public static bool GetCellForRaycast(ref float x, ref float y, ref float z, AgentProperties properties, out Cell cell) {
            //slight offset in case when request exactly on edges and corners
            if (x % gridSize <= 0.01f)
                x += 0.02f;
            if (z % gridSize <= 0.01f)
                z += 0.02f;

            if (TryGetCell(x, y, z, properties, out cell)) {
                x += ((cell.centerVector3.x - x) * 0.001f);
                z += ((cell.centerVector3.z - z) * 0.001f);
                return true;
            }
            else {
                return false;
            }
        }

#region normal raycast
#region single raycast
        /// <summary>
        ///function to call them all
        /// </summary>
        public static bool Raycast(float x, float y, float z, float directionX, float directionZ, AgentProperties properties, float maxRange, Area expectedArea, bool checkArea, Passability expectedPassability, bool checkPassability, int layerMask, out RaycastHitNavMesh2 hit) {
            //get start cell
            Cell cell;
            //if (GetCellForRaycast(ref x, ref y, ref z, properties, out cell) == false) {
            //    hit = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.OutsideGraph, null);
            //    return false;
            //}

            //slight offset in case when request exactly on edges and corners
            if (x % gridSize <= 0.01f)
                x += 0.02f;
            if (z % gridSize <= 0.01f)
                z += 0.02f;
            
            if (TryGetCell(x, y, z, properties, out cell) == false | cell == null) {
                hit = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.OutsideGraph, null);
                return false;             
            }

            x += ((cell.centerVector3.x - x) * 0.001f);
            z += ((cell.centerVector3.z - z) * 0.001f);

            //check is cell inside layer mask
            if ((1 << cell.bitMaskLayer & layerMask) == 0) {
                hit = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.RayHitCellExcludedByLayerMask, cell);
                return true;
            }

            //check is cell have expected area
            if (checkArea && cell.area != expectedArea) {
                hit = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.AreaChange, cell);
                return true;
            }

            //check is cell have expected passability
            if (checkPassability && cell.passability != expectedPassability) {
                hit = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.PassabilityChange, cell);
                return true;
            }

            x += (cell.centerVector3.x - x) * 0.001f;
            z += (cell.centerVector3.z - z) * 0.001f;

            PathFinderMainRaycasting.RaycastBody(x, y, z, directionX, directionZ, cell, maxRange * maxRange, 
                checkArea, checkPassability, expectedArea, expectedPassability, layerMask, out hit);//perform raycast
            return (int)hit.resultType > 1;//return true if it too close to borders to perform raycast, or hit border, or hit cell with change in expected area or passability (if it checked)
        }

        //only position and direction
        /// <summary>
        /// Navmesh raycast.
        /// Take position and direction. Note direction is X and Z axis. You specify direction in top down view.
        /// Normaly raycast return true when it outside border or hit something - this one return pretty much always true since max range is float.MaxValue. So this function just void
        /// </summary>     
        public static void Raycast(float x, float y, float z, float directionX, float directionZ, AgentProperties properties, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            Raycast(x, y, z, directionX, directionZ, properties, float.MaxValue, null, false, Passability.Unwalkable, false, layerMask, out hit);
        }
        /// <summary>
        /// Navmesh raycast.
        /// Take position and direction. Note direction is X and Z axis. You specify direction in top down view.
        /// return true pretty much always exept when raycase start outside navmesh
        /// </summary>     
        public static bool Raycast(Vector3 position, Vector2 directionXZ, AgentProperties properties, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(position.x, position.y, position.z, directionXZ.x, directionXZ.y, properties, float.MaxValue, null, false, Passability.Unwalkable, false, layerMask, out hit);
        }
        //range
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction and max range. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. return false only when hit outside maxRange
        /// </summary>       
        public static bool Raycast(float x, float y, float z, float directionX, float directionZ, AgentProperties properties, float maxRange, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(x, y, z, directionX, directionZ, properties, maxRange, null, false, Passability.Unwalkable, false, layerMask, out hit);
        }
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction and max range. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. return false only when hit outside maxRange
        /// </summary>       
        public static bool Raycast(Vector3 position, Vector2 directionXZ, float maxRange, AgentProperties properties, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(position.x, position.y, position.z, directionXZ.x, directionXZ.y, properties, maxRange, null, false, Passability.Unwalkable, false, layerMask, out hit);
        }
        //range, area
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction, max range and expected Area. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. Hit triggered if cell dont have expected Area. return false only when hit outside maxRange
        /// </summary>       
        public static bool Raycast(float x, float y, float z, float directionX, float directionZ, AgentProperties properties, float maxRange, Area expectedArea, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(x, y, z, directionX, directionZ, properties, maxRange, expectedArea, true, Passability.Unwalkable, false, layerMask, out hit);
        }
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction, max range and expected Area. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. Hit triggered if cell dont have expected Area. return false only when hit outside maxRange
        /// </summary>       
        public static bool Raycast(Vector3 position, Vector2 directionXZ, AgentProperties properties, float maxRange, Area expectedArea, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(position.x, position.y, position.z, directionXZ.x, directionXZ.y, properties, maxRange, expectedArea, true, Passability.Unwalkable, false, layerMask, out hit);
        }
        //range, passability
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction, max range and expected Passability. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. Hit triggered if cell dont have expected Passability. return false only when hit outside maxRange
        /// </summary>       
        public static bool Raycast(float x, float y, float z, float directionX, float directionZ, AgentProperties properties, float maxRange, Passability expectedPassability, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(x, y, z, directionX, directionZ, properties, maxRange, null, false, expectedPassability, true, layerMask, out hit);
        }
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction, max range and expected Passability. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. Hit triggered if cell dont have expected Passability. return false only when hit outside maxRange
        /// </summary>            
        public static bool Raycast(Vector3 position, Vector2 directionXZ, AgentProperties properties, float maxRange, Passability expectedPassability, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(position.x, position.y, position.z, directionXZ.x, directionXZ.y, properties, maxRange, null, false, expectedPassability, true, layerMask, out hit);
        }
        //range, area, passability
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction, max range, expected Area and Passability. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. Hit triggered if cell dont have expected Passability. return false only when hit outside maxRange
        /// </summary>       
        public static bool Raycast(float x, float y, float z, float directionX, float directionZ, AgentProperties properties, float maxRange, Area expectedArea, Passability expectedPassability, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(x, y, z, directionX, directionZ, properties, maxRange, expectedArea, true, expectedPassability, true, layerMask, out hit);
        }
        /// <summary>
        /// Navmesh raycast.
        /// Take position, direction, max range, expected Area and Passability. Note direction is X and Z axis. You specify direction in top down view.
        /// return true if outside navmesh or hit distance is closer than maxRange. Hit triggered if cell dont have expected Passability. return false only when hit outside maxRange
        /// </summary>             
        public static bool Raycast(Vector3 position, Vector2 directionXZ, AgentProperties properties, float maxRange, Area expectedArea, Passability expectedPassability, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            return Raycast(position.x, position.y, position.z, directionXZ.x, directionXZ.y, properties, maxRange, expectedArea, true, expectedPassability, true, layerMask, out hit);
        }

        public static void RaycastForMoveTemplate(float x, float y, float z, float dirX, float dirY, float range, Cell cell, out RaycastHitNavMesh2 hit, int layerMask = 1) {
            PathFinderMainRaycasting.RaycastBody(x, y, z, dirX, dirY, cell, range * range, true, true, cell.area, cell.passability, layerMask, out hit);//perform raycast
            //return (int)hit.resultType > 1;//return true if it too close to borders to perform raycast, or hit border, or hit cell with change in expected area or passability (if it checked)
        }
#endregion

#region multiple with ONE range parameter
        //generic private function to single raycast
        private static bool Raycast(float x, float y, float z, Vector2[] directions, int directionsLength, AgentProperties properties, float maxRange, Area expectedArea, bool checkArea, Passability expectedPassability, bool checkPassability, ref RaycastHitNavMesh2[] hit, bool checkResult, int layerMask = 1) {
            int length = directions.Length;

            if (hit == null || hit.Length != length) {
                hit = new RaycastHitNavMesh2[length];
            }

            Cell cell;
            //if (GetCellForRaycast(ref x, ref y, ref z, properties, out cell) == false) {
            //    hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.OutsideGraph, null);
            //    return false;
            //}

            //slight offset in case when request exactly on edges and corners
            if (x % gridSize <= 0.01f)
                x += 0.02f;
            if (z % gridSize <= 0.01f)
                z += 0.02f;
            
            if (TryGetCell(x, y, z, properties, out cell) == false | cell == null) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.OutsideGraph, null);
                return false;
            }

            x += ((cell.centerVector3.x - x) * 0.001f);
            z += ((cell.centerVector3.z - z) * 0.001f);

            //check is cell inside layer mask
            if ((1 << cell.bitMaskLayer & layerMask) == 0) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.RayHitCellExcludedByLayerMask, cell);
                return true;
            }

            //check is cell have expected area
            if (checkArea && cell.area != expectedArea) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.AreaChange, cell);
                return true;
            }
            //check is cell have expected passability
            if (checkPassability && cell.passability != expectedPassability) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.PassabilityChange, cell);
                return true;
            }     

            Vector2 cur;
            //RaycastAllocatedData allocated = GenericPool<RaycastAllocatedData>.Take();//take allocated data to perform raycast 
            for (int i = 0; i < length; i++) {
                cur = directions[i];
                RaycastHitNavMesh2 curHit;
                PathFinderMainRaycasting.RaycastBody(x, y, z, cur.x, cur.y, cell, maxRange * maxRange, checkArea, checkPassability, expectedArea, expectedPassability, layerMask, out curHit);
                hit[i] = curHit;
            }
            //GenericPool<RaycastAllocatedData>.ReturnToPool(ref allocated);//return allocated data to pool 
            
            if (checkResult) {
                for (int i = 0; i < length; i++) {
                    if ((int)hit[i].resultType > 1)
                        return true;
                }
                return false;
            }
            else
                return true;
        }

        //only position and direction
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float maxRange, Area expectedArea, bool checkArea, Passability expectedPassability, bool checkPassability, ref RaycastHitNavMesh2[] hit, bool checkResult, int layerMask = 1) {
            return Raycast(x, y, z, directions, directions.Length, properties, maxRange, null, false, Passability.Unwalkable, false, ref hit, checkResult, layerMask);
        }

        //only position and direction
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, ref RaycastHitNavMesh2[] hit, bool checkResult = true, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, float.MaxValue, null, false, Passability.Unwalkable, false, ref hit, checkResult, layerMask);
        }
        //range
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float maxRange, ref RaycastHitNavMesh2[] hit, bool checkResult = true, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRange, null, false, Passability.Unwalkable, false, ref hit, checkResult, layerMask);
        }
        //range, area
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float maxRange, Area expectedArea, ref RaycastHitNavMesh2[] hit, bool checkResult = true, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRange, expectedArea, true, Passability.Unwalkable, false, ref hit, checkResult, layerMask);
        }
        //range, passability
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float maxRange, Passability expectedPassability, ref RaycastHitNavMesh2[] hit, bool checkResult = true, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRange, null, false, expectedPassability, true, ref hit, checkResult, layerMask);
        }
        //range, area, passability
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float maxRange, Area expectedArea, Passability expectedPassability, ref RaycastHitNavMesh2[] hit, bool checkResult = true, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRange, expectedArea, true, expectedPassability, true, ref hit, checkResult, layerMask);
        }
#endregion

#region multiple with MULTIPLE range parameter
        //generic private function to single raycast
        private static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float[] maxRanges, Area expectedArea, bool checkArea, Passability expectedPassability, bool checkPassability, ref RaycastHitNavMesh2[] hit, bool checkResult, int layerMask = 1) {
            int length = directions.Length;

            if (hit == null || hit.Length != length) {
                hit = new RaycastHitNavMesh2[length];
            }

            Cell cell;
            //if (GetCellForRaycast(ref x, ref y, ref z, properties, out cell) == false) {
            //    hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.OutsideGraph, null);
            //    return false;
            //}

            if (x % gridSize <= 0.01f)
                x += 0.02f;
            if (z % gridSize <= 0.01f)
                z += 0.02f;

            if (TryGetCell(x, y, z, properties, out cell) == false | cell == null) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.OutsideGraph, null);
                return false;
            }

            x += ((cell.centerVector3.x - x) * 0.001f);
            z += ((cell.centerVector3.z - z) * 0.001f);


            //check is cell inside layer mask
            if ((1 << cell.bitMaskLayer & layerMask) == 0) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.RayHitCellExcludedByLayerMask, cell);
                return true;
            }

            //check is cell have expected area
            if (checkArea && cell.area != expectedArea) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.AreaChange, cell);
                return true;
            }

            //check is cell have expected passability
            if (checkPassability && cell.passability != expectedPassability) {
                hit[0] = new RaycastHitNavMesh2(x, y, z, NavmeshRaycastResultType2.PassabilityChange, cell);
                return true;
            }
            
            Vector2 cur;
            //RaycastAllocatedData allocated = GenericPool<RaycastAllocatedData>.Take();//take allocated data to perform raycast 
            if (maxRanges != null) {
                for (int i = 0; i < length; i++) {
                    cur = directions[i];
                    RaycastHitNavMesh2 curHit;
                    PathFinderMainRaycasting.RaycastBody(x, y, z, cur.x, cur.y, cell, maxRanges[i] * maxRanges[i], checkArea, checkPassability, expectedArea, expectedPassability, layerMask, out curHit);
                    hit[i] = curHit;
                }
            }
            else {
                for (int i = 0; i < length; i++) {
                    cur = directions[i];
                    RaycastHitNavMesh2 curHit;
                    PathFinderMainRaycasting.RaycastBody(x, y, z, cur.x, cur.y, cell, float.MaxValue, checkArea, checkPassability, expectedArea, expectedPassability, layerMask, out curHit);
                    hit[i] = curHit;
                }
            }
            //GenericPool<RaycastAllocatedData>.ReturnToPool(ref allocated);//return allocated data to pool 
            
            if (checkResult) {
                for (int i = 0; i < length; i++) {
                    if ((int)hit[i].resultType > 1)
                        return true;
                }
                return false;
            }
            else
                return true;
        }
        //range
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float[] maxRanges, ref RaycastHitNavMesh2[] hit, bool checkResult = false, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRanges, null, false, Passability.Unwalkable, false, ref hit, checkResult, layerMask);
        }
        //range, area
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float[] maxRanges, Area expectedArea, ref RaycastHitNavMesh2[] hit, bool checkResult = false, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRanges, expectedArea, true, Passability.Unwalkable, false, ref hit, checkResult, layerMask);
        }
        //range, passability
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float[] maxRanges, Passability expectedPassability, ref RaycastHitNavMesh2[] hit, bool checkResult = false, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRanges, null, false, expectedPassability, true, ref hit, checkResult, layerMask);
        }
        //range, area, passability
        public static bool Raycast(float x, float y, float z, Vector2[] directions, AgentProperties properties, float[] maxRanges, Area expectedArea, Passability expectedPassability, ref RaycastHitNavMesh2[] hit, bool checkResult = false, int layerMask = 1) {
            return Raycast(x, y, z, directions, properties, maxRanges, expectedArea, true, expectedPassability, true, ref hit, checkResult, layerMask);
        }
#endregion
#endregion
#endregion

#region management
        //give me Graph
        //public static bool GetGraph(XZPosInt pos, AgentProperties properties, out Graph graph) {
        //    Init("GetGraph"); 
        //    lock (_chunkData) {
        //        GeneralXZData key = new GeneralXZData(pos, properties);
        //        if (_chunkData.TryGetValue(key, out graph))
        //            return true;
        //        else {
        //            QueueNavMeshTemplateToPopulation(pos, properties);
        //            return false;
        //        }
        //    }
        //}
        //public static bool GetGraph(int x, int z, AgentProperties properties, out Graph graph) {   
        //    return GetGraph(new XZPosInt(x, z), properties, out graph);
        //}
        //public static bool GetGraphFrom(XZPosInt pos, Directions direction, AgentProperties properties, out Graph graph) {
        //    switch (direction) {
        //        case Directions.xPlus:
        //        return GetGraph(pos.x + 1, pos.z, properties, out graph);
        //        case Directions.xMinus:
        //        return GetGraph(pos.x - 1, pos.z, properties, out graph);
        //        case Directions.zPlus:
        //        return GetGraph(pos.x, pos.z + 1, properties, out graph);
        //        case Directions.zMinus:
        //        return GetGraph(pos.x, pos.z - 1, properties, out graph);
        //        default:
        //            Debug.LogError("defaul direction are not exist");
        //            graph = null;
        //            return false;
        //    }
        //}
        
        //try give me Graph  
        public static bool TryGetGraph(XZPosInt pos, AgentProperties properties, out Graph graph) {
            return chunkData.TryGetValue(new GeneralXZData(pos, properties), out graph);
        }
        public static bool TryGetGraph(int x, int z, AgentProperties properties, out Graph graph) {
            return TryGetGraph(new XZPosInt(x, z), properties, out graph);
        }
        public static void TryGetGraph(int x, int z, int sizeX, int sizeZ, AgentProperties properties, List<Graph> populate, bool addNulls = false) {
            if (sizeX < 1 | sizeZ < 1)
                throw new ArgumentException("size of requested graphs cannot be less than 1");
            
            for (int gridX = 0; gridX < sizeX; gridX++) {
                for (int gridZ = 0; gridZ < sizeZ; gridZ++) {
                    Graph graph;
                    if (TryGetGraph(x + gridX, z + gridZ, properties, out graph))
                        populate.Add(graph);
                    else if(addNulls)
                        populate.Add(null);
                }
            }
        }
        public static bool TryGetGraphFrom(XZPosInt pos, Directions direction, AgentProperties properties, out Graph graph) {
            switch (direction) {
                case Directions.xPlus:
                    return TryGetGraph(pos.x + 1, pos.z, properties, out graph);

                case Directions.xMinus:
                    return TryGetGraph(pos.x - 1, pos.z, properties, out graph);

                case Directions.zPlus:
                    return TryGetGraph(pos.x, pos.z + 1, properties, out graph);

                case Directions.zMinus:
                    return TryGetGraph(pos.x, pos.z - 1, properties, out graph);

                default:
                    Debug.LogError("defaul direction are not exist");
                    graph = null;
                    return false;
            }
        } 


        private static void FixPosition(ref float x, ref float z) {
            if (x % gridSize < 0.001f)
                x += 0.001f;
            if (z % gridSize < 0.001f)
                z += 0.001f;
        }

        //uses function that only searching inside graph at target chunk. used in some cases where nneed more solid implementation
        public static bool TryGetClosestCell_Internal(float x, float y, float z, AgentProperties properties, out Vector3 resultPosition, out Cell resultCell, out bool outside) {
            if (properties == null)
                throw new NullReferenceException("Agent properties can't be null when searching closest cell");

            //make a little offset so position that laying exactly on edges dont cause lots of trobles
            if (x % gridSize < 0.001f) x += 0.001f;
            if (z % gridSize < 0.001f) z += 0.001f;

            Graph graph;
            if (TryGetGraph(ToChunkPosition(x, z), properties, out graph) == false || graph.anyCellMapData == false) {//try to get graph
                resultCell = null;
                resultPosition.x = resultPosition.y = resultPosition.z = 0;
                outside = true;
                return false;
            }

            return graph.GetCellSimpleMap(x, y, z, -1, out resultCell, out resultPosition, out outside);
        }
        
        /// <summary>
        /// try get closest Cell.
        /// return closest position to navmesh (inside chunk where position are)
        /// will offset position if it laying on chunk borders 
        /// </summary>  
        public static NavmeshSampleResultType TryGetClosestCell(float x, float y, float z, AgentProperties properties, out Vector3 resultPosition, out Cell resultCell, int layer = -1, int layerMask = 1) {
            if (properties == null) 
                throw new NullReferenceException("Agent properties can't be null when searching closest cell");
            
            //make a little offset so position that laying exactly on edges dont cause lots of trobles
            if (x % gridSize < 0.001f) x += 0.001f;
            if (z % gridSize < 0.001f) z += 0.001f;
            
            Graph graph;
            if (TryGetGraph(ToChunkPosition(x, z), properties, out graph) == false || graph.anyCellMapData == false) {//try to get graph
                //if there is no graph try to get closest to target point. can be costly of there is huge amount of chunks. but oh well
                float closestDistSqr = float.MaxValue;

                foreach (var curGraph in chunkData.Values) {
                    if (curGraph.empty)
                        continue;

                    float curGraphDistSqr = SomeMath.SqrDistance(x, z, curGraph.chunk.centerV2);
                    if (curGraphDistSqr < closestDistSqr) {
                        graph = curGraph;
                        closestDistSqr = curGraphDistSqr;
                    }
                }        

                if (graph == null) {
                    resultCell = null;
                    resultPosition.x = x;
                    resultPosition.y = y;
                    resultPosition.z = z;
                    return NavmeshSampleResultType.InvalidNoNavmeshFound;
                }
                else {
                    Vector3 pos;
                    graph.GetClosestToHull(x, y, z, out resultCell, out pos);
                    resultPosition.x = pos.x;
                    resultPosition.y = pos.y;
                    resultPosition.z = pos.z;
                    return NavmeshSampleResultType.OutsideNavmesh;
                }
            }
            return graph.GetClosestCell(x, y, z, out resultPosition, out resultCell, layer, layerMask);
        }
       
        public static NavmeshSampleResultType TryGetClosestCell(float x, float y, float z, AgentProperties properties, out Cell cell, out Vector3 closestPoint, int layerMask = 1) {
            return TryGetClosestCell(x, y, z, properties, out cell, out closestPoint, layerMask: layerMask);
        }
        public static NavmeshSampleResultType TryGetClosestCell(float x, float y, float z, AgentProperties properties, out Cell cell, int layerMask = 1) {
            Vector3 closest;
            return TryGetClosestCell(x, y, z, properties, out cell, out closest, layerMask: layerMask);
        }
        public static NavmeshSampleResultType TryGetClosestCell(Vector3 pos, AgentProperties properties, out Cell cell, out Vector3 closestPoint, int layerMask = 1) {
            return TryGetClosestCell(pos.x, pos.y, pos.z, properties, out cell, out closestPoint, layerMask: layerMask);
        }
        public static NavmeshSampleResultType TryGetClosestCell(Vector3 pos, AgentProperties properties, out Cell cell, int layerMask = 1) {
            Vector3 closest;
            return TryGetClosestCell(pos.x, pos.y, pos.z, properties, out cell, out closest, layerMask: layerMask);
        }
        public static NavmeshSampleResultType TryGetClosestCell(PathFinderAgent agent, out Cell cell, out Vector3 closestPoint, int layerMask = 1) {
            return TryGetClosestCell(agent.positionVector3, agent.properties, out cell, out closestPoint, layerMask: layerMask);
        }
        public static NavmeshSampleResultType TryGetClosestCell(PathFinderAgent agent, out Cell cell, int layerMask = 1) {
            Vector3 closestPoint;
            return TryGetClosestCell(agent.positionVector3, agent.properties, out cell, out closestPoint, layerMask: layerMask);
        }

        public static NavmeshSampleResult TryGetClosestCell(Vector3 pos, AgentProperties properties, int layerMask = 1) {
            NavmeshSampleResult result;
            result.originX = pos.x;
            result.originY = pos.y;
            result.originZ = pos.z;
            result.type = TryGetClosestCell(pos.x, pos.y, pos.z, properties, out pos, out result.cell, layerMask: layerMask);
            result.positionX = pos.x;
            result.positionY = pos.y;
            result.positionZ = pos.z;
            return result;
        }
        public static NavmeshSampleResult TryGetClosestCell(PathFinderAgent agent, int layerMask = 1) {
            Vector3 pos = agent.positionVector3;
            NavmeshSampleResult result;
            result.originX = pos.x;
            result.originY = pos.y;
            result.originZ = pos.z;
            result.type = TryGetClosestCell(pos.x, pos.y, pos.z, agent.properties, out pos, out result.cell, layerMask: layerMask);
            result.positionX = pos.x;
            result.positionY = pos.y;
            result.positionZ = pos.z;
            return result;
        }


        /// <summary>
        ///try get nearest hull.
        ///return closest no navmesh hull point (that inside chunk where position are)
        ///hull is where border of navmesh are
        /// </summary>
        public static bool TryGetNearestHull(Vector3 pos, AgentProperties properties, out Cell cell, out Vector3 closestPoint) {
            Graph graph;
            if (TryGetGraph(ToChunkPosition(pos), properties, out graph)) {
                graph.GetClosestToHull(pos, out cell, out closestPoint);
                return cell != null;
            }
            else {
                cell = null;
                closestPoint = new Vector3();
                return false;
            }
        }
        public static bool TryGetNearestHull(Vector3 pos, AgentProperties properties, out Cell cell) {
            Vector3 closestPoint;
            return TryGetNearestHull(pos, properties, out cell, out closestPoint);
        }
        public static bool TryGetNearestHull(Vector3 pos, AgentProperties properties, out Vector3 closestPoint) {
            Cell cell;
            return TryGetNearestHull(pos, properties, out cell, out closestPoint);
        }
        public static bool TryGetNearestHull(PathFinderAgent agent, out Cell cell, out Vector3 closestPoint) {
            return TryGetNearestHull(agent.positionVector3, agent.properties, out cell, out closestPoint);
        }
        public static bool TryGetNearestHull(PathFinderAgent agent, out Cell cell) {
            Vector3 closestPoint;
            return TryGetNearestHull(agent, out cell, out closestPoint);
        }
        public static bool TryGetNearestHull(PathFinderAgent agent, out Vector3 closestPoint) {
            Cell cell;
            return TryGetNearestHull(agent, out cell, out closestPoint);
        }

        /// <summary>
        /// Very specific function. It do not check navmesh outlines and just return if there Cell below target point
        /// </summary>        
        public static bool TryGetCell(float x, float y, float z, AgentProperties properties, out Cell cell, out Vector3 closestPoint) {
            //slight offset in case when request exactly on 
            if (x % gridSize == 0) {
                x += 0.01f;
                //Debug.Log("x % gridSize == 0");
            }
            if (z % gridSize == 0) {
                z += 0.01f;
                //Debug.Log("z % gridSize == 0");
            }
            Graph graph;
            if (TryGetGraph(ToChunkPosition(x, z), properties, out graph)) {
                //Debuger_K.AddLine(pos, graph.chunk.centerV3,Color.cyan);
                return graph.GetCell(x, y, z, out cell, out closestPoint);
            }
            else {
                cell = null;
                closestPoint = new Vector3();
                return false;
            }
        }
        public static bool TryGetCell(float x, float y, float z, AgentProperties properties, out Cell cell) {
            Vector3 closestPoint;
            return TryGetCell(x, y, z, properties, out cell, out closestPoint);
        }
        public static bool TryGetCell(Vector3 pos, AgentProperties properties, out Cell cell, out Vector3 closestPoint) {
            return TryGetCell(pos.x, pos.y, pos.z, properties, out cell, out closestPoint);
        }
        public static bool TryGetCell(Vector3 pos, AgentProperties properties, out Cell cell) {
            Vector3 closestPoint;
            return TryGetCell(pos, properties, out cell, out closestPoint);
        }
        public static bool TryGetCell(PathFinderAgent agent, out Cell cell, out Vector3 closestPoint) {
            return TryGetCell(agent.positionVector3, agent.properties, out cell, out closestPoint);
        }
        public static bool TryGetCell(PathFinderAgent agent, out Cell cell) {
            Vector3 closestPoint;
            return TryGetCell(agent.positionVector3, agent.properties, out cell, out closestPoint);
        }

        //***************************QUEUE GRAPH***************************//
        //functions to order navmesh at some space
#region QUEUE GRAPH
        private static void QueueNavMeshTemplateToPopulation(GeneralXZData data) {
            NavMeshTemplateCreation template;

            //Debug.Log("Lock QueueNavMeshTemplateToPopulation");
            lock (currentWorkDictionary) {
                if (currentWorkDictionary.ContainsKey(data) || chunkData.ContainsKey(data))
                    return;

                template = new NavMeshTemplateCreation(chunkRange, CloneHashData(), data.gridPosition, data.properties);
                currentWorkDictionary[data] = template;
            }

            templateQueueStage1.Enqueue(template);
        }
        private static void QueueNavMeshTemplateToPopulation(XZPosInt pos, AgentProperties properties) {
            QueueNavMeshTemplateToPopulation(new GeneralXZData(pos, properties));
        }

        public static void QueueGraph(int x, int z, AgentProperties properties, int sizeX = 1, int sizeZ = 1) {
            lock (mainThreadChange) {
                if (threadState > 0) {
                    Debug.LogWarning("Navmesh Generation was blocked by pathfinder state: " + threadState);
                    return;
                }
            }

            if (sizeX <= 0 | sizeZ <= 0) {
                Debug.LogWarning("you trying to create navmesh with zero size. Which is not make any sence");
                return;
            }
            for (int _x = 0; _x < sizeX; _x++) {
                for (int _z = 0; _z < sizeZ; _z++) {
                    QueueNavMeshTemplateToPopulation(new GeneralXZData(x + _x, z + _z, properties));
                }
            }
        }
        public static void QueueGraph(Vector2 worldTopPosition, AgentProperties properties) {
            QueueNavMeshTemplateToPopulation(new GeneralXZData(ToChunkPosition(worldTopPosition.x, worldTopPosition.y), properties));
        }
        public static void QueueGraph(Vector3 worldPosition, AgentProperties properties) {  
            QueueNavMeshTemplateToPopulation(new GeneralXZData(ToChunkPosition(worldPosition.x, worldPosition.z), properties));
        }
        public static void QueueGraph(XZPosInt pos, AgentProperties properties) {
            QueueGraph(pos.x, pos.z, properties);
        }
        public static void QueueGraph(XZPosInt pos, XZPosInt size, AgentProperties properties) {
            QueueGraph(pos.x, pos.z, properties, size.x, size.z);
        }
        public static void QueueGraph(Bounds bounds, AgentProperties properties) {        
            XZPosInt min = ToChunkPosition(bounds.min);
            XZPosInt max = ToChunkPosition(bounds.max);
            QueueGraph(min.x, min.z, properties, max.x - min.x + 1, max.z - min.z + 1);
        }
        public static void QueueGraph(Vector2 startTop, Vector2 endTop, AgentProperties properties) {
            DDARasterization.DrawLineFixedMinusValues(startTop.x, startTop.y, endTop.x, endTop.y, gridSize, (int x, int y) => {
                QueueNavMeshTemplateToPopulation(new GeneralXZData(x, y, properties));
            });     
        }
        public static void QueueGraph(Vector3 start, Vector3 end, AgentProperties properties) {
            DDARasterization.DrawLineFixedMinusValues(start.x, start.z, end.x, end.z, gridSize, (int x, int y) => {
                QueueNavMeshTemplateToPopulation(new GeneralXZData(x, y, properties));
            });
        }


#endregion

        //***************************REMOVING GRAPHS***************************//
        //functions to order removing graphs at some space
#region REMOVING GRAPH
        /// <summary>
        /// function to remove graph at some space
        /// IMPORTANT: if bool createNewGraphAfter == true then pathfinder are also add this graph to generation queue after it was removed
        /// </summary>
        /// <param name="data">position and properties</param>
        /// <param name="createNewGraphAfter">do add graph after removing?</param>
        public static void RemoveGraph(GeneralXZData data, bool createNewGraphAfter = true) {
            destructQueue.Add(new NavMeshTemplateDestruction(data, createNewGraphAfter));
            Update();
        }

        /// <summary>
        /// function to remove graph at some space
        /// IMPORTANT: if bool createNewGraphAfter == true then pathfinder are also add this graph to generation queue after it was removed
        /// </summary>
        /// <param name="pos">position</param>
        /// <param name="properties">properties</param>
        /// <param name="createNewGraphAfter">do add graph after removing?</param>
        public static void RemoveGraph(XZPosInt pos, AgentProperties properties, bool createNewGraphAfter = true) {
            RemoveGraph(new GeneralXZData(pos, properties), createNewGraphAfter);
        }
        
        /// <summary>
        /// function to remove graph at some space with target size in chunks
        /// IMPORTANT: if bool createNewGraphAfter == true then pathfinder are also add this graph to generation queue after it was removed
        /// </summary>
        /// <param name="x">remove start X</param>
        /// <param name="z">remove start Z</param>
        /// <param name="properties">properties</param>
        /// <param name="sizeX">remove size X</param>
        /// <param name="sizeZ">remove size Z</param>
        /// <param name="createNewGraphAfter">do add graph after removing?</param>
        public static void RemoveGraph(int x, int z, AgentProperties properties, int sizeX = 1, int sizeZ = 1, bool createNewGraphAfter = true) {
            for (int _x = 0; _x < sizeX; _x++) {
                for (int _z = 0; _z < sizeZ; _z++) {
                    RemoveGraph(new XZPosInt(x + _x, z + _z), properties, createNewGraphAfter);
                }
            }
        }

        /// <summary>
        /// function to remove graph at space that include target bounds in world space
        /// IMPORTANT: if bool createNewGraphAfter == true then pathfinder are also add this graph to generation queue after it was removed
        /// </summary>
        /// <param name="bounds">target bounds in world space</param>
        /// <param name="properties">properties</param>
        /// <param name="createNewGraphAfter">do add graph after removing?</param>
        public static void RemoveGraph(Bounds bounds, AgentProperties properties, bool createNewGraphAfter = true) {
            float offset = properties.radius * properties.offsetMultiplier;
            Vector3 v3Offset = new Vector3(offset, 0, offset);
            XZPosInt min = ToChunkPosition(bounds.min - v3Offset);
            XZPosInt max = ToChunkPosition(bounds.max + v3Offset);
            VectorInt.Vector2Int size = new VectorInt.Vector2Int(Math.Max(1, max.x - min.x + 1), Math.Max(1, max.z - min.z + 1));
            RemoveGraph(min.x, min.z, properties, size.x, size.y, createNewGraphAfter);
        }

        /// <summary>
        /// function to remove graph at space that include multiple bounds in world space
        /// IMPORTANT: if bool createNewGraphAfter == true then pathfinder are also add this graph to generation queue after it was removed
        /// </summary>
        /// <param name="properties">properties</param>
        /// <param name="createNewGraphAfter">do add graph after removing?</param>
        /// <param name="bounds">target multiple bounds in world space</param>
        public static void RemoveGraph(AgentProperties properties, bool createNewGraphAfter = true, params Bounds[] bounds) {
            for (int i = 0; i < bounds.Length; i++) {
                RemoveGraph(bounds[i], properties, createNewGraphAfter);
            }
        }
#endregion
#endregion

#region Layers
        public static int StringToLayer(string layer) {
            if(string.IsNullOrEmpty(layer))
                throw new ArgumentException("PathFinder: target layer is null or empty");

            for (int i = 0; i < 32; i++) {
                if (settings.layers[i] == layer)
                    return i;
            }
            throw new ArgumentException("PathFinder: target layer string does not present in layers");
        }
#endregion

#region public values acessors
        public static bool areInit {
            get { return init; }
        }
       
        public static int gridLowest {
            get { return settings.gridLowest; }
        }
        public static int gridHighest {
            get { return settings.gridHighest; }
        }

        public static bool multithread {
            get { return settings.useMultithread; }
        }

        public static TerrainCollectorType terrainCollectionType {
            get { return settings.terrainCollectionType; }
        }
        public static ColliderCollectorType colliderCollectorType {
            get { return settings.colliderCollectionType; }
        }
#endregion

#region position convertation
        public static int ToGrid(float value) {
            return (int)Math.Floor(value / gridSize);
        }
        public static XZPosInt ToChunkPosition(float realX, float realZ) {
            return new XZPosInt(ToGrid(realX), ToGrid(realZ));
        }
        public static XZPosInt ToChunkPosition(Vector2 vector) {
            return ToChunkPosition(vector.x, vector.y);
        }
        public static XZPosInt ToChunkPosition(Vector3 vector) {
            return ToChunkPosition(vector.x, vector.z);
        }

        public static Bounds2DInt ToChunkPosition(float x1, float x2, float y1, float y2) {
            if (x2 < x1) {
                float temp = x1;
                x1 = x2;
                x2 = temp;
            }

            if (y2 < y1) {
                float temp = y1;
                y1 = y2;
                y2 = temp;
            }

            return ToChunkPositionPrivate(x1, x2, y1, y2);
        }


        public static Bounds2DInt ToChunkPosition(Bounds bounds) {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return ToChunkPositionPrivate(min.x, max.x, min.z, max.z);
        }

        private static Bounds2DInt ToChunkPositionPrivate(float minX, float maxX, float minY, float maxY) {
            return new Bounds2DInt(ToGrid(minX), ToGrid(minY), ToGrid(maxX), ToGrid(maxY));
        }
#endregion

#region global hash data
        public static void AddAreaHash(Area area) {
            //Debug.LogFormat("added {0}", area.name);
            lock (hashData)
                hashData.AddAreaHash(area);
        }

        public static void RemoveAreaHash(Area area) {
            lock (hashData)
                hashData.RemoveAreaHash(area);
        }

        //prefer cloning and use clone than this cause it lock
        public static int GetAreaHash(Area area, Passability passability) {
            lock (hashData)
                return hashData.GetAreaHash(area, passability);
        }
        public static void GetAreaByHash(short value, out Area area, out Passability passability) {
            lock (hashData)
                hashData.GetAreaByHash(value, out area, out passability);
        }

        public static AreaPassabilityHashData CloneHashData() {
            lock (hashData)
                return hashData.Clone();
        }
#endregion

#region serialization
#if UNITY_EDITOR
        //saving only for editor ATM cause it saved in scriptable object
        public static void SaveCurrentSceneData() {
            SceneNavmeshData data = sceneInstance.sceneNavmeshData;
            if (data == null) {
                string path = EditorUtility.SaveFilePanel("Save NavMesh", "Assets", SceneManager.GetActiveScene().name + ".asset", "asset");

                if (path == "")
                    return;

                path = FileUtil.GetProjectRelativePath(path);
                data = ScriptableObject.CreateInstance<SceneNavmeshData>();
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();
                Undo.RecordObject(sceneInstance, "Set SceneNavmeshData to NavMesh scene instance");
                sceneInstance.sceneNavmeshData = data;
                EditorUtility.SetDirty(sceneInstance);
            }

            HashSet<AgentProperties> allProperties = new HashSet<AgentProperties>();
            foreach (var key in chunkData.Keys) {
                allProperties.Add(key.properties);
            }      

            List<AgentProperties> properties = new List<AgentProperties>();
            List<SerializedNavmesh> navmesh = new List<SerializedNavmesh>();
            Dictionary<GameObject, int> gameObjectLibraryIDs = new Dictionary<GameObject, int>();

            int serializedGraphs = 0;
            int serializedCells = 0;
            foreach (var curProperties in allProperties) {
                properties.Add(curProperties);
                NavmeshSerializer serializer = new NavmeshSerializer(curProperties, chunkData, gameObjectLibraryIDs); 
                navmesh.Add(serializer.Serialize(VERSION));
                serializedGraphs += serializer.serializedGraphsCount;
                serializedCells += serializer.serializedCellsCount;
            }

            GameObject[] goLibraryArray = new GameObject[gameObjectLibraryIDs.Count];

            foreach (var pair in gameObjectLibraryIDs) {
                goLibraryArray[pair.Value] = pair.Key;
            }

            data.properties = properties;
            data.navmesh = navmesh;
            EditorUtility.SetDirty(data);

            Undo.RecordObject(sceneInstance, "Save Serialized Data you probably should not undo this");
            sceneInstance.gameObjectLibrary = goLibraryArray;

            Debug.LogFormat("Saved graphs {0}, cells{1}", serializedGraphs, serializedCells);
        }

        public static void ClearCurrenSceneData() {
            SceneNavmeshData data = sceneInstance.sceneNavmeshData;
            sceneInstance.gameObjectLibrary = new GameObject[0];
            if (data == null) {
                Debug.LogWarning("data == null");
                return;
            }

            if (data.properties != null) {
                if (data.properties.Count > 0) {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Cleared:");
                    for (int i = 0; i < data.properties.Count; i++) {
                        sb.AppendFormat("properties: {0}, graphs: {1}, cells: {2}", data.properties[i].name, data.navmesh[i].serializedGraphs.Length, data.navmesh[i].CountCells());
                    }
                    Debug.Log(sb);
                }
                else
                    Debug.Log("nothing to clear");
            }

            if (data.properties != null)
                data.properties.Clear();

            if (data.navmesh != null)
                data.navmesh.Clear();

            EditorUtility.SetDirty(data);
        }
#endif


        public static void LoadCurrentSceneData() {
            var sceneNavmeshData = sceneInstance.sceneNavmeshData;

            if (sceneNavmeshData == null) {
#if UNITY_EDITOR
                if (Debuger_K.doDebug)
                    Debug.LogWarning("No data to load");
#endif
                return;
            }

            AddPathfinderThreadDelegate(() => {
                GameObject[] gameObjectLibrary = sceneInstance.gameObjectLibrary;

                foreach (var go in gameObjectLibrary) {
                    AreaWorldMod awm = go.GetComponent<AreaWorldMod>();
                    if (awm != null)
                        awm.Init();
                }

                Cell[] globalCells = GenericPoolArray<Cell>.Take(64);
                List<Graph> allDeserializedGraphs = new List<Graph>();

                List<AgentProperties> properties = sceneNavmeshData.properties;
                List<SerializedNavmesh> navmesh = sceneNavmeshData.navmesh;

                lock (chunkData) {
                    for (int i = 0; i < properties.Count; i++) {
                        AgentProperties curProperties = properties[i];

                        if (curProperties == null)
                            continue;

                        SerializedNavmesh curNavmesh = navmesh[i];

                        //removing old graph if it exist
                        List<GeneralXZData> removeList = new List<GeneralXZData>();
                        foreach (var graph in chunkData.Values) {
                            if (graph.properties == curProperties)
                                removeList.Add(new GeneralXZData(graph.gridPosition, curProperties));
                        }
                        for (int removeIndex = 0; removeIndex < removeList.Count; removeIndex++) {
                            chunkData.Remove(removeList[removeIndex]);
                        }

                        NavmeshDeserializer deserializer = new NavmeshDeserializer(curNavmesh, curProperties, gameObjectLibrary);

                        Graph[] deserializedGraph;
                        deserializer.Deserialize(out deserializedGraph, ref globalCells);
                        allDeserializedGraphs.AddRange(deserializedGraph);

                        for (int graphIndex = 0; graphIndex < deserializedGraph.Length; graphIndex++) {
                            Graph graph = deserializedGraph[graphIndex];
                            ChunkData chunk = deserializedGraph[graphIndex].chunk;
                            XZPosInt chunkPos = chunk.xzPos;
                            int min = chunk.min;
                            int max = chunk.max;

                            YRangeInt curRange;
                            if (chunkRange.TryGetValue(chunkPos, out curRange)) {
                                if (min > curRange.min)
                                    min = curRange.min;
                                if (max < curRange.max)
                                    max = curRange.max;
                            }
                            chunkRange[chunkPos] = new YRangeInt(min, max);

                            graph.SetChunkData(new ChunkData(chunkPos, new YRangeInt(min, max)));
                            chunkData[new GeneralXZData(chunkPos, curProperties)] = graph;
                            graph.OnFinishGraph();
                        }

                        //connect chunks
                        foreach (var graph in deserializedGraph) {
                            for (int direction = 0; direction < 4; direction++) {
                                Graph neighbour;
                                if (TryGetGraphFrom(graph.gridPosition, (Directions)direction, curProperties, out neighbour)) {
                                    graph.SetNeighbour((Directions)direction, neighbour);
                                }
                            }
                        }
                    }
                }
                if (allDeserializedGraphs.Count > 0)
                    PathFinderData.SetCellIDManagerState(globalCells);

#if UNITY_EDITOR
                AddMainThreadDelegate(() => {
                    Debuger_K.ClearChunksDebug();
                    int loadedCells = 0;
                    foreach (var graph in allDeserializedGraphs) {
                        if (Debuger_K.doDebug)
                            graph.DebugGraph(true);
                        loadedCells += graph.cellsCount;
                    }                
                    if (Debuger_K.doDebug && allDeserializedGraphs.Count > 0)
                        Debug.LogWarningFormat("Loaded graphs {0} cells {1}", allDeserializedGraphs.Count, loadedCells);
                });
#endif
            });
                
            Update();
        }
#endregion

#region things to help debug stuff
#if UNITY_EDITOR
        static Vector3 ToV3(Vector2 pos) {
            return new Vector3(pos.x, 0, pos.y);
        }
        static Vector2 ToV2(Vector3 pos) {
            return new Vector2(pos.x, pos.z);
        }
#endif

        public static void CheckNavmeshIntegrity(StringBuilder sb) {
            sb.Length = 0;
            foreach (var pair in chunkData) {
                if (pair.Value == null) {
                    sb.AppendFormat("Graph at {0} is null", pair.Key);
                    continue;
                }

                Graph graph = pair.Value;

                Cell[] cellsArray;
                int cellsCount;

                graph.GetCells(out cellsArray, out cellsCount);

                for (int i = 0; i < cellsCount; i++) {
                    Cell cell = cellsArray[i];
                    if (cell == null) {
                        sb.AppendFormat("Graph at {0} contain null cell", pair.Key);
                        continue;
                    }

                    if (cell.graph == null) {
                        sb.AppendFormat("Graph at {0} contain null cell", pair.Key);
                        continue;
                    }

                    if (cell.pathContent != null) {
                        List<CellPathContentAbstract> cellPathContents = cell.pathContent;
                        for (int c_i = 0; c_i < cellPathContents.Count; c_i++) {
                            if (cellPathContents[c_i] == null) {
                                sb.AppendFormat("Cell {0} contain null CellPathContent", cell.globalID);
                            }
                        }
                    }

                    int cellConnectionsCount = cell.connectionsCount;
                    CellConnection[] cellConnections = cell.connections;

                    for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                        CellConnection connection = cellConnections[connectionIndex];
                        if (connection.type == CellConnectionType.Invalid) {
                            sb.AppendFormat("Cell {0} contain invalid connection", cell.globalID);
                        }
                    }
                }

                int cellMapResolution;
                Cell[] cellRichMap;
                IndexLengthInt[] cellRichMapLayout;
                Cell[] cellSimpleMap;
                IndexLengthInt[] cellSimpleMapLayout;
                bool cellSimpleMapHaveEmptySpots;

                graph.GetCellMapToSerialization(out cellMapResolution, out cellRichMap, out cellRichMapLayout, out cellSimpleMap, out cellSimpleMapLayout, out cellSimpleMapHaveEmptySpots);


            }
        }
        #endregion
    }

}