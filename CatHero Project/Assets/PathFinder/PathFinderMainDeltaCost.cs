using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
using UnityEditor;
#endif

namespace K_PathFinder {
    //value for library
    public class DeltaCostValue {
        public int pathfinderSampleID;                   //-1 by default. if it is -1 then it is not registered on navmesh yet
        public readonly AgentProperties agentProperties; //which agent properties see this delta cost
        public readonly int group;                       //what group this delta cost represent
        public readonly int layerMask;                   //layer mask for navmesh
        public Vector3 position;                         //position of this value
        public AnimationCurve costMultiplier;            //normalized and take 0f - 1f range. result value multiplied by this curve
        public float maxNavmeshCost;                     //how far this value affect navmesh       


#if UNITY_EDITOR
        public bool debugMode = false;
#endif

        
        /// <summary>
        /// constructor for DeltaCostValue this value change cost for particular navmesh queries
        /// </summary>
        /// <param name="AgentProperties">which agent properties see this delta cost</param>
        /// <param name="Position">position of this value</param>
        /// <param name="Group">what group this delta cost represent</param>
        /// <param name="MaxNavmeshCost">how far this value affect navmesh</param>
        /// <param name="CostMultiplier">cost modifyer. this value multiply cost of cell. if number positive then move cost increased. if negative then decreased</param>
        /// <param name="MaxDistanceFromNavMesh">max distance from navMesh. if nearest navmesh point further than this then it is not on navmesh</param>
        public DeltaCostValue(AgentProperties agentProperties, Vector3 position, int group, float maxNavmeshCost, AnimationCurve costMultiplier, int layerMask = 1) {
            if (group >= 32)
                throw new System.ArgumentException("PathFinder: Group of created DeltaCostValue cannto be larger than 31", "group");

            this.agentProperties = agentProperties;
            this.position = position;
            this.group = group;
            this.maxNavmeshCost = maxNavmeshCost;
            this.costMultiplier = costMultiplier;
            this.layerMask = layerMask;
            this.pathfinderSampleID = -1;            
        }
        
        public void SetValues(Vector3 position, float maxNavmeshCost) {
            lock (this) {
                this.position = position;
                this.maxNavmeshCost = maxNavmeshCost;
            }
        }
    }

    
    //this part of code are responcible for holding delta cost of cells
    public static partial class PathFinder {
        //this part of code generate delta cost information that applyed to specific queries
        public static float[] cellDeltaCostArray;
        public static int deltaCostMaxGroupCount;
        private static List<DeltaCostValue> deltaCostValues = new List<DeltaCostValue>();
        private static Queue<DeltaCostValueInstructions> deltaCostInstructions = new Queue<DeltaCostValueInstructions>();
        
        struct DeltaCostValueInstructions {
            public enum InstructionType {
                Add, Remove
            }
            public DeltaCostValue target;
            public InstructionType instruction;

            public DeltaCostValueInstructions(DeltaCostValue target, InstructionType instruction) {
                this.target = target;
                this.instruction = instruction;
            }
        }
        
        static int RecalculateDeltaCostBeforePositionUpdate() {     
            int resut;
            lock (deltaCostInstructions) {
                resut = deltaCostInstructions.Count;
                while (deltaCostInstructions.Count > 0) {                
                    var instruction = deltaCostInstructions.Dequeue();
                    var target = instruction.target;

                    switch (instruction.instruction) {
                        case DeltaCostValueInstructions.InstructionType.Add:
                            if (target.pathfinderSampleID != -1)
                                Debug.LogWarning("pathfinder: target cost modifyer already have assigned sample id before it added to library. is it some sort of serialization issue or it is added and removed in different threads?");

                            deltaCostValues.Add(target);
                            target.pathfinderSampleID = RegisterNavmeshSample(target.agentProperties);                
                            break;
                        case DeltaCostValueInstructions.InstructionType.Remove:
                            if (target.pathfinderSampleID == -1)
                                Debug.LogWarning("pathfinder: target cost modifyer already have empty sample id before it removed from library. is it some sort of serialization issue or it is are added and removed in different threads?");

                            deltaCostValues.Remove(target);
                            target.pathfinderSampleID = -1;
                            break;
                        default:
                            throw new System.Exception("pathfinder: this instruction type is not implemented");                        
                    }
                }
            }

            for (int i = 0; i < deltaCostValues.Count; i++) {
                var val = deltaCostValues[i];
                lock (val) {
                    navmeshPositionRequests[val.pathfinderSampleID].Set(val.position, val.layerMask, true);
                }
            }
            return resut;
        }
        
