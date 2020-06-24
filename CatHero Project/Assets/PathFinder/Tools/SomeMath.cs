using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//NOTE:
// this class should exclude it's static constructor. mean:
// 1) no constuctor
// 2) no static or const variables
//also prefere copy-paste code rather than using it in critical places

//TODO:
// 1)line functions is mess. i need to fix it
// 2)i need to investagate how should i perform sqrt

namespace K_PathFinder {
    public static class SomeMath {
        public enum Axis {
            x,y,z
        }
        public enum Axises {
            xy, xz, yz
        }

        //public static float Angle(Vector3 A, Vector3 B) {
        //    return Mathf.Acos(Mathf.Clamp(Vector3.Dot(A.normalized, B.normalized), -1f, 1f)) * 57.29578f;
        //}
        //public static float Angle(Vector2 A, Vector2 B) {
        //    return Mathf.Acos(Mathf.Clamp(Vector2.Dot(A.normalized, B.normalized), -1f, 1f)) * 57.29578f;
        //}

        //public static float Angle(float ax, float ay, float bx, float by) {
        //    float aMag = Mathf.Sqrt((ax * ax) + (ay * ay));
        //    float bMag = Mathf.Sqrt((bx * bx) + (by * by));
        //    float dot = ((ax / aMag) * (bx / bMag)) + ((ay / aMag) * (by * bMag));
        //    if (dot < -1f)
        //        dot = -1f;
        //    else if (dot > 1f)
        //        dot = 1f;
        //    return Mathf.Acos(dot) * 57.29578f; //180f / PI
        //}



        #region sqr distance
        //**************Vector2**************//
        public static int SqrDistance(int ax, int ay, int bx, int by) {
            return 
                ((bx - ax) * (bx - ax)) + 
                ((by - ay) * (by - ay));
        }
        public static float SqrDistance(float ax, float ay, float bx, float by) {
            return 
                ((bx - ax) * (bx - ax)) + 
                ((by - ay) * (by - ay));
        }
        public static float SqrDistance(Vector2 a, Vector2 b) {
            return
                ((b.x - a.x) * (b.x - a.x)) +
                ((b.y - a.y) * (b.y - a.y));
        }
        public static float SqrDistance(float ax, float ay, Vector2 b) {
            return
                ((b.x - ax) * (b.x - ax)) +
                ((b.y - ay) * (b.y - ay));
        }
        //**************Vector2**************//

        //**************Vector3**************//
        public static float SqrDistance(float ax, float ay, float az, float bx, float by, float bz) {
            return 
                ((bx - ax) * (bx - ax)) + 
                ((by - ay) * (by - ay)) + 
                ((bz - az) * (bz - az));
        }
        public static float SqrDistance(float ax, float ay, float az, Vector3 b) {
            return
                ((b.x - ax) * (b.x - ax)) +
                ((b.y - ay) * (b.y - ay)) +
                ((b.z - az) * (b.z - az));
        }
        public static float SqrDistance(Vector3 a, Vector3 b) {
            return 
                ((b.x - a.x) * (b.x - a.x)) + 
                ((b.y - a.y) * (b.y - a.y)) + 
                ((b.z - a.z) * (b.z - a.z));
        }
        //**************Vector3**************//
        #endregion

        #region distance
        public static float Distance(float ax, float ay, float az, float bx, float by, float bz) {
            return (float)Math.Sqrt(
                ((bx - ax) * (bx - ax)) + 
                ((by - ay) * (by - ay)) + 
                ((bz - az) * (bz - az)));
        }
        public static float Distance(Vector3 a, Vector3 b) {
            return (float)Math.Sqrt(
                ((b.x - a.x) * (b.x - a.x)) +
                ((b.y - a.y) * (b.y - a.y)) +
                ((b.z - a.z) * (b.z - a.z)));
        }

        public static float Distance(float ax, float ay, float bx, float by) {
            return Mathf.Sqrt(
                ((bx - ax) * (bx - ax)) + 
                ((by - ay) * (by - ay)));
        }
        public static float Distance(Vector2 a, Vector2 b) {
            return Mathf.Sqrt(
                ((b.x - a.x) * (b.x - a.x)) + 
                ((b.y - a.y) * (b.y - a.y)));
        }
        #endregion

        #region sqr magnitude
        public static float SqrMagnitude(float x, float y) {
            return (x * x) + (y * y);
        }
        public static float SqrMagnitude(Vector2 vector) {
            return (vector.x * vector.x) + (vector.y * vector.y);
        }

        public static float SqrMagnitude(float x, float y, float z) {
            return (x * x) + (y * y) + (z * z);
        }
        public static float SqrMagnitude(Vector3 vector) {
            return (vector.x * vector.x) + (vector.y * vector.y) + (vector.z * vector.z);
        }
        #endregion

        #region magnitude
        public static float Magnitude(float x, float y) {
            return Mathf.Sqrt(SqrMagnitude(x, y));
        }
        public static float Magnitude(Vector2 vec) {
            return Mathf.Sqrt(SqrMagnitude(vec.x, vec.y));
        }
        public static float Magnitude(float x, float y, float z) {
            return Mathf.Sqrt(SqrMagnitude(x, y, z));
        }
        public static float Magnitude(Vector3 vec) {
            return Mathf.Sqrt(SqrMagnitude(vec.x, vec.y, vec.z));
        }
        #endregion

        #region normalize
        public static void Normalize(ref float x, ref float y) {
            float m = Magnitude(x, y);
            x = x / m;
            y = y / m;
        }
        public static void Normalize(ref float x, ref float y, ref float z) {
            float m = Magnitude(x, y);
            x = x / m;
            y = y / m;
            z = z / m;
        }
        #endregion

        #region cross, dot
        public static float V2Cross(Vector2 left, Vector2 right) {
            return (left.y * right.x) - (left.x * right.y);
        }
        public static float V2Cross(float Ax, float Ay, float Bx, float By) {
            return (Ay * Bx) - (Ax * By);
        }
        public static float Dot(Vector2 A, Vector2 B) {
            return (A.x * B.x) + (A.y * B.y);
        }
        public static float Dot(float Ax, float Ay, float Bx, float By) {
            return (Ax * Bx) + (Ay * By);
        }
        public static float Dot(Vector3 A, Vector3 B) {
            return (A.x * B.x) + (A.y * B.y) + (A.z * B.z);
        }
        public static float Dot(float Ax, float Ay, float Az, float Bx, float By, float Bz) {
            return (Ax * Bx) + (Ay * By) + (Az * Bz);
        }

        public static Vector3 Cross(Vector3 A, Vector3 B) {
            return new Vector3(
                A.y * B.z - A.z * B.y,
                A.z * B.x - A.x * B.z,
                A.x * B.y - A.y * B.x);
        }
        #endregion
        
        #region min max
        //2 parameters
        public static float Min(float a, float b) {
            return a < b ? a : b;
        }
        public static float Max(float a, float b) {
            return a > b ? a : b;
        }
        public static int Min(int a, int b) {
            return a < b ? a : b;
        }
        public static int Max(int a, int b) {
            return a > b ? a : b;
        }
        public static sbyte Min(sbyte a, sbyte b) {
            return a < b ? a : b;
        }
        public static sbyte Max(sbyte a, sbyte b) {
            return a > b ? a : b;
        }

        //3 parameters
        public static float Min(float a, float b, float c) {
            a = a < b ? a : b;
            return a < c ? a : c;
        }
        public static float Max(float a, float b, float c) {
            a = a > b ? a : b;
            return a > c ? a : c;
        }
        public static int Min(int a, int b, int c) {
            a = a < b ? a : b;
            return a < c ? a : c;
        }
        public static int Max(int a, int b, int c) {
            a = a > b ? a : b;
            return a > c ? a : c;
        }
        #endregion

