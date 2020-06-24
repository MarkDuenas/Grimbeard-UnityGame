using K_PathFinder.Graphs;
using K_PathFinder.Pool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace K_PathFinder {
    public class NavMeshPathQueryTargetArea : NavMeshPathQueryAbstract {
        float maxSearchCost;
        Area tagretArea;

        public NavMeshPathQueryTargetArea(AgentProperties properties, IPathOwner pathOwner) : base(properties, pathOwner) { }

        public void QueueWork(Vector3 start, float maxSearchCost, Area area, int layerMask = 1, int costModifyerMask = 0, bool updatePathFinder = true) {
            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;
                this.costModifyerMask = costModifyerMask;
                this.startPosition = start;
                this.maxSearchCost = maxSearchCost;
                this.tagretArea = area;
                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        public void QueueWork(Vector3 start, float maxSearchCost, int area, int layerMask = 1, int costModifyerMask = 0, bool updatePathFinder = true) {
            QueueWork(start, maxSearchCost, PathFinder.GetArea(area), layerMask, costModifyerMask, updatePathFinder);
        }

        public override void OnBeforeNavmeshPositionUpdate() {
            pathStartSample = PathFinder.RegisterNavmeshSample(properties, startPosition, layerMask, true);
        }

        public override void PerformWork(object context) {
            notThreadSafeResult = Path.PoolRent();
            Cell[] globalCells = PathFinderData.cells;

            NavmeshSampleResult_Internal startSample = PathFinder.UnregisterNavmeshSampleAndReturnResult(pathStartSample);
            if (startSample.type == NavmeshSampleResultType.InvalidNoNavmeshFound | startSample.cellGlobalID == -1) {
                notThreadSafeResult.Init(pathOwner, PathResultType.InvalidAgentOutsideNavmesh);
                Finish();
                return;
            }

            //snap to navmesh if outside and snap
            if (startSample.type != NavmeshSampleResultType.InsideNavmesh)
                startPosition = startSample.position;

            Cell startCell = globalCells[startSample.cellGlobalID];

            //check if there result on first cell
            if (startCell.area == tagretArea) {
                notThreadSafeResult.AddMove(startPosition, (MoveState)(int)startCell.passability);
                notThreadSafeResult.SetCurrentIndex(1);
                notThreadSafeResult.Init(pathOwner, PathResultType.Valid);
                Finish();
                return;
            }  

            //search with predicate
            int cellPathCount;
            CellConnection[] cellPath;
            PathResultType pathResult;
            float pathAproxCost;
            if (SearchGenericWithPredicate(layerMask, costModifyerMask, maxExecutionTimeInMilliseconds, properties, ignoreCrouchCost, startCell, startPosition, CellSearchDelegate, maxSearchCost, out cellPath, out cellPathCount, out pathResult, out pathAproxCost)) {
                var lastConnection = cellPath[cellPathCount - 1];
                targetPosition = lastConnection.intersection;
                                
                //simplify target path
                GenericFunnel(notThreadSafeResult, cellPath, cellPathCount, startCell.passability, startPosition, globalCells[lastConnection.connection].passability, targetPosition);
                notThreadSafeResult.pathNavmeshCost = pathAproxCost;

                GenericPoolArray<CellConnection>.ReturnToPool(ref cellPath); //return to pool array that was allocated while performing Search()                  
            }
            notThreadSafeResult.Init(pathOwner, pathResult);
            Finish();
        }

        private bool CellSearchDelegate(Cell cell) {
            return cell.area == tagretArea;
        }
    }
}