        /// <summary>
        ///calculate delta cost of cell groups
        /// </summary>
        /// <returns>amount of recalculated deltas</returns>
        static int RecalculateDeltaCostAfterPositionUpdate() {
            try {
                deltaCostMaxGroupCount = 0;

                int WorkCount = deltaCostValues.Count;
                if (WorkCount == 0)               
                    return 0;                

                int maxRegisteredID = PathFinderData.maxRegisteredCellID;
                Cell[] globalCells = PathFinderData.cells;

                if (cellDeltaCostArray != null)
                    GenericPoolArray<float>.ReturnToPool(ref cellDeltaCostArray);
                               
                for (int valueIndex = 0; valueIndex < deltaCostValues.Count; valueIndex++) {
                    var deltaCostValue = deltaCostValues[valueIndex];
                    if (deltaCostMaxGroupCount < deltaCostValue.group)
                        deltaCostMaxGroupCount = deltaCostValue.group;
                }
                deltaCostMaxGroupCount += 1;

                cellDeltaCostArray = GenericPoolArray<float>.Take((deltaCostMaxGroupCount * maxRegisteredID) + deltaCostMaxGroupCount, defaultValue: 0f);
                HeapFloatFirstLowest<int> modsHeap = GenericPool<HeapFloatFirstLowest<int>>.Take();
                modsHeap.TakeFromPoolAllocatedData(128);

                //cells already used in updating
                bool[] usedCells = GenericPoolArray<bool>.Take(PathFinderData.maxRegisteredCellID + 1, defaultValue: false);
                //used IDs so bool array reseted faster
                int[] usedCellsIDs = GenericPoolArray<int>.Take(256);
                int usedCellsIDsCount;       

                for (int valueIndex = 0; valueIndex < deltaCostValues.Count; valueIndex++) {                   
                    var deltaCostValue = deltaCostValues[valueIndex];
                    AgentProperties properties = deltaCostValue.agentProperties;

                    var sample = navmeshPositionResults[deltaCostValue.pathfinderSampleID];
                    if (sample.cellGlobalID == -1)
                        continue;

                    int curGroup = deltaCostValue.group;
                    Vector3 deltaCostPosition = sample.position;
                    float maxCost = deltaCostValue.maxNavmeshCost;
                    AnimationCurve costMultiplier = deltaCostValue.costMultiplier;

                    //adding cell initial connections
                    usedCellsIDsCount = 0;
                    usedCells[sample.cellGlobalID] = true;
                    usedCellsIDs[usedCellsIDsCount++] = sample.cellGlobalID;
                    cellDeltaCostArray[(sample.cellGlobalID * deltaCostMaxGroupCount) + curGroup] += maxCost * costMultiplier.Evaluate(0f);
                    //Debuger_K.ClearLabels();
                    //Debuger_K.AddLabel(globalCells[sample.cellGlobalID].centerVector3, cellDeltaCostArray[(sample.cellGlobalID * deltaCostMaxGroupCount) + curGroup]);

                    int cellConnectionsCount = globalCells[sample.cellGlobalID].connectionsCount;
                    CellConnection[] cellConnections = globalCells[sample.cellGlobalID].connections;

                    int layerMask = deltaCostValue.layerMask;

                    for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                        CellConnection connection = cellConnections[connectionIndex];
                        float cost = connection.Cost(deltaCostPosition, properties, false);
                        if (cost <= maxCost)
                            modsHeap.Add(connection.connection, cost);
                    }

                    while (true) {
                        if (modsHeap.count == 0)
                            break;

                        float curCellMoveCost;
                        int curCellID = modsHeap.RemoveFirst(out curCellMoveCost);

                        //add current cell to used cells
                     
                        usedCells[curCellID] = true;
                        if (usedCellsIDs.Length == usedCellsIDsCount)
                            GenericPoolArray<int>.IncreaseSize(ref usedCellsIDs);
                        usedCellsIDs[usedCellsIDsCount++] = curCellID;

                        //skip cells that cannot be used
                        if ((1 << globalCells[curCellID].bitMaskLayer & layerMask) == 0)
                            continue;

                        //add delta cost of current cell

                        //cellDeltaCostArray[(curCellID * deltaCostMaxGroupCount) + curGroup] += (maxCost - curCellMoveCost);
                        cellDeltaCostArray[(curCellID * deltaCostMaxGroupCount) + curGroup] += maxCost * costMultiplier.Evaluate(curCellMoveCost / maxCost);
                        //Debuger_K.AddLabel(globalCells[curCellID].centerVector3, (curCellID * deltaCostMaxGroupCount) + curGroup + " : " + cellDeltaCostArray[(curCellID * deltaCostMaxGroupCount) + curGroup]);

                        cellConnections = globalCells[curCellID].connections;
                        cellConnectionsCount = globalCells[curCellID].connectionsCount;                

                        for (int connectionIndex = 0; connectionIndex < cellConnectionsCount; connectionIndex++) {
                            if (usedCells[cellConnections[connectionIndex].connection])
                                continue;

                            float cost = curCellMoveCost + cellConnections[connectionIndex].Cost(properties, false);
                            if (cost <= maxCost)
                                modsHeap.Add(cellConnections[connectionIndex].connection, cost);
                        }
                    }

                    //iteration over used cells and reseting bools
                    for (int i = 0; i < usedCellsIDsCount; i++) {
                        usedCells[usedCellsIDs[i]] = false;
                    }
                    usedCellsIDsCount = 0;
                }
                
                modsHeap.ReturnToPoolAllocatedData();
                GenericPool<HeapFloatFirstLowest<int>>.ReturnToPool(ref modsHeap);
                GenericPoolArray<bool>.ReturnToPool(ref usedCells);
                GenericPoolArray<int>.ReturnToPool(ref usedCellsIDs);
                
#if UNITY_EDITOR
                if (Debuger_K.settings != null && Debuger_K.settings.showDeltaCost) {
                    DebugDeltaCost(Debuger_K.debugSetDeltaCost);
                }
#endif

                return WorkCount;
            }
            catch (System.Exception e) {
                Debug.LogErrorFormat("error occured in PathFinder while updating delta cost: {0}", e);
                throw;
            }
        }

