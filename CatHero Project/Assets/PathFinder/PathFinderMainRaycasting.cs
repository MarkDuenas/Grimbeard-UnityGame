using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using K_PathFinder.Graphs;


namespace K_PathFinder {
    /// <summary>
    /// class to store referencess. cause otherwise it cant be threaded.
    /// and it cant be threaded cause line drawing thing are have delegate as input. so all temporary data stored here and also passed to line drawing delegate
    /// </summary>
    public class RaycastAllocatedData {
        //reference types
        public bool[] raycastSamples = new bool[4];//which chunk sides should be cheked

        public CellContentConnectionData[] edgeMapData;     //current chunk map
        public IndexLengthInt[] edgeMapDataLayout; //current chunk map
        public IndexLengthInt currentLayout;       //current pixel array
        public Cell curCell, prevCell;//cells
        public Area expArea;//expected area

        //value types
        public Passability expPass;//expected passability
        public bool 
            raycastDone, //main flag in case something go wrong
            checkPass,   //do check passability chages
            checkArea;   //do check area changes
        public NavmeshRaycastResultType2 raycastType; //current raycast result
        public ChunkData currentChunkData; //currrent struct that represent chunk
        public float
            raycastResultX, raycastResultY, raycastResultZ, //result
            posX, posY, posZ,  //start
            rayDirX, rayDirY,  //ray direction
            pixelSize,    //chunk pixel size
            maxSqrLength;      //maximum length

        public int 
            startIntX, startIntY, //start chunk position * 10 + start pixel
            curChunkIntX, curChunkIntY, //current chunk position * 10
            gridDistanceTreshold; //what sqr distance is too large

        public void Clear() {
            //do nothing?
        }
    }

    public static class PathFinderMainRaycasting {
        //private const bool DEBUG = false;
        //private static CellContentData[] raycastSamplesTemplate = new CellContentData[4];

        //private static Queue<RaycastAllocatedData> pool = new Queue<RaycastAllocatedData>();
        //private static object poolLock = new object();

        //called externaly cause PathFinder.gridSize can be zero in that case
        //public static void Init() {
        //    Vector2 A = new Vector2(0, 0);
        //    Vector2 B = new Vector2(PathFinder.gridSize, 0);
        //    Vector2 C = new Vector2(0, PathFinder.gridSize);
        //    Vector2 D = new Vector2(PathFinder.gridSize, PathFinder.gridSize);
        //    raycastSamplesTemplate[0] = new CellContentData(C, A);
        //    raycastSamplesTemplate[1] = new CellContentData(D, C);
        //    raycastSamplesTemplate[2] = new CellContentData(B, D);
        //    raycastSamplesTemplate[3] = new CellContentData(A, B);
        //}


