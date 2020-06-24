using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace K_PathFinder.VectorInt {
    [Serializable]
	public struct Vector2Int : IEquatable<Vector2Int> {
        [SerializeField]
        public int x, y;  
        
        public Vector2Int(int X, int Y){
            x = X;
            y = Y;
		}

        public Vector2Int(float X, float Y) {
            x = Mathf.RoundToInt(X);
            y = Mathf.RoundToInt(Y);
        }

        public Vector2Int(Vector2 v2){
            x = Mathf.RoundToInt(v2.x);
            y = Mathf.RoundToInt(v2.y);
        }

        public void Set(int X, int Y) {
            x = X;
            y = Y;
        }

        #region acessors        
        public int MinValue{
			get{return x > y ? y : x;}
		}

		public int MaxValue{
			get{return x > y ? x : y;}
		}

		public int ValueDifference{
			get{return Dif(this);}
		}
		#endregion

		#region math
		public float Length{
			get{return Mathf.Sqrt(LengthSquared);}
		}		
		public int LengthSquared{
			get{return (x * x) + (y * y);}
		}

		public static Vector2Int LowerToZero(Vector2Int value){
			return new Vector2Int(0, Dif(value));
		}

		public static int Dif(Vector2Int value){
			return Mathf.Abs(value.x - value.y);
		}

		public static Vector2Int Max(Vector2Int a, Vector2Int b){
			return new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
		}
		public static Vector2Int Min(Vector2Int a, Vector2Int b){
			return new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
		}

        public static float DistanceSqr(Vector2Int a, Vector2Int b) {
            return ((float)(b.x - a.x) * (float)(b.x - a.x)) + ((float)(b.y - a.y) * (float)(b.y - a.y));
        }

        public static float Distance(Vector2Int a, Vector2Int b) {
            return (float)Math.Sqrt(DistanceSqr(a, b));
        }
        #endregion

        #region operators
        public static explicit operator Vector2 (Vector2Int v2){
			return new Vector2(v2.x, v2.y);
		}

		public static Vector2Int operator +(Vector2Int a, Vector2Int b){
			return new Vector2Int(a.x + b.x, a.y + b.y);
		}
		public static Vector2Int operator -(Vector2Int a, Vector2Int b){
			return new Vector2Int(a.x - b.x, a.y - b.y);
		}

		public static Vector2Int operator *(Vector2Int a, int val){
			return new Vector2Int(a.x * val, a.y * val);
		}
		public static Vector2Int operator /(Vector2Int a, int val){
			return new Vector2Int(a.x / val, a.y / val);
		}

		public static Vector2Int operator *(Vector2Int a, float val){
			return new Vector2Int(Mathf.RoundToInt((float)a.x * val), Mathf.RoundToInt((float)a.y * val));
		}
		public static Vector2Int operator /(Vector2Int a, float val){
			return new Vector2Int(Mathf.RoundToInt((float)a.x / val), Mathf.RoundToInt((float)a.y / val));
		}

		public static bool operator ==(Vector2Int a, Vector2Int b){
            return a.x == b.x && a.y == b.y;
		}
		
		public static bool operator !=(Vector2Int a, Vector2Int b){
			return !(a == b);
		}
		
		public override int GetHashCode(){
			return x ^ y;
		}
		#endregion

        public bool HaveAny(int value) {
            return x == value || y == value;
        }

        public bool HaveAny(int value1, int value2) {
            return HaveAny(value1) || HaveAny(value2);
        }

		public override bool Equals(object obj){
			if (obj == null || !(obj is Vector2Int))
				return false;

            return Equals((Vector2Int)obj);
		}

        public bool Equals(Vector2Int other) {
            return other.x == x && other.y == y;
        }

        public override string ToString(){
            return string.Format("x: {0}, y: {1}", x, y);
		}

		public static Vector2Int zero{
			get { return new Vector2Int(0, 0); }
		}

		public static Vector2Int one{
            get {return new Vector2Int(1, 1);}
		}
    }
}