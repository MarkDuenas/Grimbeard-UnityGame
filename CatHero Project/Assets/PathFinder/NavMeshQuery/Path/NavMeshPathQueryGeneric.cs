using K_PathFinder.Graphs;
using K_PathFinder.PFDebuger;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder {
    public enum BestFitOptions : int {
        DontSearch = 0,
        FastSearch = 1,
        PreciseSearch = 2
    }

    public class NavMeshPathQueryGeneric : NavMeshPathQueryAbstract {
        const int MAX_RAYS = 7;
        bool applyRaycast;
        bool collectPathContent;
        BestFitOptions bestFitSearch;    
        
        public NavMeshPathQueryGeneric(AgentProperties properties, IPathOwner pathOwner) : base(properties, pathOwner) { }

        /// <summary>
        /// function to queue Path
        /// </summary>
        /// <param name="start">start position</param>
        /// <param name="target">end position</param>
        /// <param name="layerMask">determine what parts of navmesh will be used with bitmask</param>
        /// <param name="costModifyerMask">determine what cost modifyers will be used with bitmask</param>
        /// <param name="bestFitSearch">if yes then it also check nearest position to target if it fail to find it</param>     
        /// <param name="applyRaycast">if true then path will check what last node are visible from start to silmlify path</param>
        /// <param name="collectPathContent">if true it will also check cell content</param>
        /// <param name="ignoreCrouchCost">if true then crouch cosy will be calculated as walk cost</param>
        /// <param name="updatePathFinder">if true then after you queue work pathfinder will automaticaly updated. if you want to save up some perfomance you can batch some work within some time and call PathFinder.Update() youself</param>
        public void QueueWork(
            Vector3 start, 
            Vector3 target, 
            int layerMask = 1,
            int costModifyerMask = 0,
            BestFitOptions bestFitSearch = BestFitOptions.DontSearch,
            bool applyRaycast = true,
            bool collectPathContent = false,
            bool ignoreCrouchCost = false,
            bool updatePathFinder = true) {            
            if (!queryHaveWork) {
                queryHaveWork = true;
                this.layerMask = layerMask;
                this.costModifyerMask = costModifyerMask;
                this.startPosition = start;
                this.targetPosition = target;
                this.bestFitSearch = bestFitSearch;
                this.applyRaycast = applyRaycast;
                this.collectPathContent = collectPathContent;
                this.ignoreCrouchCost = ignoreCrouchCost;
                PathFinder.queryBatcher.AddWork(this, null);
                if (updatePathFinder)
                    PathFinder.Update();
            }
        }

        public override void PerformWork(object context) {
            notThreadSafeResult = Path.PoolRent();

            Cell[] globalCells = PathFinderData.cells;

            //start and end positions sampled inside OnBeforeNavmeshPositionUpdate()
            NavmeshSampleResult_Internal startSample = PathFinder.UnregisterNavmeshSampleAndReturnResult(pathStartSample);
            NavmeshSampleResult_Internal targetSample = PathFinder.UnregisterNavmeshSampleAndReturnResult(pathEndSample);
            
            //handle cases when no navmesh found
            if (startSample.type == NavmeshSampleResultType.InvalidNoNavmeshFound | startSample.cellGlobalID == -1) {
                notThreadSafeResult.Init(pathOwner, PathResultType.InvalidAgentOutsideNavmesh);
                Finish();
                return;
            }

            if (targetSample.type == NavmeshSampleResultType.InvalidNoNavmeshFound | targetSample.cellGlobalID == -1) {
                notThreadSafeResult.Init(pathOwner, PathResultType.InvalidTargetOutsideNavmesh);
                Finish();
                return;
            }

            Cell startCell = globalCells[startSample.cellGlobalID];
            Cell targetCell = globalCells[targetSample.cellGlobalID];
            
            //handle cases when navmesh found but point outside navmesh
            if (startSample.type != NavmeshSampleResultType.InsideNavmesh) {
                notThreadSafeResult.AddMove(startPosition, (MoveState)(int)startCell.passability);
                startPosition = startSample.position;
            }
            if (targetSample.type != NavmeshSampleResultType.InsideNavmesh)
                targetPosition = targetSample.position;

            //if path target are in same cell then we are done           
            if (startSample.cellGlobalID == targetSample.cellGlobalID) {       
                notThreadSafeResult.AddMove(startPosition, (MoveState)(int)startCell.passability);
                notThreadSafeResult.AddMove(targetPosition, (MoveState)(int)startCell.passability);
                notThreadSafeResult.SetCurrentIndex(1);
                notThreadSafeResult.Init(pathOwner, PathResultType.Valid);
                Finish();
                return;
            }   


            startPosition.x += ((startCell.centerVector3.x - startPosition.x) * 0.001f);
            startPosition.z += ((startCell.centerVector3.z - startPosition.z) * 0.001f);

            targetPosition.x += ((targetCell.centerVector3.x - targetPosition.x) * 0.001f);
            targetPosition.z += ((targetCell.centerVector3.z - targetPosition.z) * 0.001f);

            PathResultType resultType;
            int cellPathCount;
            CellConnection[] cellPath;
            float pathAproxCost;
            //i really need to stop adding argument here :(
            if (SearchGeneric(
                layerMask,
                costModifyerMask,
                maxExecutionTimeInMilliseconds, 
                properties,
                ignoreCrouchCost, 
                bestFitSearch, 
                startCell, startPosition, 
                ref targetCell, 
                ref targetPosition, 
                out cellPath, out cellPathCount, out resultType, out pathAproxCost)) {
                GenericFunnel(notThreadSafeResult, cellPath, cellPathCount, startCell.passability, startPosition, targetCell.passability, targetPosition);

                notThreadSafeResult.pathNavmeshCost = pathAproxCost;

                //if collection cell values
                if (collectPathContent) {
                    if (startCell.advancedAreaCell)//adding first cell
                        notThreadSafeResult.pathContent.AddRange(startCell.pathContent);
                    //adding second node in all collections
                    for (int i = 0; i < cellPathCount; i++) {
                        Cell connectedCell = globalCells[cellPath[i].connection];
                        if (connectedCell.advancedAreaCell)
                            notThreadSafeResult.pathContent.AddRange(connectedCell.pathContent);
                    }
                }

                if (applyRaycast) {     
                    Vector2 ajusted;
                    ajusted.x = startPosition.x + ((startCell.centerVector3.x - startPosition.x) * 0.001f);
                    ajusted.y = startPosition.z + ((startCell.centerVector3.z - startPosition.z) * 0.001f);                                            
                    Vector2 curDir = notThreadSafeResult.lastV2 - ajusted;

                    float curMag = SomeMath.SqrMagnitude(curDir);

                    //Debuger_K.ClearGeneric();

                    if (curMag > 0.001f) {
                        RaycastHitNavMesh2 rhnm;
                        PathFinderMainRaycasting.LinecastBody(ajusted.x, startPosition.y, ajusted.y, curDir.x, curDir.y, startCell, targetCell, curMag, true, true, startCell.area, startCell.passability, layerMask, out rhnm);
                                                
                        //Debuger_K.ClearGeneric();
                        //Debuger_K.AddLine(ajustedstart, rhnm.lastCell.centerVector3, Color.red);
                        if(rhnm.lastCell == targetCell)
                            notThreadSafeResult.SetCurrentIndex(notThreadSafeResult.pathNodes.Count - 1);             
                        else {
                            int lastVisible = 1;

                            for (int i = 1; i < Mathf.Min(MAX_RAYS, notThreadSafeResult.pathNodes.Count - 1); i++) { //ignore last and first
                                var prev = notThreadSafeResult.pathNodes[i - 1];
                                var cur = notThreadSafeResult.pathNodes[i];
                                var next = notThreadSafeResult.pathNodes[i + 1];

                                Vector2 curLinePos;
                                curLinePos.x = cur.x;
                                curLinePos.y = cur.z;

                                float cross = SomeMath.V2Cross(
                                    cur.x - prev.x, 
                                    cur.z - prev.z,
                                    next.x - prev.x,
                                    next.z - prev.z);

                                if(cross != 0f) {
                                    float curDirX = cur.x - startPosition.x;
                                    float curDirZ = cur.z - startPosition.z;

                                    if(cross > 0) {
                                        curLinePos.x -= (curDirZ * 0.005f);
                                        curLinePos.y += (curDirX * 0.005f);
                                    }
                                    else {
                                        curLinePos.x += (curDirZ * 0.005f);
                                        curLinePos.y -= (curDirX * 0.005f);
                                    }
                                }

                                //becaming cur line dir
                                curDir.x = curLinePos.x - ajusted.x;
                                curDir.y = curLinePos.y - ajusted.y;
                                
                                PathFinderMainRaycasting.RaycastBody(ajusted.x, startPosition.y, ajusted.y, curDir.x, curDir.y, startCell, SomeMath.SqrMagnitude(curDir), true, true, startCell.area, startCell.passability, layerMask, out rhnm);
                                if (rhnm.resultType == 0) 
                                    lastVisible = i;     
                            }
                            notThreadSafeResult.SetCurrentIndex(lastVisible);
                        }
                    }
                }
                
                GenericPoolArray<CellConnection>.ReturnToPool(ref cellPath);            
            }
            notThreadSafeResult.Init(pathOwner, resultType);
            Finish();
        }

        //public class NavMeshPathQuerySimple : NavMeshPathQueryAbstract {
        //    public NavMeshPathQuerySimple(AgentProperties properties, IPathOwner pathOwner) : base(properties, pathOwner) { }

        //    public void QueueWork(Vector3 start, Vector3 target, int layerMask = 1, bool snapToNavMesh = true, bool updatePathFinder = true) {
        //        if (!queryHaveWork) {
        //            queryHaveWork = true;
        //            this.layerMask = layerMask;
        //            this.startPosition = start;
        //            this.targetPosition = target;
        //            this.snapToNavMesh = snapToNavMesh;
        //            PathFinder.queryBatcher.AddWork(this, null);
        //            if (updatePathFinder)
        //                PathFinder.Update();
        //        }
        //    }

        //    //bare bones of path search
        //    public override void PerformWork(object context) {
        //        notThreadSafeResult = Path.PoolRent();

        //        //start and end positions sampled inside OnBeforeNavmeshPositionUpdate()
        //        NavmeshSampleResult_Internal startSample = PathFinder.UnregisterNavmeshSampleAndReturnResult(pathStartSample);
        //        NavmeshSampleResult_Internal targetSample = PathFinder.UnregisterNavmeshSampleAndReturnResult(pathEndSample);

        //        if (startSample.type == NavmeshSampleResultType.InvalidNoNavmeshFound | startSample.cellGlobalID == -1) {
        //            notThreadSafeResult.Init(pathOwner, PathResultType.InvalidAgentOutsideNavmesh);
        //            Finish();
        //            return;
        //        }

        //        if (targetSample.type == NavmeshSampleResultType.InvalidNoNavmeshFound | targetSample.cellGlobalID == -1) {
        //            notThreadSafeResult.Init(pathOwner, PathResultType.InvalidTargetOutsideNavmesh);
        //            Finish();
        //            return;
        //        }

        //        if (snapToNavMesh) {
        //            if (startSample.type == NavmeshSampleResultType.OutsideNavmesh)
        //                startPosition = startSample.position;
        //            if (targetSample.type == NavmeshSampleResultType.OutsideNavmesh)
        //                targetPosition = targetSample.position;
        //        }

        //        Cell[] globalCells = PathFinderData.cells;
        //        Cell startCell = globalCells[startSample.cellGlobalID];

        //        //if path target are in same cell then we are done           
        //        if (startSample.cellGlobalID == targetSample.cellGlobalID) {
        //            notThreadSafeResult.AddMove(startPosition, (MoveState)(int)startCell.passability);
        //            notThreadSafeResult.AddMove(targetPosition, (MoveState)(int)startCell.passability);
        //            notThreadSafeResult.SetCurrentIndex(1);
        //            notThreadSafeResult.Init(pathOwner, PathResultType.Valid);
        //            Finish();
        //            return;
        //        }

        //        Cell targetCell = globalCells[targetSample.cellGlobalID];

        //        int cellPathCount;
        //        CellConnection[] cellPath;
        //        PathResultType pathResult;
        //        float pathAproxCost;
        //        if (SearchSimple(layerMask, maxExecutionTimeInMilliseconds, properties, ignoreCrouchCost, startCell, startPosition, targetCell, targetPosition, out cellPath, out cellPathCount, out pathResult, out pathAproxCost)) {
        //            GenericFunnel(notThreadSafeResult, cellPath, cellPathCount, startCell.passability, startPosition, targetCell.passability, targetPosition);
        //            notThreadSafeResult.pathNavmeshCost = pathAproxCost;
        //            GenericPoolArray<CellConnection>.ReturnToPool(ref cellPath); //return to pool array that was allocated while performing Search()            
        //        }
        //        notThreadSafeResult.Init(pathOwner, pathResult);
        //        Finish();
        //    }
        //}
    }
}