        public static void RaycastBody(float posX, float posY, float posZ, float rayDirX, float rayDirY, Cell cell,
            float maxLengthSqr, bool checkArea, bool checkPass, Area expArea, Passability expPass, int layerMask,
            out RaycastHitNavMesh2 hit) {
            hit.x = posX;
            hit.y = posY;
            hit.z = posZ;
            hit.lastCell = cell;
            hit.resultType = NavmeshRaycastResultType2.Nothing;

            Cell[] globalCells = PathFinderData.cells;

            while (true) {
                CellContentRaycastData[] raycastData = cell.raycastData;
                int raycastDataCount = cell.raycastDataCount;
                bool anyHit = false;
                for (int i = 0; i < raycastDataCount; i++) {
                    CellContentRaycastData d = raycastData[i];
                    //Vector3 a = new Vector3(d.xLeft, d.yLeft, d.zLeft);
                    //Vector3 b = new Vector3(d.xRight, d.yRight, d.zRight);

                    if (SomeMath.V2Cross(d.xLeft - posX, d.zLeft - posZ, rayDirX, rayDirY) >= 0f &&
                        SomeMath.V2Cross(d.xRight - posX, d.zRight - posZ, rayDirX, rayDirY) < 0f) {
          
                        //Debuger_K.AddLine(mapData.data.midV3, p, Color.blue);
                        //Debuger_K.AddLine(mapData.from.centerVector3, mapData.data.midV3, Color.green);
                        //Debuger_K.AddLabel(ccd.leftV3, SomeMath.V2Cross(ccd.xLeft - rad.posX, ccd.zLeft - rad.posZ, rad.rayDirX, rad.rayDirY));
                        //Debuger_K.AddLabel(ccd.rightV3, SomeMath.V2Cross(ccd.xRight - rad.posX, ccd.zRight - rad.posZ, rad.rayDirX, rad.rayDirY));

                        float ccdDirX = d.xRight - d.xLeft;
                        float ccdDirZ = d.zRight - d.zLeft;
                        float dot = ccdDirZ * rayDirX - ccdDirX * rayDirY; // if d == 0 then bad situation;

                        if (dot != 0f) {
                            anyHit = true;
                            float product = ((d.xLeft - posX) * rayDirY + (posZ - d.zLeft) * rayDirX) / dot;
                            hit.x = d.xLeft + ccdDirX * product;
                            hit.y = d.yLeft + (d.yRight - d.yLeft) * product;
                            hit.z = d.zLeft + ccdDirZ * product;

                            hit.lastCell = cell;

                            //Debuger_K.AddDot(hit.point, Color.red);
                            //Debuger_K.AddLabel(hit.point, SomeMath.Distance(posX, posY, posZ, hit.x, hit.y, hit.z) + " : " + maxLength);
                            if (SomeMath.SqrDistance(posX, posY, posZ, hit.x, hit.y, hit.z) >= maxLengthSqr) {
                                hit.resultType = NavmeshRaycastResultType2.ReachMaxDistance;
                                return;
                            }
                            if (d.connection != -1) {
                                Cell curCell = globalCells[d.connection];
                                if ((1 << curCell.bitMaskLayer & layerMask) == 0) {
                                    hit.resultType = NavmeshRaycastResultType2.RayHitCellExcludedByLayerMask;
                                    return;
                                }
                                else if (checkPass && curCell.passability != expPass) {
                                    hit.resultType = NavmeshRaycastResultType2.PassabilityChange;                         
                                    return;
                                }
                                else if (checkArea && curCell.area != expArea) {
                                    hit.resultType = NavmeshRaycastResultType2.AreaChange;                               
                                    return;
                                }
                                cell = curCell;
                            }
                            else {
                                hit.resultType = NavmeshRaycastResultType2.NavmeshBorderHit;
                                return;
                            }
                            continue;
                        }           
                    }
                }
                if (anyHit == false) {
                    Debug.LogErrorFormat("ray did not hit any cell border x {0}, y {1}, z {2}, dx {3}, dy {4}, max {5}", posX, posY, posZ, rayDirX, rayDirY, maxLengthSqr);
                    return;
                }
            }
        }

