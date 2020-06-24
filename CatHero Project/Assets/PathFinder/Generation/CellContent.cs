using K_PathFinder.EdgesNameSpace;
using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Graphs {
    //struct for storing two Vector3
    //never use it for points in same space out you get divine punishment
    [Serializable]
    public struct CellContentData : IEquatable<CellContentData> {
        [SerializeField]
        public float 
            xLeft, yLeft, zLeft, //lefts
            xRight, yRight, zRight; //rights        

        public CellContentData(float xLeft, float yLeft, float zLeft, float xRight, float yRight, float zRight) {
            this.xLeft = xLeft;
            this.yLeft = yLeft;
            this.zLeft = zLeft;

            this.xRight = xRight;
            this.yRight = yRight;
            this.zRight = zRight;
        }
        public CellContentData(Vector3 left, Vector3 right) {
            xLeft = left.x;
            yLeft = left.y;
            zLeft = left.z;

            xRight = right.x;
            yRight = right.y;
            zRight = right.z;
        }
        public CellContentData(Vector2 left, Vector2 right) {
            xLeft = left.x;
            yLeft = 0;
            zLeft = left.y;

            xRight = right.x;
            yRight = 0;
            zRight = right.y;
        }

        public CellContentData(EdgeAbstract edgeAbstract) : this(edgeAbstract.aPositionV3, edgeAbstract.bPositionV3) {}
        
        public bool Contains(Vector3 v3) {
            return 
                (xLeft == v3.x && yLeft == v3.y && zLeft == v3.z) | 
                (xRight == v3.x && yRight == v3.y && zRight == v3.z);
        }

        public void SwapEdges() {
            float tmp;

            tmp = xLeft;
            xLeft = xRight;
            xRight = tmp;

            tmp = yLeft;
            yLeft = yRight;
            yRight = tmp;

            tmp = zLeft;
            zLeft = zRight;
            zRight = tmp;
        }
        
        /// <summary>
        /// return nearest point on this segment Vector3
        /// </summary>
        public Vector3 NearestPoint(float positionX, float positionY, float positionZ) {
            //return SomeMath.NearestPointToSegment(xLeft, yLeft, zLeft, xRight, yRight, zRight, positionX, positionY, positionZ);

            float dirBx = xRight - xLeft;
            float dirBy = yRight - yLeft;
            float dirBz = zRight - zLeft;
            float tVal = (
                (positionX - xLeft) * dirBx +
                (positionY - yLeft) * dirBy +
                (positionZ - zLeft) * dirBz) /
                (dirBx * dirBx + dirBy * dirBy + dirBz * dirBz);

            Vector3 result;
            if (tVal < 0f) {
                result.x = xLeft;
                result.y = yLeft;
                result.z = zLeft;
            }
            else if (tVal > 1f) {
                result.x = xRight;
                result.y = yRight;
                result.z = zRight;
            }
            else {
                result.x = xLeft + dirBx * tVal;
                result.y = yLeft + dirBy * tVal;
                result.z = zLeft + dirBz * tVal;
            }
            return result;
        }
        /// <summary>
        /// return nearest point on this segment Vector3
        /// </summary>
        public Vector3 NearestPoint(Vector3 position) {
            return NearestPoint(position.x, position.y, position.z);
        }        

        public Vector3 NearestPointXZ(float positionX, float positionZ) {
            return SomeMath.ClosestToSegmentTopProjection(leftV3, rightV3, new Vector2(positionX, positionZ));
        }

        /// <summary>
        /// return ray intersection by top projection (if it occured)
        /// </summary>
        public bool RayIntersectXZ(float rayOriginX, float rayOriginZ, float rayDirectionX, float rayDirectionZ, out Vector3 result) {
            float Rx, Ry, Rz;
            bool Rb = SomeMath.RayIntersectXZ(rayOriginX, rayOriginZ, rayDirectionX, rayDirectionZ, xLeft, yLeft, zLeft, xRight, yRight, zRight, out Rx, out Ry, out Rz);
            result = new Vector3(Rx, Ry, Rz);
            return Rb;
        }
        public bool RayIntersectXZ(float rayOriginX, float rayOriginZ, float rayDirectionX, float rayDirectionZ, out float resultX, out float resultY, out float resultZ) {         
            return SomeMath.RayIntersectXZ(rayOriginX, rayOriginZ, rayDirectionX, rayDirectionZ, xLeft, yLeft, zLeft, xRight, yRight, zRight, out resultX, out resultY, out resultZ);
        }
        public bool RayIntersectXZ(float rayOriginX, float rayOriginZ, float rayDirectionX, float rayDirectionZ, out float resultX, out float resultZ) {
            return SomeMath.RayIntersectSegment(rayOriginX, rayOriginZ, rayDirectionX, rayDirectionZ, xLeft, zLeft, xRight, zRight, out resultX, out resultZ);
        }
        /// <summary>
        /// return ray intersection by top projection (if it occured)
        /// </summary>
        public bool RayIntersectXZ(Vector2 rayOrigin, Vector2 rayDirection, out Vector3 result) {
            return RayIntersectXZ(rayOrigin.x, rayOrigin.y, rayDirection.x, rayDirection.y, out result);
        }

        //thats a lot of arguments
        public bool LineIntersectXZ(      
            float leadingLineAx, float leadingLineAy, float leadingLineAz, //leading Line A
            float leadingLineBx, float leadingLineBy, float leadingLineBz, //leading Line B                                                                             
            out Vector3 lineIntersection) {
            float mainLineDirectionx = xRight - xLeft;
            float mainLineDirectiony = yRight - yLeft;
            float mainLineDirectionz = zRight - zLeft;
            float leadingLineDirectionx = leadingLineBx - leadingLineAx;   
            float leadingLineDirectionz = leadingLineBz - leadingLineAz;

            float denominator = (mainLineDirectionz * leadingLineDirectionx - mainLineDirectionx * leadingLineDirectionz);

            //paralel
            if (denominator == 0) {
                lineIntersection.x = lineIntersection.y = lineIntersection.z = 0f;
                return false;
            }

            float t = (
                (xLeft - leadingLineAx) * leadingLineDirectionz +
                (leadingLineAz - zLeft) * leadingLineDirectionx) / denominator;

            if (t >= 0f && t <= 1f) {
                lineIntersection = new Vector3(
                    xLeft + (mainLineDirectionx * t),
                    yLeft + (mainLineDirectiony * t),
                    zLeft + (mainLineDirectionz * t));

                //fancy way to check if intersection between leading leadingLineA and leadingLineB
                return
                    (leadingLineDirectionx * (lineIntersection.x - leadingLineAx)) +                       //dot product of intersection and leading line >= 0. Mean intersection if front of leading lline
                    (leadingLineDirectionz * (lineIntersection.z - leadingLineAz)) >= 0 &&                 //dot product of intersection and leading line >= 0. Mean intersection if front of leading lline
                                                                                                           //Check if intersection point closer to leading line A than leading line B
                    ((leadingLineBx - leadingLineAx) * (leadingLineBx - leadingLineAx)) + 
                    ((leadingLineBz - leadingLineAz) * (leadingLineBz - leadingLineAz)) >=         //sqr distance between leading lines
                    ((lineIntersection.x - leadingLineAx) * (lineIntersection.x - leadingLineAx)) + 
                    ((lineIntersection.z - leadingLineAz) * (lineIntersection.z - leadingLineAz)); //sqr distance between leading line a and intersection
            }
            else {
                lineIntersection.x = lineIntersection.y = lineIntersection.z = 0f;
                return false;
            }
        }


        public CellContentData Add(float posX, float posY, float posZ) {
            return new CellContentData(xLeft + posX, yLeft + posY, zLeft + posZ, xRight + posX, yRight + posY, zRight + posZ);
        }

        public CellContentData Add(Vector3 pos) {
            return Add(pos.x, pos.y, pos.z);
        }
        public CellContentData Add(Vector2 pos) {
            return Add(pos.x, 0, pos.y);
        }

        /// <summary>
        /// do right - leftm then take -y, x of it and return dot product of it with target x and y
        /// neat way to know is this line far or near if it used to cunstruct hull in clockwise
        /// essentially it is SomeMath.Dot(SomeMath.RotateRight(directionV2), new Vector2(x, y)) 
        /// </summary>
        public float RotateRightAndReturnDot(float x, float y) {
            return (-(zRight - zLeft) * x) + ((xRight - xLeft) * y);
        }

        /// <summary>
        /// return left to right direction
        /// </summary>
        public Vector3 directionV3 {
            get { return new Vector3(xRight - xLeft, yRight - yLeft, zRight - zLeft); }
        }
        /// <summary>
        /// return left to right direction by XZ
        /// </summary>
        public Vector2 directionV2 {
            get { return new Vector2(xRight - xLeft, zRight - zLeft); }
        }

        /// <summary>
        /// return center of line
        /// </summary>
        public Vector3 centerV3 {
            get { return new Vector3((xLeft + xRight) * 0.5f, (yLeft + yRight) * 0.5f, (zLeft + zRight) * 0.5f); }
        }
        /// <summary>
        /// return center of line by XZ
        /// </summary>
        public Vector2 centerV2 {
            get { return new Vector2((xLeft + xRight) * 0.5f, (zLeft + zRight) * 0.5f); }
        }


        /// <summary>
        /// x,y,z
        /// </summary>
        public Vector3 leftV3 {
            get { return new Vector3(xLeft, yLeft, zLeft); }
        }
        /// <summary>
        /// x,y,z
        /// </summary>
        public Vector3 rightV3 {
            get { return new Vector3(xRight, yRight, zRight); }
        }

        /// <summary>
        /// x,z
        /// </summary>
        public Vector2 leftV2 {
            get { return new Vector2(xLeft, zLeft); }
        }
        /// <summary>
        /// x,z
        /// </summary>
        public Vector2 rightV2 {
            get { return new Vector2(xRight, zRight); }
        }


        /// <summary>
        /// x,y,z
        /// </summary>
        public Vector3 midV3 {
            get { return new Vector3((xLeft + xRight) * 0.5f, (yLeft + yRight) * 0.5f, (zLeft + zRight) * 0.5f); }
        }
        /// <summary>
        /// x,z
        /// </summary>
        public Vector2 midV2 {
            get { return new Vector2((xLeft + xRight) * 0.5f, (zLeft + zRight) * 0.5f); }
        }

        /// <summary>
        /// left
        /// </summary>
        public Vector3 a {
            get { return new Vector3(xLeft, yLeft, zLeft); }
        }
        /// <summary>
        /// right
        /// </summary>
        public Vector3 b {
            get { return new Vector3(xRight, yRight, zRight); }
        }


        public static bool operator ==(CellContentData a, CellContentData b) {
            //return (a.leftV3 == b.leftV3 && a.rightV3 == b.rightV3) | (a.leftV3 == b.rightV3 && a.rightV3 == b.leftV3);
            return
                (a.xLeft == b.xLeft && a.yLeft == b.yLeft && a.zLeft == b.zLeft && a.xRight == b.xRight && a.yRight == b.yRight && a.zRight == b.zRight) |
                (a.xLeft == b.xRight && a.yLeft == b.yRight && a.zLeft == b.zRight && a.xRight == b.xLeft && a.yRight == b.yLeft && a.zRight == b.zLeft);
        }
        public static bool operator !=(CellContentData a, CellContentData b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            return (int)(xLeft + yLeft + zLeft + xRight + yRight + zRight);
        }

        public override bool Equals(object obj) {
            if (obj == null || !(obj is CellContentData))
                return false;

            return Equals((CellContentData)obj);
        }

        public bool Equals(CellContentData other) {
            return this == other;
        }

        public override string ToString() {
            return string.Format("(V1: {0} V2: {1})", leftV3, rightV3);
        }
    }

    //struct for storing content data and connection of 2 cells
    //-1 for empty connection
    [Serializable]
    public struct CellContentConnectionData : IEquatable<CellContentConnectionData> {
        public int from, connection;
        public CellContentData data;

        public CellContentConnectionData(int from, int connection, CellContentData data) {
            this.from = from;
            this.connection = connection;
            this.data = data;
        }

        public CellContentConnectionData(int from, int connection, Vector3 left, Vector3 right) : this(from, connection, new CellContentData(left, right)) { }

        public static bool operator ==(CellContentConnectionData a, CellContentConnectionData b) {
            return a.from == b.from && a.connection == b.connection && a.data == b.data;
        }
        public static bool operator !=(CellContentConnectionData a, CellContentConnectionData b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            return data.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (obj == null || !(obj is CellContentConnectionData))
                return false;
            return Equals((CellContentConnectionData)obj);
        }

        public bool Equals(CellContentConnectionData other) {
            return this == other;
        }

        public override string ToString() {
            return data.ToString();
        }
    }

    [Serializable]
    public struct CellContentRaycastData {
        public float
            xLeft, yLeft, zLeft, //lefts
            xRight, yRight, zRight; //rights       
        public int connection;

        public CellContentRaycastData(CellContentData data, int connection) {
            xLeft = data.xLeft;
            yLeft = data.yLeft;
            zLeft = data.zLeft;
            xRight = data.xRight;
            yRight = data.yRight;
            zRight = data.zRight;
            this.connection = connection;
        }

        public Vector2 NearestPointXZ(Vector2 position) {
            return SomeMath.NearestPointToSegment(xLeft, zLeft, xRight, zRight, position.x, position.y);
        }

        public Vector2 NearestPointXZ(float positionX, float positionZ) {
            return SomeMath.NearestPointToSegment(xLeft, zLeft, xRight, zRight, positionX, positionZ);
        }
    }

    public enum CellConnectionType : int {
        Invalid = 0,
        Generic = 1,
        jumpUp = 2,
        jumpDown = 3
    }

    [Serializable]
    public struct CellConnection {   
        public Vector3 intersection; //axis for jumps
        public float costFrom, costTo;
        public int from, connection; //-1 connection is no connection   
        public int veryImportantNumber;
        public int cellInternalIndex; //only valid if connection taked from cell
        
        /// <summary>
        /// generic
        /// </summary>
        public CellConnection(int from, int connection, float costFrom, float costTo, Vector3 intersection, Passability passabilityFrom, Passability passabilityConnection) {
            int value;
            veryImportantNumber = (int)CellConnectionType.Generic;
            value = (sbyte)passabilityFrom;
            veryImportantNumber |= value << 4;
            value = (sbyte)passabilityConnection;
            veryImportantNumber |= value << 8;

            //this.cellData = cellData;
            this.from = from;
            this.connection = connection;
            this.costFrom = costFrom;
            this.costTo = costTo;
            this.intersection = intersection;
            //this.passabilityFrom = passabilityFrom;
            //this.passabilityConnection = passabilityConnection;
            cellInternalIndex = -1;
        }

        /// <summary>
        /// jumps
        /// </summary>
        public CellConnection(Vector3 Axis, ConnectionJumpState JumpState, int From, int Connection, float costFrom, float costTo, Passability passabilityFrom, Passability passabilityConnection) {
            int value;
            veryImportantNumber = (int)(JumpState == ConnectionJumpState.jumpUp ? CellConnectionType.jumpUp : CellConnectionType.jumpDown);
            value = (sbyte)passabilityFrom;
            veryImportantNumber |= value << 4;
            value = (sbyte)passabilityConnection;
            veryImportantNumber |= value << 8;
            
            intersection = Axis;
            from = From;
            connection = Connection;

            this.costFrom = costFrom;
            this.costTo = costTo;
            cellInternalIndex = -1;
        }

        /// <summary>
        /// dummy connection for funnel
        /// </summary>
        public CellConnection(CellConnection con) {  
            veryImportantNumber = (int)CellConnectionType.Generic;
            int pass = (con.veryImportantNumber >> 8) & 15;      
            veryImportantNumber |= pass << 4;      
            veryImportantNumber |= pass << 8;

            cellInternalIndex = con.cellInternalIndex;
            from = connection = con.connection;
            intersection.x = intersection.y = intersection.z = costFrom = costTo = 0f;    
        }

        public CellConnectionType type {
            get { return (CellConnectionType)(veryImportantNumber & 15); }
        }

        public bool isGeneric {
            get { return (veryImportantNumber & 15) == (int)CellConnectionType.Generic; }
        }

        public Passability passabilityFrom {
            get { return (Passability)((veryImportantNumber >> 4) & 15); }
        }
        public Passability passabilityConnection {
            get { return (Passability)((veryImportantNumber >> 8) & 15); }
        }
        public bool difPassabilities {
            get { return ((veryImportantNumber >> 4) & 15) != ((veryImportantNumber >> 8) & 15); }
        }

        public float Cost(AgentProperties properties, bool ignoreCrouchCost) {
            float result = 0;

            //from
            int value = (veryImportantNumber >> 4) & 15; //passability from
            if(value == 3) //walkable most comonly used
                result += properties.walkMod * costFrom;
            else if(value == 2)//crouchable less common
                result += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * costFrom;
            else
                Debug.LogWarning("wrong passability in cost mod");

            //connection
            value = (veryImportantNumber >> 8) & 15; //passability connection
            if (value == 3) //walkable most comonly used
                result += properties.walkMod * costTo;
            else if (value == 2)//crouchable less common
                result += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * costTo;
            else
                Debug.LogWarning("wrong passability in cost mod");

            //jumps
            value = veryImportantNumber & 15;
            if (value == 2) //jump up
                result += properties.jumpUpMod;
            else if (value == 3) //jump down
                result += properties.jumpDownMod;

            return result;
        }

        public float Cost(Vector3 fromPos, AgentProperties properties, bool ignoreCrouchCost) {
            float result = 0;

            //from
            int value = (veryImportantNumber >> 4) & 15; //passability from
            if (value == 3) { //walkable most comonly used
                result += properties.walkMod * SomeMath.Distance(fromPos, intersection);
            }
            else if (value == 2)//crouchable less common
                result += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * Vector3.Distance(fromPos, intersection);
            else
                Debug.LogWarning("wrong passability in cost mod");

            //connection
            value = (veryImportantNumber >> 8) & 15; //passability connection
            if (value == 3) //walkable most comonly used
                result += properties.walkMod * costTo;
            else if (value == 2)//crouchable less common
                result += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * costTo;
            else
                Debug.LogWarning("wrong passability in cost mod");

            //jumps
            value = veryImportantNumber & 15;
            if (value == 2) //jump up
                result += properties.jumpUpMod;            
            else if (value == 3) //jump down
                result += properties.jumpDownMod;
            
            return result;
        }

        public void CostForPointSearch(AgentProperties properties, bool ignoreCrouchCost, out Vector3 point, out float costToPoint, out float costTotal) {
            point = intersection;
            costToPoint = 0;

            //from
            int value = (veryImportantNumber >> 4) & 15; //passability from
            if (value == 3) //walkable most comonly used
                costToPoint += properties.walkMod * costFrom;
            else if (value == 2)//crouchable less common
                costToPoint += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * costFrom;
            else
                Debug.LogWarning("wrong passability in cost mod");

            costTotal = costToPoint;

            //connection
            value = (veryImportantNumber >> 8) & 15; //passability connection
            if (value == 3) //walkable most comonly used
                costTotal += properties.walkMod * costTo;
            else if (value == 2)//crouchable less common
                costTotal += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * costTo;
            else
                Debug.LogWarning("wrong passability in cost mod");

            //jumps
            value = veryImportantNumber & 15;
            if (value == 2) //jump up
                costTotal += properties.jumpUpMod;
            else if (value == 3) //jump down
                costTotal += properties.jumpDownMod;
        }

        public void CostForPointSearch(Vector3 fromPos, AgentProperties properties, bool ignoreCrouchCost, out Vector3 point, out float costToPoint, out float costTotal) {
            point = intersection;
            costToPoint = 0;

            //from
            int value = (veryImportantNumber >> 4) & 15; //passability from
            if (value == 3) { //walkable most comonly used
                costToPoint += properties.walkMod * SomeMath.Distance(fromPos, intersection);
            }
            else if (value == 2)//crouchable less common
                costToPoint += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * Vector3.Distance(fromPos, intersection);
            else
                Debug.LogWarning("wrong passability in cost mod");

            costTotal = costToPoint;

            //connection
            value = (veryImportantNumber >> 8) & 15; //passability connection
            if (value == 3) //walkable most comonly used
                costTotal += properties.walkMod * costTo;
            else if (value == 2)//crouchable less common
                costTotal += (ignoreCrouchCost ? properties.walkMod : properties.crouchMod) * costTo;
            else
                Debug.LogWarning("wrong passability in cost mod");

            //jumps
            value = veryImportantNumber & 15;
            if (value == 2) //jump up
                costTotal += properties.jumpUpMod;
            else if (value == 3) //jump down
                costTotal += properties.jumpDownMod;
        }

        //public Vector3 enterPoint { get { return cellData.leftV3; } }
        //public Vector3 axis { get { return intersection; } }
        //public Vector3 exitPoint { get { return cellData.rightV3; } }

        public static CellConnection invalid {
            get { return new CellConnection(); }
        }

        public static bool operator ==(CellConnection a, CellConnection b) {
            return 
                a.type == b.type && a.costFrom == b.costFrom && 
                a.costTo == b.costTo && a.intersection == b.intersection &&     
                a.from == b.from && 
                a.connection == b.connection;
        }
        public static bool operator !=(CellConnection a, CellConnection b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            return from + veryImportantNumber + (int)type;
        }

        public override bool Equals(object obj) {
            if (obj == null || !(obj is CellConnection))
                return false;

            return Equals((CellConnection)obj);
        }

        public bool Equals(CellConnection other) {
            return this == other;
        }
    }

    //big struct
    public struct CellTempJumpConnection {
        public bool jumpUp;
        public Vector3 enter, axis, exit;
        public Cell from, connection;

        public CellTempJumpConnection(bool jumpUp, Cell from, Cell connection, Vector3 enter, Vector3 axis, Vector3 exit) {
            this.jumpUp = jumpUp;
            this.enter = enter;
            this.axis = axis;
            this.exit = exit;
            this.from = from;
            this.connection = connection;
            //PFDebuger.Debuger_K.AddLabel(enter, "enter");
            //PFDebuger.Debuger_K.AddLabel(axis, "axis");
            //PFDebuger.Debuger_K.AddLabel(exit, "exit");
        }
    }
}