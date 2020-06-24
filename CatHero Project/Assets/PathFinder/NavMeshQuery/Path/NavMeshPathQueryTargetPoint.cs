using K_PathFinder.Graphs;
using K_PathFinder.PFTools;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace K_PathFinder {
    public class NavMeshPathQueryTargetPoint<ContentValueType> : NavMeshPathQueryAbstract where ContentValueType : ICellContentValue {
        Predicate<ContentValueType> predicate = null;
        public ContentValueType resultPoint;
        public float maxSearchCost;

        public NavMeshPathQueryTargetPoint(AgentProperties properties, IPathOwner pathOwner) : base(properties, pathOwner) { }

        public void QueueWork(Vector3 start, float maxSearchCost, Predicate<ContentValueType> predicate, int layerMask = 1, int costModifyerMask = 0, bool updatePathFinder = true) {
            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;
                this.costModifyerMask = costModifyerMask;
                this.startPosition = start;
                this.predicate = predicate;
                this.maxSearchCost = maxSearchCost;
                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        } 
        
        public void QueueWork(Vector3 start, float maxSearchCost, int layerMask = 1, int costModifyerMask = 0, bool updatePathFinder = true) {
            QueueWork(start, maxSearchCost, null, layerMask, costModifyerMask, updatePathFinder);
        }

        public override void OnBeforeNavmeshPositionUpdate() {
            pathStartSample = PathFinder.RegisterNavmeshSample(properties, startPosition, layerMask, true);
        }

        public override void PerformWork(object context) {
            Path path = Path.PoolRent();

            NavmeshSampleResult_Internal startSample = PathFinder.UnregisterNavmeshSampleAndReturnResult(pathStartSample);

            if (startSample.type == NavmeshSampleResultType.InvalidNoNavmeshFound | startSample.cellGlobalID == -1) {
                notThreadSafeResult.Init(pathOwner, PathResultType.InvalidAgentOutsideNavmesh);
                Finish();
                return;
            }
            
            if (startSample.type != NavmeshSampleResultType.InsideNavmesh)
                startPosition = startSample.position;
            
            Cell startCell = PathFinderData.cells[startSample.cellGlobalID];

            //check if there result on first cell
            foreach (var content in startCell.cellContentValues) {    
                if (content is ContentValueType) {
                    ContentValueType casted = (ContentValueType)content;
                    bool valid = false;
                    if (predicate != null) {
                        if (predicate((ContentValueType)content))
                            valid = true;
                    }
                    else valid = true;

                    if (valid) {
                        path.AddMove(casted.position, (MoveState)(int)startCell.passability);
                        path.SetCurrentIndex(1);
                        notThreadSafeResult.Init(pathOwner, PathResultType.Valid);                 
                        resultPoint = casted;
                        Finish();
                        return;
                    }
                }
            }

            //search with predicate
            int cellPathCount;
            CellConnection[] cellPath;
            PathResultType pathResult;
            float pathAproxCost;
            if (SearchGenericWithPredicate(layerMask, costModifyerMask, maxExecutionTimeInMilliseconds, properties, ignoreCrouchCost, startCell, startPosition, CellSearchDelegate, maxSearchCost, out cellPath, out cellPathCount, out pathResult, out pathAproxCost)) {
                Cell cellTarget = PathFinderData.cells[cellPath[cellPathCount - 1].connection];
                Vector3 targetPosition = new Vector3();

                foreach (var content in cellTarget.cellContentValues) {
                    if (content is ContentValueType) {
                        ContentValueType casted = (ContentValueType)content;
                        bool valid = false;
                        if (predicate != null) {
                            if (predicate((ContentValueType)content))
                                valid = true;
                        }
                        else valid = true;

                        if (valid) {
                            resultPoint = casted;
                            targetPosition = casted.position;
                            break;
                        }
                    }
                }

                //simplify target path
                GenericFunnel(path, cellPath, cellPathCount, startCell.passability, startPosition, cellTarget.passability, targetPosition);
                notThreadSafeResult.pathNavmeshCost = pathAproxCost;

                GenericPoolArray<CellConnection>.ReturnToPool(ref cellPath); //return to pool array that was allocated while performing Search()                   
            }
            notThreadSafeResult.Init(pathOwner, pathResult);
            Finish();
        }

        private bool CellSearchDelegate(Cell cell) {
            foreach (var content in cell.cellContentValues) {
                if (content is ContentValueType) {
                    if(predicate != null) {
                        if (predicate((ContentValueType)content))
                            return true;
                    }
                    else {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}