        public static void LinecastBody(float posX, float posY, float posZ, float rayDirX, float rayDirY, Cell cell, Cell target,
            float maxLengthSqr, bool checkArea, bool checkPass, Area expArea, Passability expPass, int layerMask,
            out RaycastHitNavMesh2 hit) {
            hit.x = posX;
            hit.y = posY;
            hit.z = posZ;
            hit.lastCell = cell;
            hit.resultType = NavmeshRaycastResultType2.Nothing;

            Cell[] globalCells = PathFinderData.cells;

            while (true) {
                CellContentRaycastData[] raycastData = cell.raycastData;
                int raycastDataCount = cell.raycastDataCount;
                bool anyHit = false;
                for (int i = 0; i < raycastDataCount; i++) {
                    CellContentRaycastData d = raycastData[i];
                    if (SomeMath.V2Cross(d.xLeft - posX, d.zLeft - posZ, rayDirX, rayDirY) >= 0f &&
                        SomeMath.V2Cross(d.xRight - posX, d.zRight - posZ, rayDirX, rayDirY) < 0f) {
                        float ccdDirX = d.xRight - d.xLeft;
                        float ccdDirZ = d.zRight - d.zLeft;
                        float dot = ccdDirZ * rayDirX - ccdDirX * rayDirY; // if d == 0 then lines paralel

                        if (dot != 0f) {
                            anyHit = true;
                            float product = ((d.xLeft - posX) * rayDirY + (posZ - d.zLeft) * rayDirX) / dot;
                            hit.x = d.xLeft + ccdDirX * product;
                            hit.y = d.yLeft + (d.yRight - d.yLeft) * product;
                            hit.z = d.zLeft + ccdDirZ * product;
                            hit.lastCell = cell;

                            if(cell == target) {
                                hit.resultType = NavmeshRaycastResultType2.Nothing;
                                return;
                            }

                            if (SomeMath.SqrDistance(posX, posY, posZ, hit.x, hit.y, hit.z) >= maxLengthSqr) {
                                hit.resultType = NavmeshRaycastResultType2.ReachMaxDistance;
                                return;
                            }
                            if (d.connection != -1) {
                                Cell curCell = globalCells[d.connection];
                                if ((1 << curCell.bitMaskLayer & layerMask) == 0) {
                                    hit.resultType = NavmeshRaycastResultType2.RayHitCellExcludedByLayerMask;
                                    return;
                                }
                                else if (checkPass && curCell.passability != expPass) {
                                    hit.resultType = NavmeshRaycastResultType2.PassabilityChange;
                                    return;
                                }
                                else if (checkArea && curCell.area != expArea) {
                                    hit.resultType = NavmeshRaycastResultType2.AreaChange;
                                    return;
                                }
                                cell = curCell;
                            }
                            else {
                                hit.resultType = NavmeshRaycastResultType2.NavmeshBorderHit;
                                return;
                            }
                            continue;
                        }
                    }
                }
                if (anyHit == false) {
#if UNITY_EDITOR
                    PFDebuger.Debuger_K.AddLabel(cell.centerVector3, "Start");
                    PFDebuger.Debuger_K.AddLabel(target.centerVector3, "end");
                    PFDebuger.Debuger_K.AddLabel(new Vector3(posX, posY + 0.05f, posZ), "agent");

                    var rData = cell.raycastData;
                    var rCount = cell.raycastDataCount;      
                    for (int i = 0; i < rCount; i++) {
                        var curD = rData[i];
                        PFDebuger.Debuger_K.AddLine(new Vector3(curD.xLeft, curD.yLeft, curD.zLeft), new Vector3(curD.xRight, curD.yRight, curD.zRight), Color.green, addOnTop: 0.05f);
                    }
                    rData = target.raycastData;
                    rCount = target.raycastDataCount;
                    for (int i = 0; i < rCount; i++) {
                        var curD = rData[i];
                        PFDebuger.Debuger_K.AddLine(new Vector3(curD.xLeft, curD.yLeft, curD.zLeft), new Vector3(curD.xRight, curD.yRight, curD.zRight), Color.red, addOnTop: 0.05f);
                    }
                    PFDebuger.Debuger_K.AddRay(new Vector3(posX, posY + 0.05f, posZ), new Vector3(rayDirX, 0, rayDirY), Color.blue, length: Mathf.Sqrt(maxLengthSqr));
#endif

                    throw new System.Exception(string.Format("ray did not hit any cell border x {0}, y {1}, z {2}, dx {3}, dy {4}, max {5}", posX, posY, posZ, rayDirX, rayDirY, maxLengthSqr));           
                }
            }
        }


        //public static void Raycast2Body2(float posX, float posY, float posZ, float rayDirX, float rayDirY, Cell cell, 
        //    float maxLength, bool checkArea, bool checkPass, Area expArea, Passability expPass, 
        //    RaycastAllocatedData rad, out RaycastHitNavMesh2 hit) {
        //    if (SomeMath.SqrMagnitude(rayDirX, rayDirY) == 0f) {
        //        hit = new RaycastHitNavMesh2(posX, posY, posZ, NavmeshRaycastResultType2.ReachMaxDistance, cell);
        //        return;
        //    }

        //    rad.pixelSize = PathFinder.gridSize / PathFinder.CELL_GRID_SIZE;

        //    //trick to fix case when raycast start on "near" edge
        //    //currently can't be on chunk edge so we dont care if chunk changed
        //    if (cell == null) Debug.Log("cell == null");
        //    if (cell.graph == null) Debug.Log("cell.graph == null");

        //    rad.currentChunkData = cell.graph.chunk;

