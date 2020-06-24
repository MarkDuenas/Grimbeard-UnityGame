using K_PathFinder;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder.Samples {
    [System.Serializable]
    public struct TerrainNoiseValue {
        public float scale;
        public float weight;
        public AnimationCurve distribution;
    }

    public class Example14 : MonoBehaviour {
        //some consts for terrain generation
        const int HEIGHT_MAP_RESULUTION = 33;
        const int ALPHA_MAP_RESOLUTION = 33;

        [Header("PathFinder stuff")]
        public AgentProperties properties;//VERY IMPORTANT VALUE. this value used as key to queue NavMesh. also it's used to get generated navmesh. this is way to tell how navmesh should be generated
        [Area] public int grassArea, sandArea, cliffArea, rockyArea; //area indexes of splat prototypes

        [Header("Simple agent")]
        public Camera cameraToMoveAround;       //camera for raycasts
        public float moveSpeed = 3f;            //moving speed of agent
        private CharacterController controler;  //character controler to move around
        private PathFinderAgent agent;          //agent to queue path and recieve it

        [Header("Procedural terrain")]
        public float terrainDestroyRange = 75f; //range of terrain dissapear
        public float terrainAppearRange = 55f;  //range of terrain appear

        public float terrainChunkSize = 25f;   //size of terrain chunk
        public float terrainChunkHeight = 25f; //height of terrain chunk
        public TerrainNoiseValue[] noiseData;  //array of noise settings to generate heightmap
        public Texture2D[] cliffSandGrassRocky;//textures of terrain. it should appear as 0 = cliff, 1 = sand, 2 = grass, 3 = rocks in this order
        public AnimationCurve cliffAngle;      //normalized angle where cliff texture appear
        public AnimationCurve rockyAngle;      //normalized angle where rocky texture appear
        public AnimationCurve sandHeight;      //normalized height where sand texture appear
        public GameObject treePrefab;          //prefab for tree
        public int treesPerChunk = 2;          //how much trees per terrain chunk

        private Dictionary<VectorInt.Vector2Int, Terrain> terrainDictionary = new Dictionary<VectorInt.Vector2Int, Terrain>(); //dictionary of terrain
        private List<VectorInt.Vector2Int> removeList = new List<VectorInt.Vector2Int>(); //list to remove values from terrain dictionary

#if UNITY_2018_3_OR_NEWER
        private TerrainLayer[] splatLayers; //prefabed terrain layers
#else
        private SplatPrototype[] splatPrototypes; //prefabed splat prototypes
#endif
        private TreePrototype[] treePrototypes;   //prefabes terrain prototypes
        private GameObject terrains, pool;        //gameobjects that used as folders in scene

        void Start() {
            #region stuff to simplify terrain generation
            terrains = new GameObject("Terrains");
            pool = new GameObject("Pool");

#if UNITY_2018_3_OR_NEWER
            splatLayers = new TerrainLayer[4];
            for (int i = 0; i < 4; i++) { 
                splatLayers[i] = new TerrainLayer() {
                    diffuseTexture = cliffSandGrassRocky[i],
                    tileSize = new Vector2(5, 5)
                };
            }
#else
            splatPrototypes = new SplatPrototype[4];
            for (int i = 0; i < 4; i++) {
                SplatPrototype sp = new SplatPrototype();
                sp.texture = cliffSandGrassRocky[i];
                sp.tileSize = new Vector2(5, 5);
                splatPrototypes[i] = sp;
            }
#endif

            treePrototypes = new TreePrototype[] { new TreePrototype() { prefab = treePrefab, bendFactor = 1f } };
#endregion

            agent = gameObject.AddComponent<PathFinderAgent>();//creating simple agent to move around
            agent.properties = properties;//assign to that aggent sellected properties
            controler = GetComponent<CharacterController>();//get controler to move around

            //setup some basic userful delegate that used when agent get new path
            agent.SetRecievePathDelegate(path => {
                //setup simple message so agent tell if path it get is not valid in some way
                if (path.pathType != PathResultType.Valid)
                    Debug.LogWarning(path.pathType);
            });
        }

        private void Update() {
            Vector2 transformPosition2D = new Vector2(transform.position.x, transform.position.z);
            int thisPositionX = Mathf.RoundToInt(transformPosition2D.x / terrainChunkSize);
            int thisPositionZ = Mathf.RoundToInt(transformPosition2D.y / terrainChunkSize);

#region remove unused terrain
            foreach (var pos in terrainDictionary.Keys) {
                //if distance from current transform is too big then add this to remove list
                if (new Vector2(
                    (pos.x * terrainChunkSize) + (terrainChunkSize * 0.5f) - transformPosition2D.x,
                    (pos.y * terrainChunkSize) + (terrainChunkSize * 0.5f) - transformPosition2D.y).magnitude > terrainDestroyRange)
                    removeList.Add(pos);
            }
            foreach (var pos in removeList) { RemoveTerrain(pos.x, pos.y); }//remove terrains from current remove list
            removeList.Clear();
#endregion

            //this region contain implementation of adding to terrain information how textures relate to PathFinder Area
#region generate terrain in proximity
            int checkDistance = (int)(terrainAppearRange / terrainChunkSize);
            for (int x = thisPositionX - checkDistance; x < thisPositionX + checkDistance + 1; x++) {
                for (int z = thisPositionZ - checkDistance; z < thisPositionZ + checkDistance + 1; z++) {
                    //get current top position
                    Vector2 position = new Vector2(
                        (x * terrainChunkSize) + (terrainChunkSize * 0.5f),
                        (z * terrainChunkSize) + (terrainChunkSize * 0.5f));

                    //if position close enough and it is not already generated
                    if ((position - transformPosition2D).magnitude <= terrainAppearRange && terrainDictionary.ContainsKey(new VectorInt.Vector2Int(x, z)) == false) {
                        GameObject terrainGameObject;
                        bool isNewGameObject;
                        //generate some terrain. also give gameobject of terrain
                        //and also tell if it is generated or taken from pool
                        Generate(x, z, out terrainGameObject, out isNewGameObject);

                        //adding navigation components to terrain
                        if (isNewGameObject) { //else it already existed and just taken from pool
                            //adding settings to new terrain
                            TerrainNavmeshSettings settings = terrainGameObject.AddComponent<TerrainNavmeshSettings>();
                            settings.SetArea(0, cliffArea);//index 0 is Cliff in terrain generation
                            settings.SetArea(1, sandArea); //index 1 is Sand in terrain generation
                            settings.SetArea(2, grassArea);//index 2 is Grass in terrain generation
                            settings.SetArea(3, rockyArea);//index 3 is Rocky area in terrain generation
                        }
                    }
                }
            };
#endregion

            //queue some NavMesh around current transform
            PathFinder.QueueGraph(new Bounds(transform.position, new Vector3(20, 0, 20)), properties);

            //raycasting
            Ray ray = cameraToMoveAround.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Input.GetMouseButtonDown(0) && Physics.Raycast(ray, out hit, 10000f, agent.properties.includedLayers))  //when button is pressed and ray hit something
                agent.SetGoalMoveHere(hit.point, applyRaycast: true);//order path to that point    

            //check if there some move points in current path
            if (agent.haveNextNode) {
                //remove next node if closer than radius in top projection. there is other variants of this function
                agent.RemoveNextNodeIfCloserThanRadiusVector2();

                //if next point still exist then we move towards it
                if (agent.haveNextNode) {
                    Vector2 moveDirection = agent.nextNodeDirectionVector2.normalized;
                    controler.SimpleMove(new Vector3(moveDirection.x, 0, moveDirection.y) * moveSpeed);
                }
            }
        }

#region terrain generation
        private void Generate(int chunkX, int chunkZ, out GameObject terrainGameObject, out bool isNewGameObject) {
            //get free terrain from pool
            Terrain terrain;
            TerrainData terrainData;
            GetClearTerrain(out terrainGameObject, out terrain, out terrainData, out isNewGameObject);

#region generate heightmap
            //precalculate weights for noise data nad make it normalized
            float[] normalizedWeights = new float[noiseData.Length];
            float weightsSum = 0f;
            for (int i = 0; i < noiseData.Length; i++) { weightsSum += noiseData[i].weight; }
            for (int i = 0; i < noiseData.Length; i++) { normalizedWeights[i] = noiseData[i].weight / weightsSum; }

            float[,] heightMap = new float[HEIGHT_MAP_RESULUTION, HEIGHT_MAP_RESULUTION];
            for (int heightMapX = 0; heightMapX < HEIGHT_MAP_RESULUTION; heightMapX++) {
                for (int heightMapZ = 0; heightMapZ < HEIGHT_MAP_RESULUTION; heightMapZ++) {
                    float normalizedChunkX = (float)heightMapX / (HEIGHT_MAP_RESULUTION - 1);
                    float normalizedChunkZ = (float)heightMapZ / (HEIGHT_MAP_RESULUTION - 1);

                    float noise = 0f;
                    //sample noise for every noise data
                    for (int i = 0; i < noiseData.Length; i++) {
                        var value = noiseData[i];

                        noise +=
                            value.distribution.Evaluate(
                                Mathf.PerlinNoise(
                                    (chunkX * value.scale) + (normalizedChunkX * value.scale),
                                    (chunkZ * value.scale) + (normalizedChunkZ * value.scale))
                                ) * normalizedWeights[i];
                    }

                    heightMap[heightMapZ, heightMapX] = noise;
                }
            }
            //set height map to terrain
            terrainData.SetHeights(0, 0, heightMap);
#endregion

#region generate texture
            float[,,] alphaMap = new float[ALPHA_MAP_RESOLUTION, ALPHA_MAP_RESOLUTION, 4];

            for (int x = 0; x < ALPHA_MAP_RESOLUTION; x++) {
                for (int z = 0; z < ALPHA_MAP_RESOLUTION; z++) {
                    float normalizedChunkX = (float)x / (ALPHA_MAP_RESOLUTION - 1);
                    float normalizedChunkZ = (float)z / (ALPHA_MAP_RESOLUTION - 1);
                    float steepness = terrainData.GetSteepness(normalizedChunkX, normalizedChunkZ) / 90f;
                    float height = terrainData.GetInterpolatedHeight(normalizedChunkX, normalizedChunkZ);

                    float cliff = cliffAngle.Evaluate(steepness);
                    float rock = rockyAngle.Evaluate(steepness);
                    float sand = sandHeight.Evaluate(height);
                    float grass = Mathf.Clamp01(1f - (cliff + rock + sand));//grass if there is nothing else
                    float sum = cliff + rock + sand + grass;

                    //normalize map
                    alphaMap[z, x, 0] = cliff / sum;
                    alphaMap[z, x, 1] = sand / sum;
                    alphaMap[z, x, 2] = grass / sum;
                    alphaMap[z, x, 3] = rock / sum;

                }
            }
            terrainData.SetAlphamaps(0, 0, alphaMap);
#endregion

#region adding trees
            List<TreeInstance> treeInstances = new List<TreeInstance>();
            System.Random random = new System.Random(new VectorInt.Vector2Int(chunkX, chunkZ).GetHashCode());
            for (int i = 0; i < treesPerChunk; i++) {
                float treeX = random.Next(0, 1000) / 1000f;
                float treeZ = random.Next(0, 1000) / 1000f;

                if (terrainData.GetSteepness(treeX, treeZ) < 45f) {
                    treeInstances.Add(new TreeInstance() {
                        prototypeIndex = 0,
                        heightScale = 1f,
                        widthScale = 1f,
                        position = new Vector3(treeX, terrainData.GetInterpolatedHeight(treeX, treeZ) / terrainChunkHeight, treeZ),
                        color = Color.white,
                        lightmapColor = Color.white,
                        rotation = (random.Next(0, 1000) / 1000f) * Mathf.PI
                    });
                }
            }
            terrainData.treeInstances = treeInstances.ToArray();
#endregion

            terrainGameObject.name = string.Format("x:{0} z:{1}", chunkX, chunkZ);
            terrainGameObject.transform.position = new Vector3(chunkX * terrainChunkSize, 0, chunkZ * terrainChunkSize);
            terrainGameObject.transform.SetParent(terrains.transform);

            AssignTerrainNeighbours(chunkX, chunkZ);
            AssignTerrainNeighbours(chunkX + 1, chunkZ);
            AssignTerrainNeighbours(chunkX - 1, chunkZ);
            AssignTerrainNeighbours(chunkX, chunkZ + 1);
            AssignTerrainNeighbours(chunkX, chunkZ - 1);

            terrainDictionary.Add(new VectorInt.Vector2Int(chunkX, chunkZ), terrain);
        }

        void AssignTerrainNeighbours(int x, int z) {
            Terrain terrain;
            if (terrainDictionary.TryGetValue(new VectorInt.Vector2Int(x, z), out terrain)) {
                Terrain left, top, right, bottom;
                terrainDictionary.TryGetValue(new VectorInt.Vector2Int(x - 1, z), out left);
                terrainDictionary.TryGetValue(new VectorInt.Vector2Int(x, z + 1), out top);
                terrainDictionary.TryGetValue(new VectorInt.Vector2Int(x + 1, z), out right);
                terrainDictionary.TryGetValue(new VectorInt.Vector2Int(x, z - 1), out bottom);
                terrain.SetNeighbors(left, top, right, bottom);
                terrain.Flush();
            }
        }

        void GetClearTerrain(out GameObject terrainGameObject, out Terrain terrain, out TerrainData terrainData, out bool isNewGameObject) {
            if (pool.transform.childCount > 0) {
                terrainGameObject = pool.transform.GetChild(0).gameObject;
                terrainGameObject.transform.SetParent(null);
                terrain = terrainGameObject.GetComponent<Terrain>();
                terrainData = terrain.terrainData;
                terrainGameObject.SetActive(true);
                isNewGameObject = false;
            }
            else {
                terrainGameObject = new GameObject();
                terrainGameObject.isStatic = true;
                terrain = terrainGameObject.AddComponent<Terrain>();
                terrainData = new TerrainData();
                terrain.terrainData = terrainData;
                terrainData.heightmapResolution = HEIGHT_MAP_RESULUTION;
                terrainData.size = new Vector3(terrainChunkSize, terrainChunkHeight, terrainChunkSize);
                terrainData.treePrototypes = treePrototypes;

#if UNITY_2018_3_OR_NEWER
                terrainData.terrainLayers = splatLayers;
#else
                terrainData.splatPrototypes = splatPrototypes;
#endif

                terrainData.alphamapResolution = ALPHA_MAP_RESOLUTION;

                TerrainCollider terrainCollider = terrainGameObject.AddComponent<TerrainCollider>();
                terrainCollider.terrainData = terrainData;
                isNewGameObject = true;
            }
        }

        void RemoveTerrain(int x, int z) {
            Terrain terrain;
            if (terrainDictionary.TryGetValue(new VectorInt.Vector2Int(x, z), out terrain)) {
                terrainDictionary.Remove(new VectorInt.Vector2Int(x, z));

                terrain.transform.SetParent(pool.transform);
                terrain.gameObject.SetActive(false);

                terrain.SetNeighbors(null, null, null, null);
                AssignTerrainNeighbours(x + 1, z);
                AssignTerrainNeighbours(x - 1, z);
                AssignTerrainNeighbours(x, z + 1);
                AssignTerrainNeighbours(x, z - 1);
            }
        }
#endregion
    }
}
