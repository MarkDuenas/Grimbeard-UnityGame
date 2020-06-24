using UnityEngine;
using System;
using System.Collections;

namespace K_PathFinder.VectorInt {
    //little perfomance experiment
	public struct Vector2Float : IEquatable<Vector2Float> {
        public readonly float x, y;

        #region constructors 

		public Vector2Float(float X, float Y){
            x = X;
            y = Y;
        }

        public Vector2Float(Vector2 v2){
            x = v2.x;
            y = v2.y;
        }
		
		#endregion

        public float Length {
            get { return Mathf.Sqrt(LengthSquared); }
        }
        public float LengthSquared {
            get { return (x * x) + (y * y); }
        }

        #region operators
        public static explicit operator Vector2 (Vector2Float v2){
			return new Vector2(v2.x, v2.y);
		}

		public static Vector2Float operator +(Vector2Float a, Vector2Float b){
			return new Vector2Float(a.x + b.x, a.y + b.y);
		}
		public static Vector2Float operator -(Vector2Float a, Vector2Float b){
			return new Vector2Float(a.x - b.x, a.y - b.y);
		}

		public static Vector2Float operator *(Vector2Float a, int val){
			return new Vector2Float(a.x * val, a.y * val);
		}
		public static Vector2Float operator /(Vector2Float a, int val){
			return new Vector2Float(a.x / val, a.y / val);
		}
        
		public static Vector2Float operator /(Vector2Float a, float val){
			return new Vector2Float(Mathf.RoundToInt((float)a.x / val), Mathf.RoundToInt((float)a.y / val));
		}

		public static bool operator ==(Vector2Float a, Vector2Float b){
            return a.x == b.x && a.y == b.y;
		}
		
		public static bool operator !=(Vector2Float a, Vector2Float b){
			return !(a == b);
		}
		
		public override int GetHashCode(){
			return (int)x ^ ((int)y * 20);
		}
		#endregion
        
		public override bool Equals(object obj){
			if (obj == null || !(obj is Vector2Float))
				return false;

            return Equals((Vector2Float)obj);
		}

        public bool Equals(Vector2Float other) {
            return other.x == x && other.y == y;
        }
        

        public override string ToString(){
            return string.Format("({0}, {1})", x, y);
		}


        public static Vector2Float zero {
			get { return new Vector2Float(0, 0); }
		}

		public static Vector2Float one {
            get {return new Vector2Float(1, 1);}
		}
    }
}