        //    int curGridX = (int)((posX - rad.currentChunkData.realX) / rad.pixelSize);
        //    int curGridY = (int)((posZ - rad.currentChunkData.realZ) / rad.pixelSize);

        //    //if (curGridX < 0) curGridX = 0; else if (curGridX > 9) curGridX = 9; 
        //    //if (curGridY < 0) curGridY = 0; else if (curGridY > 9) curGridY = 9;

        //    rad.startIntX = (rad.currentChunkData.x * 10) + curGridX;
        //    rad.startIntY = (rad.currentChunkData.z * 10) + curGridY;

        //    var tempVal = maxLength / rad.pixelSize;
        //    if(tempVal > 10000) {//too big number anyway
        //        rad.gridDistanceTreshold = SomeMath.Sqr(10000);
        //    }
        //    else {
        //        rad.gridDistanceTreshold = (int)SomeMath.Sqr(maxLength / rad.pixelSize) + 1;
        //    }

        //    //CellDataMapValue[] edgeMapData = rad.edgeMapData;
        //    //IndexLengthInt[] edgeMapDataLayout = rad.edgeMapDataLayout;


        //    //just shift start position to cell center in case it laying in awkward place.
        //    Vector2 dirToCellCenter = (cell.centerVector2 - new Vector2(posX, posZ)).normalized;
        //    posX += dirToCellCenter.x * 0.001f;
        //    posZ += dirToCellCenter.y * 0.001f;

        //    //if (DEBUG) {
        //    //    Debuger_K.AddRay(new Vector3(posX, posY + 0.1f, posZ), new Vector3(rayDirX, 0, rayDirY), Color.gray);
        //    //}

        //    rad.posX = posX;
        //    rad.posY = posY;
        //    rad.posZ = posZ;
        //    rad.rayDirX = rayDirX;
        //    rad.rayDirY = rayDirY;
        //    rad.checkPass = checkPass;
        //    rad.checkArea = checkArea;
        //    rad.expPass = expPass;
        //    rad.expArea = expArea;
        //    rad.raycastType = NavmeshRaycastResultType2.Nothing;
        //    rad.maxSqrLength = SomeMath.Sqr(maxLength);
        //    rad.curCell = cell;
        //    rad.prevCell = null;
        //    rad.raycastDone = false;

        //    float
        //        chunkX,
        //        chunkZ,
        //        curHullX,
        //        curHullZ,
        //        lastHullX = posX,
        //        lastHullZ = posZ;

        //    for (int i = 0; i < 4; i++) {
        //        rad.raycastSamples[i] = raycastSamplesTemplate[i].RotateRightAndReturnDot(rayDirX, rayDirY) < 0;
        //    }

        //    int chunkIteration = 0;
        //    while (rad.raycastDone == false) {
        //        chunkIteration++;
        //        if (chunkIteration > 50) {
        //            string s = string.Format("chunkIteration too large. x {0}, y {1}, z {2}, dx {3}, dy {4}, max {5}", posX, posY, posZ, rayDirX, rayDirY, maxLength);
        //            //Debuger_K.AddRay(new Vector3(posX, posY, posZ), Vector3.down, Color.cyan);
        //            //Debuger_K.AddRay(new Vector3(posX, posY, posZ), new Vector3(rayDirX, 0, rayDirY), Color.yellow, 50);
        //            //Debuger_K.UserfulPublicFlag = true;
        //            Debug.LogError(s);
        //            break;
        //        }

        //        rad.currentChunkData = rad.curCell.graph.chunk;
        //        rad.curChunkIntX = rad.currentChunkData.x * 10;
        //        rad.curChunkIntY = rad.currentChunkData.z * 10;
        //        rad.edgeMapData = rad.curCell.graph.edgeMapData;
        //        rad.edgeMapDataLayout = rad.curCell.graph.edgeMapDataLayout;

        //        chunkX = rad.currentChunkData.realX;
        //        chunkZ = rad.currentChunkData.realZ;

        //        #region border points   
        //        curHullX = posX;
        //        curHullZ = posZ;
        //        for (int i = 0; i < 4; i++) {
        //            if (rad.raycastSamples[i]) {
        //                CellContentData curSide = raycastSamplesTemplate[i];
        //                float rX, rZ;