        #region misc
        public static float Sqr(float value) {
            return value * value;
        }
        public static int Sqr(int value) {
            return value * value;
        }
        public static int Difference(int a, int b) {
            a = a - b;
            if (a < 0) a *= -1;
            return a;
        }
        public static float Difference(float a, float b) {
            a = a - b;
            if (a < 0) a *= -1;
            return a;
        }
        public static bool AllOnOneSideOfZero(float val1, float val2) {
            return (val1 > 0f && val2 > 0f) | (val1 <= 0f && val2 <= 0f);
        }
        public static int Clamp(int min, int max, int value) {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
        public static float Clamp(float min, float max, float value) {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        //first inclussive and last exclussive
        public static bool InRangeArrayLike(int value, int min, int max) {
            return value < max && value >= max;
        }
        public static bool InRangeExclusive(int value, int min, int max) {
            return value > min && value < max;
        }
        public static bool InRangeExclusive(float value, float min, float max) {
            return value > min && value < max;
        }
        public static bool InRangeInclusive(int value, int min, int max) {
            return value >= min && value <= max;
        }
        public static bool InRangeInclusive(float value, float min, float max) {
            return value >= min && value <= max;
        }
        public static bool InRangeInclusive(float value1, float value2, float min, float max) {
            return (value1 >= min && value1 <= max) | (value2 >= min && value2 <= max);
        }

        public static Vector2 RotateRight(Vector2 vector) {
            return new Vector2(-vector.y, vector.x);
        }
        public static Vector2 RotateRight(float x, float y) {
            return new Vector2(-y, x);
        }
        public static Vector2 RotateLeft(Vector2 vector) {
            return new Vector2(vector.y, -vector.x);
        }
        public static Vector2 RotateLeft(float x, float y) {
            return new Vector2(y, -x);
        }
        #endregion



        public static Vector3 TwoVertexNormal(Vector3 first, Vector3 second) {
            return (first.z * second.x) - (first.x * second.z) < 0 ?
                (first.normalized + second.normalized).normalized * -1 :
                (first.normalized + second.normalized).normalized;
        }



        //public static bool PointInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 po) {
        //    float s = a.y * c.x - a.x * c.y + (c.y - a.y) * po.x + (a.x - c.x) * po.y;
        //    float t = a.x * b.y - a.y * b.x + (a.y - b.y) * po.x + (b.x - a.x) * po.y;

        //    if ((s <= 0) != (t <= 0))
        //        return false;

        //    float A = -b.y * c.x + a.y * (c.x - b.x) + a.x * (b.y - c.y) + b.x * c.y;
        //    if (A < 0.0) {
        //        s = -s;
        //        t = -t;
        //        A = -A;
        //    }
        //    return s > 0 && t > 0 && (s + t) < A;
        //}
        
        public static bool PointInTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P) {
            return PointInTriangle(A.x, A.y, B.x, B.y, C.x, C.y, P.x, P.y);
        }

        public static bool PointInTriangle(float Ax, float Ay, float Bx, float By, float Cx, float Cy, float Px, float Py) {
            float cxDir = Cx - Ax;
            float cyDir = Cy - Ay;
            float byDir = By - Ay;
            float pyDir = Py - Ay;

            float w1 = (Ax * cyDir + pyDir * cxDir - Px * cyDir) / (byDir * cxDir - (Bx - Ax) * cyDir);
            float w2 = (pyDir - w1 * byDir) / cyDir;
            return w1 >= 0 && w2 >= 0 && (w1 + w2) <= 1;
        }

        //- is right 
        //+ is left
        public static float LineSide(Vector2 A, Vector2 B, Vector2 P) {
            return (B.x - A.x) * (P.y - A.y) - (B.y - A.y) * (P.x - A.x);
        }
        //- is right 
        //+ is left
        public static float LineSide(float Ax, float Ay, float Bx, float By, float pointX, float pointY) {
            return (Bx - Ax) * (pointY - Ay) - (By - Ay) * (pointX - Ax);
        }

        public static bool PointInTriangleClockwise(Vector2 A, Vector2 B, Vector2 C, float pointX, float pointY) {
            return PointInTriangleClockwise(A.x, A.y, B.x, B.y, C.x, C.y, pointX, pointY);
        }

        public static bool PointInTriangleClockwise(float Ax, float Ay, float Bx, float By, float Cx, float Cy, float pointX, float pointY) {
            return
                LineSide(Ax, Ay, Bx, By, pointX, pointY) <= 0f &&
                LineSide(Bx, By, Cx, Cy, pointX, pointY) <= 0f &&
                LineSide(Cx, Cy, Ax, Ay, pointX, pointY) <= 0f;
        }

        public static float CalculateHeight(Vector3 A, Vector3 B, Vector3 C, float x, float z) {
            float det = (B.z - C.z) * (A.x - C.x) + (C.x - B.x) * (A.z - C.z);
            float l1 = ((B.z - C.z) * (x - C.x) + (C.x - B.x) * (z - C.z)) / det;
            float l2 = ((C.z - A.z) * (x - C.x) + (A.x - C.x) * (z - C.z)) / det;
            float l3 = 1.0f - l1 - l2;
            return l1 * A.y + l2 * B.y + l3 * C.y;
        }

        public static float CalculateHeight(float Ax, float Ay, float Az, float Bx, float By, float Bz, float Cx,float Cy, float Cz, float x, float z) {
            float det = (Bz - Cz) * (Ax - Cx) + (Cx - Bx) * (Az - Cz);
            float l1 = ((Bz - Cz) * (x - Cx) + (Cx - Bx) * (z - Cz)) / det;
            float l2 = ((Cz - Az) * (x - Cx) + (Ax - Cx) * (z - Cz)) / det;
            float l3 = 1.0f - l1 - l2;
            return l1 * Ay + l2 * By + l3 * Cy;
        }

        #region nearest point to line/segment
        //Vector3
        //nearest on line
        public static Vector3 NearestPointToLine(float lineAx, float lineAy, float lineAz, float lineBx, float lineBy, float lineBz, float pointx, float pointy, float pointz) {
            float dirBx = lineBx - lineAx;
            float dirBy = lineBy - lineAy;
            float dirBz = lineBz - lineAz;
            float tVal = (
                (pointx - lineAx) * dirBx + 
                (pointy - lineAy) * dirBy + 
                (pointz - lineAz) * dirBz) / 
                (dirBx * dirBx + dirBy * dirBy + dirBz * dirBz);

            return new Vector3(lineAx + dirBx * tVal, lineAy + dirBy * tVal, lineAz + dirBz * tVal);
        }
        public static Vector3 NearestPointToLine(Vector3 A, Vector3 B, Vector3 point) {
            return NearestPointToLine(A.x, A.y, A.z, B.x, B.y, B.z, point.x, point.y, point.z);
        }
        
        //Vector3
        public static Vector3 NearestPointToSegment(float lineAx, float lineAy, float lineAz, float lineBx, float lineBy, float lineBz, float pointx, float pointy, float pointz) {
            float dirBx = lineBx - lineAx;
            float dirBy = lineBy - lineAy;
            float dirBz = lineBz - lineAz;
            float tVal = (
                (pointx - lineAx) * dirBx +
                (pointy - lineAy) * dirBy +
                (pointz - lineAz) * dirBz) /
                (dirBx * dirBx + dirBy * dirBy + dirBz * dirBz);

            Vector3 result;
            if (tVal < 0f) {
                result.x = lineAx;
                result.y = lineAy;
                result.z = lineAz;
            }
            else if (tVal > 1f) {
                result.x = lineBx;
                result.y = lineBy;
                result.z = lineBz;
            }
            else {
                result.x = lineAx + dirBx * tVal;
                result.y = lineAy + dirBy * tVal;
                result.z = lineAz + dirBz * tVal;
            }
            return result;
        }
        public static Vector3 NearestPointToSegment(Vector3 A, Vector3 B, Vector3 point) {
            return NearestPointToSegment(A.x, A.y, A.z, B.x, B.y, B.z, point.x, point.y, point.z);
        }

        //Vector2
        //nearest on line
        public static void NearestPointToLine(float lineAx, float lineAy, float lineBx, float lineBy, float pointX, float pointY, out float resultX, out float resultY) {
            float dirBx = lineBx - lineAx;
            float dirBy = lineBy - lineAy;
            //float dirPx = P.x - A.x;
            //float dirPy = P.y - A.y;
            //float dot1 = dirBx * dirBx + dirBy * dirBy;
            //float dot2 = dirPx * dirBx + dirPy * dirBy;
            //float tVal = dot2 / dot1;   
            float tVal = ((pointX - lineAx) * dirBx + (pointY - lineAy) * dirBy) / (dirBx * dirBx + dirBy * dirBy);
            resultX = lineAx + dirBx * tVal;
            resultY = lineAy + dirBy * tVal;
        }
        public static void NearestPointToLine(Vector2 A, Vector2 B, Vector2 point, out float resultX, out float resultY) {
            NearestPointToLine(A.x, A.y, B.x, B.y, point.x, point.y, out resultX, out resultY);
        }

        public static Vector2 NearestPointToLine(float lineAx, float lineAy, float lineBx, float lineBy, float pointX, float pointY) {
            float dirBx = lineBx - lineAx;
            float dirBy = lineBy - lineAy;
            float tVal = ((pointX - lineAx) * dirBx + (pointY - lineAy) * dirBy) / (dirBx * dirBx + dirBy * dirBy);
            return new Vector2(lineAx + dirBx * tVal, lineAy + dirBy * tVal);
        }
        public static Vector2 NearestPointToLine(Vector2 A, Vector2 B, Vector2 point) {
            return NearestPointToLine(A.x, A.y, B.x, B.y, point.x, point.y);
        }

        //nearest on segment
        public static void NearestPointToSegment(float lineAx, float lineAy, float lineBx, float lineBy, float pointX, float pointY, out float resultX, out float resultY) {
            float dirBx = lineBx - lineAx;
            float dirBy = lineBy - lineAy;
            float tVal = ((pointX - lineAx) * dirBx + (pointY - lineAy) * dirBy) / (dirBx * dirBx + dirBy * dirBy);
            if (tVal < 0f) tVal = 0f;
            else if (tVal > 1f) tVal = 1f;
            resultX = lineAx + dirBx * tVal;
            resultY = lineAy + dirBy * tVal;
        }
        public static void NearestPointToSegment(Vector2 A, Vector2 B, Vector2 point, out float resultX, out float resultY) {
            NearestPointToSegment(A.x, A.y, B.x, B.y, point.x, point.y, out resultX, out resultY);
        }

        public static Vector2 NearestPointToSegment(float lineAx, float lineAy, float lineBx, float lineBy, float pointX, float pointY) {
            float dirBx = lineBx - lineAx;
            float dirBy = lineBy - lineAy;
            float tVal = ((pointX - lineAx) * dirBx + (pointY - lineAy) * dirBy) / (dirBx * dirBx + dirBy * dirBy);
            if (tVal < 0f) tVal = 0f;
            else if (tVal > 1f) tVal = 1f;
            return new Vector2(lineAx + dirBx * tVal, lineAy + dirBy * tVal);
        }
        public static Vector2 NearestPointToSegment(Vector2 A, Vector2 B, Vector2 point) {
            return NearestPointToSegment(A.x, A.y, B.x, B.y, point.x, point.y);
        }
        #endregion


        public static float TriangleArea(Vector2 a, Vector2 b, Vector2 c) {
            return Math.Abs(Vector3.Cross(b - a, c - a).z) * 0.5f;
        }

        #region not-a-k_math-still-good
        //public static Vector3 ProjectPointOnLineSegment(Vector3 linePoint1, Vector3 linePoint2, Vector3 point) {

        //    Vector3 vector = linePoint2 - linePoint1;

        //    Vector3 projectedPoint = ProjectPointOnLine(linePoint1, vector.normalized, point);

        //    int side = PointOnWhichSideOfLineSegment(linePoint1, linePoint2, projectedPoint);
        //    Debug.Log(side);

        //    //The projected point is on the line segment
        //    if (side == 0) {

        //        return projectedPoint;
        //    }

        //    if (side == 1) {

        //        return linePoint1;
        //    }

        //    if (side == 2) {

        //        return linePoint2;
        //    }

        //    //output is invalid
        //    return Vector3.zero;
        //}
        //public static Vector3 ProjectPointOnLine(Vector3 linePoint, Vector3 lineVec, Vector3 point) {

        //    //get vector from point on line to point in space
        //    Vector3 linePointToPoint = point - linePoint;

        //    float t = Vector3.Dot(linePointToPoint, lineVec);

        //    return linePoint + lineVec * t;
        //}
        //public static int PointOnWhichSideOfLineSegment(Vector3 linePoint1, Vector3 linePoint2, Vector3 point) {

        //    Vector3 lineVec = linePoint2 - linePoint1;
        //    Vector3 pointVec = point - linePoint1;

        //    float dot = Vector3.Dot(pointVec, lineVec);

        //    //point is on side of linePoint2, compared to linePoint1
        //    if (dot > 0) {

        //        //point is on the line segment
        //        if (pointVec.magnitude <= lineVec.magnitude) {

        //            return 0;
        //        }

        //        //point is not on the line segment and it is on the side of linePoint2
        //        else {

        //            return 2;
        //        }
        //    }

        //    //Point is not on side of linePoint2, compared to linePoint1.
        //    //Point is not on the line segment and it is on the side of linePoint1.
        //    else {

        //        return 1;
        //    }
        //}
        #endregion

        public static Vector3 MidPoint(Vector3 a, Vector3 b) {
            return (a + b) * 0.5f;
        }
        public static Vector3 MidPoint(params Vector3[] input) {
            Vector3 output = Vector3.zero;
            foreach (var item in input)
                output += item;

            return output / input.Length;
        }        
        public static Vector3 MidPoint(IEnumerable<Vector3> input) {
            Vector3 output = Vector3.zero;
            foreach (var item in input)
                output += item;

            return output / input.Count();
        }
        public static Vector2 MidPoint(Vector2 a, Vector2 b) {
            return (a + b) * 0.5f;
        }
        public static Vector2 MidPoint(Vector2[] input, int count) {   
            float x = 0, y = 0;

            for (int i = 0; i < count; i++) {
                Vector2 vector = input[i];
                x += vector.x;
                y += vector.y;
            }
 
            return new Vector2(x / count, y / count);
        }

        public static Vector2 MidPoint(params Vector2[] input) {
            return MidPoint(input, input.Length);
        }

        public static Vector2 MidPoint(IEnumerable<Vector2> input) {
            Vector2 output = Vector2.zero;
            foreach (var item in input)
                output += item;

            return output / input.Count();
        }

        public static List<Vector2> DouglasPeucker(List<Vector2> points, int startIndex, int lastIndex, float epsilon) {
            float dmax = 0f;
            int index = startIndex;

            for (int i = index + 1; i < lastIndex; ++i) {
                float d = PointLineDistance(points[i], points[startIndex], points[lastIndex]);
                if (d > dmax) {
                    index = i;
                    dmax = d;
                }
            }

            if (dmax > epsilon) {
                var res1 = DouglasPeucker(points, startIndex, index, epsilon);
                var res2 = DouglasPeucker(points, index, lastIndex, epsilon);

                var finalRes = new List<Vector2>();
                for (int i = 0; i < res1.Count - 1; ++i) {
                    finalRes.Add(res1[i]);
                }

                for (int i = 0; i < res2.Count; ++i) {
                    finalRes.Add(res2[i]);
                }

                return finalRes;
            }
            else {
                return new List<Vector2>(new Vector2[] { points[startIndex], points[lastIndex] });
            }
        }

        public static float PointLineDistance(Vector2 point, Vector2 start, Vector2 end) {
            if (start == end) {
                return Vector2.Distance(point, start);
            }

            float n = Mathf.Abs((end.x - start.x) * (start.y - point.y) - (start.x - point.x) * (end.y - start.y));
            float d = Mathf.Sqrt((end.x - start.x) * (end.x - start.x) + (end.y - start.y) * (end.y - start.y));

            return n / d;
        }

        public static List<Vector3> DouglasPeucker(List<Vector3> points, int startIndex, int lastIndex, float epsilon) {
            float dmax = 0f;
            int index = startIndex;

            for (int i = index + 1; i < lastIndex; ++i) {
                float d = Vector3.Distance(NearestPointToLine(points[startIndex], points[lastIndex], points[i]), points[i]);
                if (d > dmax) {
                    index = i;
                    dmax = d;
                }
            }

            if (dmax > epsilon) {
                var res1 = DouglasPeucker(points, startIndex, index, epsilon);
                var res2 = DouglasPeucker(points, index, lastIndex, epsilon);

                var finalRes = new List<Vector3>();
                for (int i = 0; i < res1.Count - 1; ++i) {
                    finalRes.Add(res1[i]);
                }

                for (int i = 0; i < res2.Count; ++i) {
                    finalRes.Add(res2[i]);
                }

                return finalRes;
            }
            else {
                return new List<Vector3>(new Vector3[] { points[startIndex], points[lastIndex] });
            }
        }



        #region projection
        //public static Vector3 ClosestToLineTopProjection(Vector3 lineA, Vector3 lineB, Vector2 point) {
        //    Vector3 pointV3 = new Vector3(point.x, 0, point.y);
        //    Vector3 lineVec1 = lineB - lineA;
        //    Vector3 lineVec2 = Vector3.down;

        //    float a = Vector3.Dot(lineVec1, lineVec1);
        //    float b = Vector3.Dot(lineVec1, lineVec2);
        //    float e = Vector3.Dot(lineVec2, lineVec2);

        //    float d = a * e - b * b;

        //    if (d == 0f)
        //        Debug.LogError("Lines are paralel");

        //    Vector3 r = lineA - pointV3;
        //    float c = Vector3.Dot(lineVec1, r);
        //    float f = Vector3.Dot(lineVec2, r);
        //    float s = (b * f - c * e) / d;

        //    return lineA + lineVec1 * s;
        //}

        ////some black magick i cant remember how it works
        //public static bool ClosestToSegmentTopProjection(Vector3 lineA, Vector3 lineB, Vector2 point, out Vector3 intersection) {
        //    Vector3 pointV3 = new Vector3(point.x, 0, point.y);
        //    Vector3 lineVec1 = lineB - lineA;
        //    Vector3 lineVec2 = Vector3.down;

        //    float a = Vector3.Dot(lineVec1, lineVec1);
        //    float b = Vector3.Dot(lineVec1, lineVec2);
        //    float e = Vector3.Dot(lineVec2, lineVec2);

        //    float d = a * e - b * b;

        //    if (d == 0f) {
        //        intersection = Vector3.zero;
        //        return false;
        //    }

        //    Vector3 r = lineA - pointV3;
        //    float c = Vector3.Dot(lineVec1, r);
        //    float f = Vector3.Dot(lineVec2, r);
        //    float s = (b * f - c * e) / d;

        //    intersection = lineA + lineVec1 * s;
        //    return s >= 0 & s <= 1f;
        //}



        //public static bool ClosestToSegmentTopProjection(Vector3 lineA, Vector3 lineB, Vector2 point, bool clamp, out Vector3 intersection) {
        //    Vector3 pointV3 = new Vector3(point.x, 0, point.y);
        //    Vector3 lineVec1 = lineB - lineA;
        //    Vector3 lineVec2 = Vector3.down;

        //    float a = Vector3.Dot(lineVec1, lineVec1);
        //    float b = Vector3.Dot(lineVec1, lineVec2);
        //    float e = Vector3.Dot(lineVec2, lineVec2);

        //    float d = a * e - b * b;

        //    if (d == 0f) {
        //        intersection = Vector3.zero;
        //        return false;
        //    }

        //    Vector3 r = lineA - pointV3;
        //    float c = Vector3.Dot(lineVec1, r);
        //    float f = Vector3.Dot(lineVec2, r);
        //    float s = (b * f - c * e) / d;


        //    if (clamp) {
        //        s = Mathf.Clamp01(s);
        //        intersection = lineA + lineVec1 * s;
        //        return true;
        //    }
        //    else {
        //        intersection = lineA + lineVec1 * s;
        //        return s >= 0 & s <= 1f;
        //    }
        //}

        public static Vector3 ClosestToLineTopProjection(Vector3 lineA, Vector3 lineB, Vector2 point) {
            float dirBx = lineB.x - lineA.x;
            float dirBy = lineB.y - lineA.y;
            float dirBz = lineB.z - lineA.z;
            float tVal = ((point.x - lineA.x) * dirBx + (point.y - lineA.z) * dirBz) / (dirBx * dirBx + dirBz * dirBz);
            return new Vector3(dirBx * tVal + lineA.x, dirBy * tVal + lineA.y, dirBz * tVal + lineA.z);
        }
        public static Vector3 ClosestToSegmentTopProjection(Vector3 lineA, Vector3 lineB, Vector2 point) {
            float dirBx = lineB.x - lineA.x;
            float dirBy = lineB.y - lineA.y;
            float dirBz = lineB.z - lineA.z;
            float tVal = ((point.x - lineA.x) * dirBx + (point.y - lineA.z) * dirBz) / (dirBx * dirBx + dirBz * dirBz);
            if (tVal < 0f) tVal = 0f;
            else if (tVal > 1f) tVal = 1f;
            return new Vector3(dirBx * tVal + lineA.x, dirBy * tVal + lineA.y, dirBz * tVal + lineA.z);
        }
        #endregion

        private static bool RayIntersectXZ_for_troubleshooting(
            float rayOriginX, float rayOriginZ, float rayDirectionX, float rayDirectionZ,
            float lineA_x, float lineA_y, float lineA_z, float lineB_x, float lineB_y, float lineB_z,
            out Vector3 intersectRay, out Vector3 intersectLine, out float tVal, out float dot) {
            float lineDir_x = lineB_x - lineA_x;
            float lineDir_y = lineB_y - lineA_y;
            float lineDir_z = lineB_z - lineA_z;
            float denominator = (lineDir_z * rayDirectionX - lineDir_x * rayDirectionZ);

            //paralel
            if (denominator == 0) {
                intersectLine = new Vector3();
                intersectRay = new Vector3();
                tVal = 0;
                dot = 0;
                return false;
            }

            tVal = ((lineA_x - rayOriginX) * rayDirectionZ + (rayOriginZ - lineA_z) * rayDirectionX) / denominator;

            intersectLine = new Vector3(
                lineA_x + (lineDir_x * tVal),
                lineA_y + (lineDir_y * tVal),
                lineA_z + (lineDir_z * tVal));

            float tValClamped = Clamp(0f, 1f, tVal);

            intersectRay = new Vector3(
                lineA_x + (lineDir_x * tValClamped),
                lineA_y + (lineDir_y * tValClamped),
                lineA_z + (lineDir_z * tValClamped));

            dot =
                (rayDirectionX * (intersectLine.x - rayOriginX)) +
                (rayDirectionZ * (intersectLine.z - rayOriginZ));

            return tVal >= 0f && tVal <= 1f && dot > 0;
        }


        #region something intersect something

        #region ray intersect segment
        public static bool RayIntersectSegment(
            float rayOrigin_x, float rayOrigin_y, 
            float rayDirection_x, float rayDirection_y,
            float segmentA_x, float segmentA_y, 
            float segmentB_x, float segmentB_y,
            out float intersect_x, 
            out float intersect_y) {
            float lineDir_x = segmentB_x - segmentA_x;
            float lineDir_y = segmentB_y - segmentA_y;
            float denominator = lineDir_y * rayDirection_x - lineDir_x * rayDirection_y;

            //paralel
            if (denominator == 0) {
                intersect_x = intersect_y = 0;
                return false;
            }

            float t = ((segmentA_x - rayOrigin_x) * rayDirection_y + (rayOrigin_y - segmentA_y) * rayDirection_x) / denominator;

            if (t >= 0f && t <= 1f) {
                intersect_x = segmentA_x + (lineDir_x * t);
                intersect_y = segmentA_y + (lineDir_y * t);

                float dot =
                    (rayDirection_x * (intersect_x - rayOrigin_x)) +
                    (rayDirection_y * (intersect_y - rayOrigin_y));

                if (dot > 0)
                    return true;
                else {
                    intersect_x = intersect_y = 0;
                    return false;
                }
            }
            else {
                intersect_x = intersect_y = 0;
                return false;
            }
        }

        public static bool RayIntersectSegment(Vector2 rayOrigin, Vector2 rayDirection, Vector2 segmentA, Vector2 segmentB, out Vector2 intersection) {
            float resultX, resultY;
            bool result = RayIntersectSegment(rayOrigin.x, rayOrigin.y, rayDirection.x, rayDirection.y, segmentA.x, segmentA.y, segmentB.x, segmentB.y, out resultX, out resultY);
            intersection = new Vector2(resultX, resultY);
            return result;
        }

        /// <summary>
        /// slightly simplified version for cases when ray starts from 0,0
        /// </summary>
        public static bool RayIntersectSegment(         
            float rayDirectionX, float rayDirectionY,
            float segmentA_x, float segmentA_y,
            float segmentB_x, float segmentB_y,
            out float intersect_x,
            out float intersect_y) {
            float lineDir_x = segmentB_x - segmentA_x;
            float lineDir_y = segmentB_y - segmentA_y;
            float denominator = lineDir_y * rayDirectionX - lineDir_x * rayDirectionY;

            //paralel
            if (denominator == 0) {
                intersect_x = intersect_y = 0;
                return false;
            }

            float t = (segmentA_x * rayDirectionY + -segmentA_y * rayDirectionX) / denominator;

            if (t >= 0f && t <= 1f) {
                intersect_x = segmentA_x + (lineDir_x * t);
                intersect_y = segmentA_y + (lineDir_y * t);

                float dot =
                    (rayDirectionX * intersect_x) +
                    (rayDirectionY * intersect_y);

                if (dot > 0)
                    return true;
                else {
                    intersect_x = intersect_y = 0;
                    return false;
                }
            }
            else {
                intersect_x = intersect_y = 0;
                return false;
            }
        }
        /// <summary>
        /// slightly simplified version for cases when ray starts from 0,0
        /// </summary>
        public static bool RayIntersectSegment(Vector2 rayDirection, Vector2 segmentA, Vector2 segmentB, out Vector2 intersection) {
            float resultX, resultY;
            bool result = RayIntersectSegment(rayDirection.x, rayDirection.y, segmentA.x, segmentA.y, segmentB.x, segmentB.y, out resultX, out resultY);
            intersection = new Vector2(resultX, resultY);
            return result;
        }
        #endregion

        #region line intersect segment
        public static bool LineIntersectSegment(
            float lineA_x, float lineA_y,
            float lineB_x, float lineB_y,
            float segmentA_x, float segmentA_y,
            float segmentB_x, float segmentB_y,
            out float intersect_x,
            out float intersect_y) {
            float lineDir_x = segmentB_x - segmentA_x;
            float lineDir_y = segmentB_y - segmentA_y;
            float denominator = lineDir_y * lineB_x - lineDir_x * lineB_y;

            //paralel
            if (denominator == 0) {
                intersect_x = intersect_y = 0;
                return false;
            }

            float t = ((segmentA_x - lineA_x) * lineB_y + (lineA_y - segmentA_y) * lineB_x) / denominator;

            if (t >= 0f && t <= 1f) {
                intersect_x = segmentA_x + (lineDir_x * t);
                intersect_y = segmentA_y + (lineDir_y * t);
                return true;
            }
            else {
                intersect_x = intersect_y = 0;
                return false;
            }
        }

        public static bool LineIntersectSegment(Vector2 rayOrigin, Vector2 rayDirection, Vector2 segmentA, Vector2 segmentB, out Vector2 intersection) {
            float resultX, resultY;
            bool result = LineIntersectSegment(rayOrigin.x, rayOrigin.y, rayDirection.x, rayDirection.y, segmentA.x, segmentA.y, segmentB.x, segmentB.y, out resultX, out resultY);
            intersection = new Vector2(resultX, resultY);
            return result;
        }

        /// <summary>
        /// slightly simplified version for cases when line starts from 0,0
        /// </summary>
        public static bool LineIntersectSegment(
            float line_x, float line_y,
            float segmentA_x, float segmentA_y,
            float segmentB_x, float segmentB_y,
            out float intersect_x,
            out float intersect_y) {
            float lineDir_x = segmentB_x - segmentA_x;
            float lineDir_y = segmentB_y - segmentA_y;
            float denominator = lineDir_y * line_x - lineDir_x * line_y;

            //paralel
            if (denominator == 0) {
                intersect_x = intersect_y = 0;
                return false;
            }

            float t = (segmentA_x * line_y + -segmentA_y * line_x) / denominator;

            if (t >= 0f && t <= 1f) {
                intersect_x = segmentA_x + (lineDir_x * t);
                intersect_y = segmentA_y + (lineDir_y * t);
                return true; 
            }
            else {
                intersect_x = intersect_y = 0;
                return false;
            }
        }
        /// <summary>
        /// slightly simplified version for cases when line starts from 0,0
        /// </summary>
        public static bool LineIntersectSegment(Vector2 line, Vector2 segmentA, Vector2 segmentB, out Vector2 intersection) {
            float resultX, resultY;
            bool result = LineIntersectSegment(line.x, line.y, segmentA.x, segmentA.y, segmentB.x, segmentB.y, out resultX, out resultY);
            intersection = new Vector2(resultX, resultY);
            return result;
        }
        #endregion

        #region ray intersect 3d segment with top projection
        public static bool RayIntersectXZ(Vector2 rayOrigin, Vector2 rayDirection, Vector2 segmentA, Vector2 segmentB, out Vector2 lineIntersection) {
            float intersectX, intersectY, intersectZ;
            bool result = RayIntersectXZ(rayOrigin.x, rayOrigin.y, rayDirection.x, rayDirection.y, segmentA.x, 0, segmentA.y, segmentB.x, 0, segmentB.y, out intersectX, out intersectY, out intersectZ);
            lineIntersection = result ? new Vector2(intersectX, intersectZ) : new Vector2();
            return result;
        }

        public static bool RayIntersectXZ(Vector3 rayOrigin, Vector3 rayDirection, Vector3 segmentA, Vector3 segmentB, out Vector3 lineIntersection) {
            float intersectX, intersectY, intersectZ;
            bool result = RayIntersectXZ(rayOrigin.x, rayOrigin.z, rayDirection.x, rayDirection.z, segmentA.x, segmentA.y, segmentA.z, segmentB.x, segmentB.y, segmentB.z, out intersectX, out intersectY, out intersectZ);
            lineIntersection = result ? new Vector3(intersectX, intersectY, intersectZ) : new Vector3();
            return result;
        }


        public static bool RayIntersectXZ(Vector2 rayOrigin, Vector2 rayDirection, Vector3 segmentA, Vector3 segmentB, out Vector3 lineIntersection) {
            float intersectX, intersectY, intersectZ;
            bool result = RayIntersectXZ(rayOrigin.x, rayOrigin.y, rayDirection.x, rayDirection.y, segmentA.x, segmentA.y, segmentA.z, segmentB.x, segmentB.y, segmentB.z, out intersectX, out intersectY, out intersectZ);
            lineIntersection = result ? new Vector3(intersectX, intersectY, intersectZ) : new Vector3();
            return result;
        }

        public static bool RayIntersectXZ(
            float rayOriginX, float rayOriginZ, float rayDirectionX, float rayDirectionZ,
            float segmentA_x, float segmentA_y, float segmentA_z, float segmentB_x, float segmentB_y, float segmentB_z,
            out float intersect_x, out float intersect_y, out float intersect_z) {
            float lineDir_x = segmentB_x - segmentA_x;
            float lineDir_y = segmentB_y - segmentA_y;
            float lineDir_z = segmentB_z - segmentA_z;
            float denominator = lineDir_z * rayDirectionX - lineDir_x * rayDirectionZ;

            //paralel
            if (denominator == 0) {
                intersect_x = intersect_y = intersect_z = 0;
                return false;
            }

            float t = ((segmentA_x - rayOriginX) * rayDirectionZ + (rayOriginZ - segmentA_z) * rayDirectionX) / denominator;

            if (t >= 0f && t <= 1f) {
                intersect_x = segmentA_x + (lineDir_x * t);
                intersect_y = segmentA_y + (lineDir_y * t);
                intersect_z = segmentA_z + (lineDir_z * t);

                float dot =
                    (rayDirectionX * (intersect_x - rayOriginX)) +
                    (rayDirectionZ * (intersect_z - rayOriginZ));

                if (dot > 0)
                    return true;
                else {
                    intersect_x = intersect_y = intersect_z = 0;
                    return false;
                }
            }
            else {
                intersect_x = intersect_y = intersect_z = 0;
                return false;
            }
        }

        public static bool RayIntersectXZ(float rayOriginX, float rayOriginZ, float rayDirectionX, float rayDirectionZ, Graphs.CellContentData segment, out float intersect_x, out float intersect_y, out float intersect_z) {
            return RayIntersectXZ(rayOriginX, rayOriginZ, rayDirectionX, rayDirectionZ, segment.xLeft, segment.yLeft, segment.zLeft, segment.xRight, segment.yRight, segment.zRight, out intersect_x, out intersect_y, out intersect_z);
        }
        #endregion
        #endregion


        public static bool ClampedRayIntersectXZ(
        Vector3 rayOrigin, Vector3 rayDirection,
        Vector3 lineA, Vector3 lineB,
        out Vector3 lineIntersection) {

            Vector3 lineDirection = lineB - lineA;
            float denominator = (lineDirection.z * rayDirection.x - lineDirection.x * rayDirection.z);

            //lines are paralel
            if (denominator == 0) {
                lineIntersection = Vector3.zero;
                return false;
            }

            float t1 = ((lineA.x - rayOrigin.x) * rayDirection.z + (rayOrigin.z - lineA.z) * rayDirection.x) / denominator;
            bool result = t1 < 0f || t1 > 1f;
            t1 = Mathf.Clamp01(t1);


            lineIntersection = lineA + (lineDirection * t1);

            //float dot =
            //    (rayDirection.x * (lineIntersection.x - rayOrigin.x)) +
            //    (rayDirection.z * (lineIntersection.z - rayOrigin.z));

            return result;
        }


        //return point that on first line by projectiong second line from XZ
        public static bool LineIntersectXZ(Vector3 mainLineA, Vector3 mainLineB, Vector3 leadingLineA, Vector3 leadingLineB, out Vector3 lineIntersection) {
            Vector3 mainLineDirection = mainLineB - mainLineA; //direction of main line
            Vector3 leadingLineDirection = leadingLineB - leadingLineA; //direction of tested line
            float denominator = (mainLineDirection.z * leadingLineDirection.x - mainLineDirection.x * leadingLineDirection.z);

            //paralel
            if (denominator == 0) {
                lineIntersection = Vector3.zero;
                return false;
            }

            float t = ((mainLineA.x - leadingLineA.x) * leadingLineDirection.z + (leadingLineA.z - mainLineA.z) * leadingLineDirection.x) / denominator;

            if (t >= 0f && t <= 1f) {
                lineIntersection = mainLineA + (mainLineDirection * t);

                //fancy way to check if intersection between leading leadingLineA and leadingLineB
                return
                    (leadingLineDirection.x * (lineIntersection.x - leadingLineA.x)) +                    //dot product of intersection and leading line >= 0. Mean intersection if front of leading lline
                    (leadingLineDirection.z * (lineIntersection.z - leadingLineA.z)) >= 0 &&              //dot product of intersection and leading line >= 0. Mean intersection if front of leading lline
                    //Check if intersection point closer to leading line A than leading line B
                    (Sqr(leadingLineB.x - leadingLineA.x)) + (Sqr(leadingLineB.z - leadingLineA.z)) >=       //sqr distance between leading lines
                    (Sqr(lineIntersection.x - leadingLineA.x)) + (Sqr(lineIntersection.z - leadingLineA.z)); //sqr distance between leading line a and intersection
            }
            else {
                lineIntersection = Vector3.zero;
                return false;
            }
        }



        //thats a lot of arguments
        public static bool LineIntersectXZ(
            //line A
            float mainLineAx, float mainLineAy, float mainLineAz,
            //line B
            float mainLineBx, float mainLineBy, float mainLineBz,
            //leading Line A
            float leadingLineAx, float leadingLineAy, float leadingLineAz,
            //leading Line B
            float leadingLineBx, float leadingLineBy, float leadingLineBz,
            //result
            out Vector3 lineIntersection) {

            float mainLineDirectionx = mainLineBx - mainLineAx;
            float mainLineDirectiony = mainLineBy - mainLineAy;
            float mainLineDirectionz = mainLineBz - mainLineAz;
            float leadingLineDirectionx = leadingLineBx - leadingLineAx;
            //float leadingLineDirectiony = leadingLineBy - leadingLineAy;
            float leadingLineDirectionz = leadingLineBz - leadingLineAz;
            float denominator = (mainLineDirectionz * leadingLineDirectionx - mainLineDirectionx * leadingLineDirectionz);

            //paralel
            if (denominator == 0) {
                lineIntersection = Vector3.zero;
                return false;
            }

            float t = (
                (mainLineAx - leadingLineAx) * leadingLineDirectionz +
                (leadingLineAz - mainLineAz) * leadingLineDirectionx) / denominator;

            if (t >= 0f && t <= 1f) {
                lineIntersection = new Vector3(
                    mainLineAx + (mainLineDirectionx * t),
                    mainLineAy + (mainLineDirectiony * t), 
                    mainLineAz + (mainLineDirectionz * t));

                //fancy way to check if intersection between leading leadingLineA and leadingLineB
                return
                    (leadingLineDirectionx * (lineIntersection.x - leadingLineAx)) +                       //dot product of intersection and leading line >= 0. Mean intersection if front of leading lline
                    (leadingLineDirectionz * (lineIntersection.z - leadingLineAz)) >= 0 &&                 //dot product of intersection and leading line >= 0. Mean intersection if front of leading lline
                    //Check if intersection point closer to leading line A than leading line B
                    (Sqr(leadingLineBx - leadingLineAx)) + (Sqr(leadingLineBz - leadingLineAz)) >=         //sqr distance between leading lines
                    (Sqr(lineIntersection.x - leadingLineAx)) + (Sqr(lineIntersection.z - leadingLineAz)); //sqr distance between leading line a and intersection
            }
            else {
                lineIntersection = Vector3.zero;
                return false;
            }
        }

        public static bool LineLineIntersectXZ(Vector3 mainLineA, Vector3 mainLineB, Vector3 leadingLineA, Vector3 leadingLineB, out Vector3 lineIntersection) {
            Vector3 mainLineDirection = mainLineB - mainLineA;
            Vector3 leadingLineDirection = leadingLineB - leadingLineA;
            float denominator = (mainLineDirection.z * leadingLineDirection.x - mainLineDirection.x * leadingLineDirection.z);

            //paralel
            if (denominator == 0) {
                lineIntersection = Vector3.zero;
                return false;
            }

            float t = ((mainLineA.x - leadingLineA.x) * leadingLineDirection.z + (leadingLineA.z - mainLineA.z) * leadingLineDirection.x) / denominator;
            lineIntersection = mainLineA + (mainLineDirection * t);
            return true;
        }

        #region line-line
        public static bool LineIntersection(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 intersection) {
            float ix, iy;
            bool result = LineIntersection(a1.x, a1.y, a2.x, a2.y, b1.x, b1.y, b2.x, b2.y, out ix, out iy);
            intersection = new Vector2(ix, iy);
            return result;
        }

        public static bool LineIntersection(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out float intersectionX, out float intersectionY) {
            return LineIntersection(a1.x, a1.y, a2.x, a2.y, b1.x, b1.y, b2.x, b2.y, out intersectionX, out intersectionY);
        }

        public static bool LineIntersection(Line2D line, Vector2 a, Vector2 b, out Vector2 intersection) {
            float ix, iy;
            bool result = LineIntersection(line.leftX, line.leftY, line.rightX, line.rightY, a.x, a.y, b.x, b.y, out ix, out iy);
            intersection = new Vector2(ix, iy);
            return result;
        }

        public static bool LineIntersection(Line2D line, float ax, float ay, float bx, float by, out Vector2 intersection) {
            float ix, iy;
            bool result = LineIntersection(line.leftX, line.leftY, line.rightX, line.rightY, ax, ay, bx, by, out ix, out iy);
            intersection = new Vector2(ix, iy);
            return result;
        }

        public static bool LineIntersection(Line2D line, float ax, float ay, float bx, float by, out float intersectionX, out float intersectionY) {
            return LineIntersection(line.leftX, line.leftY, line.rightX, line.rightY, ax, ay, bx, by, out intersectionX, out intersectionY);
        }

        public static bool LineIntersection(Line2D line1, Line2D line2, out float intersectionX, out float intersectionY) {
            bool result = LineIntersection(
                line1.leftX, line1.leftY, line1.rightX, line1.rightY,
                line2.leftX, line2.leftY, line2.rightX, line2.rightY, 
                out intersectionX, out intersectionY);
            return result;
        }

        public static bool LineIntersection(
            float line1_Ax, float line1_Ay, //line 1 A
            float line1_Bx, float line1_By, //line 1 B
            float line2_Ax, float line2_Ay, //line 2 A
            float line2_Bx, float line2_By, //line 2 B
            out float intersectionX, 
            out float intersectionY) {

            float line1_dirX = line1_Bx - line1_Ax;
            float line1_dirY = line1_By - line1_Ay;

            float line2_dirX = line2_Bx - line2_Ax;
            float line2_dirY = line2_By - line2_Ay;            

            float d = line1_dirY * line2_dirX - line1_dirX * line2_dirY;

            if (d == 0) {
                intersectionX = intersectionY = 0f;
                return false;
            }

            float product = ((line1_Ax - line2_Ax) * line2_dirY + (line2_Ay - line1_Ay) * line2_dirX) / d;

            intersectionX = line1_Ax + line1_dirX * product;
            intersectionY = line1_Ay + line1_dirY * product;
            return true;
        }
        #endregion


        #region segmenit-line
        public static bool SegmentLineIntersection(Vector2 segment1, Vector2 segment2, Vector2 line1, Vector2 line2, out Vector2 intersection) {
            float ix, iy;
            bool result = SegmentLineIntersection(segment1.x, segment1.y, segment2.x, segment2.y, line1.x, line1.y, line2.x, line2.y, out ix, out iy);
            intersection = new Vector2(ix, iy);
            return result;
        }

        public static bool SegmentLineIntersection(
            float segment1X, float segment1Y,//segment 1
            float segment2X, float segment2Y,//segment 2
            float line1X, float line1Y,      //ray position
            float line2X, float line2Y,      //ray direction
            out float intersectionX, 
            out float intersectionY) {

            float segmentDirX = segment2X - segment1X;
            float segmentDirY = segment2Y - segment1Y;

            float lineDirX = line2X - line1X;
            float lineDirY = line2Y - line1Y;

            float d = segmentDirY * lineDirX - segmentDirX * lineDirY;

            if (d == 0) {
                intersectionX = intersectionY = 0f;
                return false;
            }

            float t = ((segment1X - line1X) * lineDirY + (line1Y - segment1Y) * lineDirX) / d;

            // Find the point of intersection.
            if (t >= 0f && t <= 1f) {
                intersectionX = segment1X + segmentDirX * t;
                intersectionY = segment1Y + segmentDirY * t;
                return true;
            }
            else {
                intersectionX = intersectionY = 0f;
                return false;
            }
        }
        #endregion

        #region segment-segment
        public static bool SegmentSegmentIntersection(Vector2 segment1_1, Vector2 segment1_2, Vector2 segment2_1, Vector2 segment2_2, out Vector2 intersection) {
            float ix, iy;
            bool result = SegmentSegmentIntersection(segment1_1.x, segment1_1.y, segment1_2.x, segment1_2.y, segment2_1.x, segment2_1.y, segment2_2.x, segment2_2.y, out ix, out iy);
            intersection = new Vector2(ix, iy);
            return result;
        }

        public static bool SegmentSegmentIntersection(
            float segment1_1X, float segment1_1Y,//segment 1, point 1
            float segment1_2X, float segment1_2Y,//segment 1, point 2
            float segment2_1X, float segment2_1Y,//segment 2, point 1
            float segment2_2X, float segment2_2Y,//segment 2, point 2
            out float intersectionX,
            out float intersectionY) {

            float segment1DirX = segment1_2X - segment1_1X;
            float segment1DirY = segment1_2Y - segment1_1Y;
            float segment2DirX = segment2_2X - segment2_1X;
            float segment2DirY = segment2_2Y - segment2_1Y;
            
            float d = (segment1DirY * segment2DirX - segment1DirX * segment2DirY);

            if (d == 0) {
                intersectionX = intersectionY = 0f;
                return false;
            }

            float t = ((segment1_1X - segment2_1X) * segment2DirY + (segment2_1Y - segment1_1Y) * segment2DirX) / d;

            // Find the point of intersection.
            if (t >= 0f && t <= 1f) {
                intersectionX = segment1_1X + segment1DirX * t;
                intersectionY = segment1_1Y + segment1DirY * t;

                float intersectionRelativeToSegment2X = intersectionX - segment2_1X;
                float intersectionRelativeToSegment2Y = intersectionY - segment2_1Y;

                return 
                    Dot(intersectionRelativeToSegment2X, intersectionRelativeToSegment2Y, segment2DirX, segment2DirY) > 0f &
                    (SqrMagnitude(intersectionRelativeToSegment2X, intersectionRelativeToSegment2Y) <= SqrMagnitude(segment2DirX, segment2DirY));
            }
            else {
                intersectionX = intersectionY = 0f;
                return false;
            }
        }
        #endregion


        #region clip line
        public static Vector3 ClipLineToPlaneX(Vector3 linePoint, Vector3 lineVectorNormalized, float x) {
            return linePoint + (((x - linePoint.x) / lineVectorNormalized.x) * lineVectorNormalized);
        }
        public static Vector3 ClipLineToPlaneY(Vector3 linePoint, Vector3 lineVectorNormalized, float y) {
            return linePoint + (((y - linePoint.y) / lineVectorNormalized.y) * lineVectorNormalized);
        }
        public static Vector3 ClipLineToPlaneZ(Vector3 linePoint, Vector3 lineVectorNormalized, float z) {
            return linePoint + (((z - linePoint.z) / lineVectorNormalized.z) * lineVectorNormalized);
        }
        public static Vector2 ClipLineToPlaneX(Vector2 linePoint, Vector2 lineVectorNormalized, float x) {
            return linePoint + (((x - linePoint.x) / lineVectorNormalized.x) * lineVectorNormalized);
        }
        public static Vector2 ClipLineToPlaneY(Vector2 linePoint, Vector2 lineVectorNormalized, float y) {
            return linePoint + (((y - linePoint.y) / lineVectorNormalized.y) * lineVectorNormalized);
        }

        //return point projected to line from X axis
        //private static Vector2 ClampLineToPlaneX(float leftX, float leftY, float rightX, float rightY, float projectedX, float projectedY) {
        //    float ratio = (projectedX - leftX) / (rightX - leftX);
        //    float lineY = (rightY - leftY) * ratio;
        //    return new Vector2(projectedX, leftY + lineY);
        //}
        //returns Y  at target X
        public static float ClampLineToPlaneX(float aX, float aY, float bX, float bY, float projectedX) {
            return (projectedX - aX) / (bX - aX) * (bY - aY) + aY;
        }

        #endregion

        #region draw circle
        public static Vector2[] DrawCircle(int value) {
            Vector2[] result = new Vector2[value];
            for (int i = 0; i < value; ++i) {
                result[i] = new Vector3(
                    (float)Math.Cos(i * 2.0f * Math.PI / value),
                    (float)Math.Sin(i * 2.0f * Math.PI / value));
            }
            return result;
        }

        public static Vector2[] DrawCircle(int value, float radius) {
            Vector2[] result = new Vector2[value];
            for (int i = 0; i < value; ++i) {
                result[i] = new Vector3(
                    (float)Math.Cos(i * 2.0f * Math.PI / value) * radius,
                    (float)Math.Sin(i * 2.0f * Math.PI / value) * radius);
            }
            return result;
        }

        public static Vector3[] DrawCircle(int value, Vector3 position, float radius) {
            Vector3[] result = new Vector3[value];
            for (int i = 0; i < value; ++i) {
                result[i] = new Vector3(
                    (float)Math.Cos(i * 2.0f * Math.PI / value) * radius + position.x,
                    position.y,
                    (float)Math.Sin(i * 2.0f * Math.PI / value) * radius + position.z);
            }
            return result;
        }

        public static Vector3[] DrawCircle(Axises axises, int count, Vector3 position, float radius = 1f) {
            Vector3[] result = new Vector3[count];

            for (int i = 0; i < count; ++i) {
                Vector3 vector;
                float v1 = (float)Math.Cos(i * 2.0f * Math.PI / count) * radius;
                float v2 = (float)Math.Sin(i * 2.0f * Math.PI / count) * radius;
                switch (axises) {
                    case Axises.xy:
                        vector = new Vector3(v1 + position.x, v2 + position.y, position.z);
                        break;
                    case Axises.xz:
                        vector = new Vector3(v1 + position.x, position.y, v2 + position.z);
                        break;
                    case Axises.yz:
                        vector = new Vector3(position.x, v1 + position.y, v2 + position.z);
                        break;
                    default:
                        vector = position;
                        break;
                }

                result[i] = vector;
            }
            return result;
        }

        #endregion
        
        #region bounds stuff
        public static Bounds GetCombinedBounds(Bounds[] input) {
            float boundsMinX, boundsMinY, boundsMinZ, boundsMaxX, boundsMaxY, boundsMaxZ;
            Bounds firstBounds = input[0];
            Vector3 center = firstBounds.center;
            Vector3 extends = firstBounds.extents;

            boundsMinX = center.x - extends.x;
            boundsMinY = center.y - extends.y;
            boundsMinZ = center.z - extends.z;
            boundsMaxX = center.x + extends.x;
            boundsMaxY = center.y + extends.y;
            boundsMaxZ = center.z + extends.z;

            for (int i = 1; i < input.Length; i++) {
                Bounds bounds = input[i];
                Vector3 bCenter = bounds.center;
                Vector3 bExtents = bounds.extents;
                boundsMinX = Min(bCenter.x - bExtents.x, boundsMinX);
                boundsMinY = Min(bCenter.y - bExtents.y, boundsMinY);
                boundsMinZ = Min(bCenter.z - bExtents.z, boundsMinZ);
                boundsMaxX = Max(bCenter.x + bExtents.x, boundsMaxX);
                boundsMaxY = Max(bCenter.y + bExtents.y, boundsMaxY);
                boundsMaxZ = Max(bCenter.z + bExtents.z, boundsMaxZ);
            }

            return new Bounds(
                new Vector3((boundsMinX + boundsMaxX) * 0.5f, (boundsMinY + boundsMaxY) * 0.5f, (boundsMinZ + boundsMaxZ) * 0.5f),
                new Vector3(boundsMaxX - boundsMinX, boundsMaxY - boundsMinY, boundsMaxZ - boundsMinZ));

        }

        public static Bounds FitBounds(Bounds A, Bounds B) {
            Vector3 max = Vector3.Min(A.max, B.max);
            Vector3 min = Vector3.Max(A.min, B.min);
            Vector3 size = max - min;
            return new Bounds(min + (size * 0.5f), size);
        }

        public static Bounds GetBounds(params Vector3[] vectors) {
            if (vectors.Length == 0)
                return new Bounds();

            float minX, maxX, minY, maxY, minZ, maxZ;
            minX = maxX = vectors[0].x;
            minY = maxY = vectors[0].y;
            minZ = maxZ = vectors[0].z;
            for (int i = 1; i < vectors.Length; i++) {
                Vector3 vector = vectors[i];
                minX = Math.Min(minX, vector.x);
                maxX = Math.Max(maxX, vector.x);
                minY = Math.Min(minY, vector.y);
                maxY = Math.Max(maxY, vector.y);
                minZ = Math.Min(minZ, vector.z);
                maxZ = Math.Max(maxZ, vector.z);
            }

            Vector3 size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            return new Bounds(new Vector3(minX + (size.x * 0.5f), minY + (size.y * 0.5f), minZ + (size.z * 0.5f)), size);
        }

        public static Bounds2D GetBounds2D(params Vector2[] vectors) {
            if (vectors.Length == 0)
                return new Bounds2D();

            float minX, maxX, minY, maxY;
            minX = maxX = vectors[0].x;
            minY = maxY = vectors[0].y;
            for (int i = 1; i < vectors.Length; i++) {
                Vector3 vector = vectors[i];
                minX = Math.Min(minX, vector.x);
                maxX = Math.Max(maxX, vector.x);
                minY = Math.Min(minY, vector.y);
                maxY = Math.Max(maxY, vector.y);
            }

            return new Bounds2D(minX, minY, maxX, maxY);
        }

        #endregion

        #region Bezier
        public static Vector3 BezierLinear(float t, Vector3 p0, Vector3 p1) {
            return p0 + t * (p1 - p0);
        }

        public static Vector3 BezierQuadratic(float t, Vector3 p0, Vector3 p1, Vector3 p2) {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            Vector3 p = uu * p0;       
            p += 2 * u * t * p1;
            p += tt * p2;
            return p;
        }

        public static Vector3 BezierCubic(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += ttt * p3;

            return p;
        }
        #endregion

        #region handy convertation
        public static Vector2 ToVector2(Vector3 vector) {
            return new Vector2(vector.x, vector.z);
        }
        public static Vector3 ToVector3(Vector2 vector) {
            return new Vector3(vector.x, 0, vector.y);
        }
        #endregion

        #region misc
        public static Vector2 GetTargetVector(float angle, float length) {
            return new Vector2(
                (float)Math.Cos(angle * Math.PI / 180) * length,
                (float)Math.Sin(angle * Math.PI / 180) * length);
        }
        #endregion
    }
}