        private static void ClearDeltaCost() {
            deltaCostValues.Clear();
            if (cellDeltaCostArray != null) 
                GenericPoolArray<float>.ReturnToPool(ref cellDeltaCostArray);            
        }

        public static void AddDeltaCostValue(DeltaCostValue value) {
            lock (deltaCostInstructions) 
                deltaCostInstructions.Enqueue(new DeltaCostValueInstructions(value, DeltaCostValueInstructions.InstructionType.Add));            
        }

        public static void RemoveDeltaCostValue(DeltaCostValue value) {
            lock (deltaCostValues) 
                deltaCostInstructions.Enqueue(new DeltaCostValueInstructions(value, DeltaCostValueInstructions.InstructionType.Remove));            
        }

#if UNITY_EDITOR
        public static void DebugDeltaCost(DebugSet set) {
            set.Clear();
            var globalCells = PathFinderData.cells;
            int cellsCount = PathFinderData.maxRegisteredCellID;
            int maxGroupCount = deltaCostMaxGroupCount;

            string format = "[{0}] {1}\n";
           
            for (int curCellID = 0; curCellID < cellsCount; curCellID++) { 
                bool any = false;
                for (int curGroup = 0; curGroup < maxGroupCount; curGroup++) {
                    float val = cellDeltaCostArray[(curCellID * deltaCostMaxGroupCount) + curGroup];
                    if(val != 0f) {
                        sb.AppendFormat(format, curGroup, val);
                        any = true;
                    }
                }

                if (any) {
                    set.AddLabel(globalCells[curCellID].centerVector3, sb.ToString());
                    sb.Length = 0;
                }
            }
        }
#endif
    }
}