        //                if (SomeMath.RayIntersectSegment(posX, posZ, rayDirX, rayDirY, curSide.xLeft + chunkX, curSide.zLeft + chunkZ, curSide.xRight + chunkX, curSide.zRight + chunkZ, out rX, out rZ)) {
        //                    curHullX = rX;
        //                    curHullZ = rZ;
        //                }
        //                //if (DEBUG)
        //                //Debuger_K.AddLine(curSide.a, curSide.b, Color.red, chunkIteration);
        //            }
        //        }

        //        #region debug
        //        //if (DEBUG) {
        //        //Debuger_K.AddLine(new Vector3(curHullX, 0, curHullZ), new Vector3(lastHullX, 0, lastHullZ), Color.yellow, chunkIteration);

        //        //for (int x = 0; x < PathFinder.CELL_GRID_SIZE + 1; x++) {
        //        //    Debuger_K.AddLine(
        //        //        rad.currentChunkData.realPositionV3 + new Vector3(x * rad.pixelSize, 0, 0),
        //        //        rad.currentChunkData.realPositionV3 + new Vector3(x * rad.pixelSize, 0, PathFinder.gridSize),
        //        //        Color.red);
        //        //}
        //        //for (int z = 0; z < PathFinder.CELL_GRID_SIZE + 1; z++) {
        //        //    Debuger_K.AddLine(
        //        //        rad.currentChunkData.realPositionV3 + new Vector3(0, 0, z * rad.pixelSize),
        //        //        rad.currentChunkData.realPositionV3 + new Vector3(PathFinder.gridSize, 0, z * rad.pixelSize),
        //        //        Color.red);
        //        //}
        //        //}
        //        #endregion

        //        #endregion

        //        DDARasterization.DrawLine(
        //            lastHullX - chunkX,
        //            lastHullZ - chunkZ,
        //            curHullX - chunkX,
        //            curHullZ - chunkZ,
        //            rad.pixelSize,
        //            rad,
        //            RaycastDelegate);

        //        if (rad.curCell != null) {
        //            dirToCellCenter = (rad.curCell.centerVector2 - new Vector2(curHullX, curHullZ)).normalized; //every chunk shift point to next cell center
        //            lastHullX = curHullX + dirToCellCenter.x * 0.001f;
        //            lastHullZ = curHullZ + dirToCellCenter.y * 0.001f;
        //            //lastHullX = curHullX;
        //            //lastHullZ = curHullZ;
        //        }
        //    }

        //    hit = new RaycastHitNavMesh2(rad.raycastResultX, rad.raycastResultY, rad.raycastResultZ, rad.raycastType, rad.curCell);
        //}


        //private static bool RaycastDelegate(int x, int y, RaycastAllocatedData rad) {
        //    if (rad.raycastDone)
        //        return true;

        //    if (x < 0) x = 0; else if (x > PathFinder.CELL_GRID_SIZE - 1) x = PathFinder.CELL_GRID_SIZE - 1; //x = SomeMath.Clamp(0, CELL_GRID_SIZE - 1, x);
        //    if (y < 0) y = 0; else if (y > PathFinder.CELL_GRID_SIZE - 1) y = PathFinder.CELL_GRID_SIZE - 1; //y = SomeMath.Clamp(0, CELL_GRID_SIZE - 1, y);
        //    if (SomeMath.SqrDistance(rad.startIntX, rad.startIntY, rad.curChunkIntX + x, rad.curChunkIntY + y) > rad.gridDistanceTreshold) {
        //        rad.raycastType = NavmeshRaycastResultType2.ReachMaxDistance;
        //        rad.raycastDone = true;
        //    }

        //    CellContentConnectionData[] edgeMapData = rad.edgeMapData;
        //    IndexLengthInt[] edgeMapDataLayout = rad.edgeMapDataLayout;
        //    IndexLengthInt currentLayout = edgeMapDataLayout[(10 * y) + x];

        //    Vector3 p = rad.currentChunkData.realPositionV3 + new Vector3((x * rad.pixelSize) + (rad.pixelSize * 0.5f), 0, (y * rad.pixelSize) + (rad.pixelSize * 0.5f));
        //    Debuger_K.AddDot(p, Color.yellow, 0.02f);
        //    //Debuger_K.AddLine(rad.curCell.centerVector3, p, Color.cyan);


        //    int cellLoop = 0;
        //    for (int i = 0; i < currentLayout.length; i++) {
        //        if (cellLoop > 100) {
        //            Debug.LogErrorFormat("cellLoop too large. x {0}, y {1}, z {2}, dx {3}, dy {4}, max {5}", rad.posX, rad.posY, rad.posZ, rad.rayDirX, rad.rayDirY, Mathf.Sqrt(rad.maxSqrLength));
        //            rad.raycastDone = true;
        //            return true;
        //        }

        //        CellContentConnectionData mapData = edgeMapData[currentLayout.index + i];
        //        CellContentData ccd = mapData.data;

        //        //Debuger_K.AddLine(mapData.data.midV3, p, Color.blue);

        //        if(cellLoop == 1) {
        //            Debuger_K.AddDot(rad.curCell.centerVector3, Color.magenta, 0.07f);

        //            if (x == 2 && y == 4) {
        //                Vector3 up = (Vector3.up * 0.1f * (i + 1));
        //                Debuger_K.AddLine(ccd.a + up, ccd.b + up, Color.blue);
        //                Debuger_K.AddLine(ccd.midV3 + up, mapData.from.centerVector3, Color.green);

        //                if (mapData.from == rad.curCell) {

        //                    if (mapData.connection != null)
        //                        Debuger_K.AddLine(ccd.midV3 + up, mapData.connection.centerVector3, Color.red);
        //                }
        //            }
        //        }



        //        if (mapData.from == rad.curCell && 
        //            SomeMath.V2Cross(ccd.xLeft - rad.posX, ccd.zLeft - rad.posZ, rad.rayDirX, rad.rayDirY) >= 0f &&
        //            SomeMath.V2Cross(ccd.xRight - rad.posX, ccd.zRight - rad.posZ, rad.rayDirX, rad.rayDirY) < 0f){


        //            //Debuger_K.AddLine(mapData.data.midV3, p, Color.blue);
        //            //Debuger_K.AddLine(mapData.from.centerVector3, mapData.data.midV3, Color.green);
        //            //Debuger_K.AddLabel(ccd.leftV3, SomeMath.V2Cross(ccd.xLeft - rad.posX, ccd.zLeft - rad.posZ, rad.rayDirX, rad.rayDirY));
        //            //Debuger_K.AddLabel(ccd.rightV3, SomeMath.V2Cross(ccd.xRight - rad.posX, ccd.zRight - rad.posZ, rad.rayDirX, rad.rayDirY));

        //            float ccdDirX = ccd.xRight - ccd.xLeft;
        //            float ccdDirZ = ccd.zRight - ccd.zLeft;
        //            float d = ccdDirZ * rad.rayDirX - ccdDirX * rad.rayDirY; // if d == 0 then bad situation;

        //            if (d != 0f) {
        //                float product = ((ccd.xLeft - rad.posX) * rad.rayDirY + (rad.posZ - ccd.zLeft) * rad.rayDirX) / d;


        //                cellLoop++;
        //                rad.raycastResultX = ccd.xLeft + ccdDirX * product;
        //                rad.raycastResultY = ccd.yLeft + (ccd.yRight - ccd.yLeft) * product;
        //                rad.raycastResultZ = ccd.zLeft + ccdDirZ * product;
        //                Debuger_K.AddDot(new Vector3(rad.raycastResultX, rad.raycastResultY, rad.raycastResultZ), Color.red);


        //                if (SomeMath.SqrDistance(rad.posX, rad.posY, rad.posZ, rad.raycastResultX, rad.raycastResultY, rad.raycastResultZ) >= rad.maxSqrLength) {
        //                    rad.raycastType = NavmeshRaycastResultType2.ReachMaxDistance;
        //                    rad.raycastDone = true;
        //                    return true;
        //                }

        //                if (mapData.connection != null) {          
        //                    if (rad.curCell.canBeUsed == false) {
        //                        rad.raycastType = NavmeshRaycastResultType2.IncativeCell;
        //                        rad.raycastDone = true;
        //                        return true;
        //                    }
        //                    else if (rad.checkPass && rad.curCell.passability != rad.expPass) {
        //                        rad.raycastType = NavmeshRaycastResultType2.PassabilityChange;
        //                        rad.raycastDone = true;
        //                        return true;
        //                    }
        //                    else if (rad.checkArea && rad.curCell.area != rad.expArea) {
        //                        rad.raycastType = NavmeshRaycastResultType2.AreaChange;
        //                        rad.raycastDone = true;
        //                        return true;
        //                    }

        //                    i = 0;
        //                    rad.curCell = mapData.connection;
        //                }
        //                else {
        //                    rad.curCell = null;
        //                    rad.raycastType = NavmeshRaycastResultType2.NavmeshBorderHit;
        //                    rad.raycastDone = true;
        //                    return true;
        //                }
        //            }
        //        }
        //    }
        //    return rad.raycastDone;
        //}


        //private static bool RaycastDelegate(int x, int y, RaycastAllocatedData rad) {
        //    if (rad.raycastDone)
        //        return true;

        //    if (x < 0) x = 0; else if (x > PathFinder.CELL_GRID_SIZE - 1) x = PathFinder.CELL_GRID_SIZE - 1; //x = SomeMath.Clamp(0, CELL_GRID_SIZE - 1, x);
        //    if (y < 0) y = 0; else if (y > PathFinder.CELL_GRID_SIZE - 1) y = PathFinder.CELL_GRID_SIZE - 1; //y = SomeMath.Clamp(0, CELL_GRID_SIZE - 1, y);
        //    if (SomeMath.SqrDistance(rad.startIntX, rad.startIntY, rad.curChunkIntX + x, rad.curChunkIntY + y) > rad.gridDistanceTreshold) {
        //        rad.raycastType = NavmeshRaycastResultType2.ReachMaxDistance;
        //        rad.raycastDone = true;
        //    }
        //    //IMPORTANT: edges in this thing are sorted. "connection != null" at the begining and "connection == null" at the end.
        //    //some logic here based on this order    

        //    CellContentConnectionData[] edgeMapData = rad.edgeMapData;
        //    IndexLengthInt[] edgeMapDataLayout = rad.edgeMapDataLayout;

        //    if (edgeMapData == null | edgeMapDataLayout == null)
        //        return false;

        //    IndexLengthInt currentLayout = edgeMapDataLayout[Graph.GetEdgeMapIndex(x, y)];

        //    #region debug            
        //    //if (DEBUG) {
        //    //Vector3 p = rad.currentChunkData.realPositionV3 + new Vector3((x * rad.pixelSize) + (rad.pixelSize * 0.5f), 0, (y * rad.pixelSize) + (rad.pixelSize * 0.5f));
        //    //Debuger_K.AddDot(rad.curCell.centerVector3, Color.cyan);
        //    //Debuger_K.AddDot(p, Color.red, 0.05f);
        //    //list.ForEach(item => Debuger_K.AddLine(item.data.NearestPoint(p), p, Color.blue));
        //    //}
        //    #endregion

        //    int cellLoop = 0;
        //    bool doCellLoop = true;
        //    while (doCellLoop) {
        //        cellLoop++;
        //        if (cellLoop > 100) {
        //            Debug.LogErrorFormat("cellLoop too large. x {0}, y {1}, z {2}, dx {3}, dy {4}, max {5}", rad.posX, rad.posY, rad.posZ, rad.rayDirX, rad.rayDirY, Mathf.Sqrt(rad.maxSqrLength));
        //            break;
        //        }

        //        doCellLoop = false;
        //        for (int i = 0; i < currentLayout.length; i++) {
        //            CellContentConnectionData mapData = edgeMapData[currentLayout.index + i];
        //            if (mapData.from != rad.curCell)
        //                continue;



        //            CellContentData ccd = mapData.data;

        //            Debuger_K.AddLabel(ccd.leftV3, SomeMath.V2Cross(ccd.xLeft - rad.posX, ccd.zLeft - rad.posZ, rad.rayDirX, rad.rayDirY));
        //            Debuger_K.AddLabel(ccd.rightV3, SomeMath.V2Cross(ccd.xRight - rad.posX, ccd.zRight - rad.posZ, rad.rayDirX, rad.rayDirY));

        //            if ((-(ccd.zRight - ccd.zLeft) * rad.rayDirX) + ((ccd.xRight - ccd.xLeft) * rad.rayDirY) < 0f &&
        //                SomeMath.V2Cross(ccd.xLeft - rad.posX, ccd.zLeft - rad.posZ, rad.rayDirX, rad.rayDirY) <= 0f &&
        //                SomeMath.V2Cross(ccd.xRight - rad.posX, ccd.zRight - rad.posZ, rad.rayDirX, rad.rayDirY) > 0f)
        //                continue;

        //            float ccdDirX = ccd.xRight - ccd.xLeft;
        //            float ccdDirZ = ccd.zRight - ccd.zLeft;
        //            float d = ccdDirZ * rad.rayDirX - ccdDirX * rad.rayDirY; // if d == 0 then bad situation;

        //            if (d == 0f)
        //                continue;

        //            float product = ((ccd.xLeft - rad.posX) * rad.rayDirY + (rad.posZ - ccd.zLeft) * rad.rayDirX) / d;

        //            //Debuger_K.AddDot(new Vector3(ix, iy, iz), Color.red);

        //            //if (SomeMath.RayIntersectXZ(rad.posX, rad.posZ, rad.rayDirX, rad.rayDirY, ccd.xLeft, ccd.yLeft, ccd.zLeft, ccd.xRight, ccd.yRight, ccd.zRight, out ix, out iy, out iz) == false)
        //            //    continue;

        //            rad.raycastResultX = ccd.xLeft + ccdDirX * product;
        //            rad.raycastResultY = ccd.yLeft + (ccd.yRight - ccd.yLeft) * product;
        //            rad.raycastResultZ = ccd.zLeft + ccdDirZ * product;
        //            rad.prevCell = rad.curCell;

        //            if (SomeMath.SqrDistance(rad.posX, rad.posY, rad.posZ, rad.raycastResultX, rad.raycastResultY, rad.raycastResultZ) >= rad.maxSqrLength) {
        //                rad.raycastType = NavmeshRaycastResultType2.ReachMaxDistance;
        //                rad.raycastDone = true;
        //                return true;
        //            }

        //            if (mapData.connection != null) {
        //                #region debug
        //                //if (DEBUG) {
        //                //    Vector3 p = currentChunkData.realPositionV3 + new Vector3((x * chunkPixelSize) + (chunkPixelSize * 0.5f), 0, (y * chunkPixelSize) + (chunkPixelSize * 0.5f));
        //                //    //Debuger_K.AddLine(ToV3(curHullIntersection), resultVector);
        //                //    if (prevCell != null) {
        //                //        Vector3 p1p = SomeMath.MidPoint(curCell.centerV3, prevCell.centerV3);
        //                //        //Vector3 p2p = SomeMath.MidPoint(p1p, p);
        //                //        Debuger_K.AddLine(curCell.centerV3, prevCell.centerV3, Color.green);
        //                //        Debuger_K.AddLine(p1p, p, Color.cyan);
        //                //    }
        //                //}
        //                #endregion

        //                doCellLoop = true;
        //                rad.curCell = mapData.connection;

        //                if (rad.curCell.canBeUsed == false) {
        //                    rad.raycastType = NavmeshRaycastResultType2.IncativeCell;
        //                    rad.raycastDone = true;
        //                    return true;
        //                }
        //                else if (rad.checkPass && rad.curCell.passability != rad.expPass) {
        //                    rad.raycastType = NavmeshRaycastResultType2.PassabilityChange;
        //                    rad.raycastDone = true;
        //                    return true;
        //                }
        //                else if (rad.checkArea && rad.curCell.area != rad.expArea) {
        //                    rad.raycastType = NavmeshRaycastResultType2.AreaChange;
        //                    rad.raycastDone = true;
        //                    return true;
        //                }
        //            }
        //            else {
        //                rad.curCell = null;
        //                rad.raycastType = NavmeshRaycastResultType2.NavmeshBorderHit;
        //                rad.raycastDone = true;
        //                return true;
        //            }
        //            break;
        //        }
        //    }
        //    return rad.raycastDone;
        //}

    }
}