using System;
using UnityEngine;

//structs that used here there everywhere
namespace K_PathFinder {
    //[Serializable]
    //public struct KeyValue<TKey, TValue> {
    //    [SerializeField] public TKey key;
    //    [SerializeField] public TValue value;

    //    public KeyValue(TKey Key, TValue Value) {
    //        key = Key;
    //        value = Value;
    //    }

    //    public override string ToString() {
    //        return string.Format("key {0} value {1}", key.ToString(), value == null ? "null" : value.ToString());
    //    }
    //}

    [Serializable]
    public struct IndexLengthInt : IEquatable<IndexLengthInt> {
        [SerializeField] public int index;
        [SerializeField] public int length;

        public IndexLengthInt(int Index, int Length) {
            index = Index;
            length = Length;
        }

        public override bool Equals(object obj) {
            if (obj == null || !(obj is IndexLengthInt))
                return false;

            return Equals((IndexLengthInt)obj);
        }

        public int indexPlusLength {
            get { return index + length; }
        }

        public bool Equals(IndexLengthInt other) {
            return other.index == index && other.length == length;
        }

        public override int GetHashCode() {
            return index * length;
        }

        public override string ToString() {
            return string.Format("index: {0}, length: {1}", index, length);
        }
    }

    [Serializable]
    public struct Line2D : IEquatable<Line2D> {
        [SerializeField] public float leftX, leftY, rightX, rightY;

        public Line2D(float LeftX, float LeftY, float RightX, float RightY) {
            leftX = LeftX;
            leftY = LeftY;
            rightX = RightX;
            rightY = RightY;
        }

        public Line2D(Vector2 left, Vector2 right) {
            leftX = left.x;
            leftY = left.y;
            rightX = right.x;
            rightY = right.y;
        }

        //to test things. maybe using this in array case change perfomance?
        public void Set(Vector2 left, Vector2 right) {
            leftX = left.x;
            leftY = left.y;
            rightX = right.x;
            rightY = right.y;
        }

        public Vector2 left {
            get { return new Vector2(leftX, leftY); }
            set {
                leftX = value.x;
                leftY = value.y;
            }
        }

        public Vector2 right {
            get { return new Vector2(rightX, rightY); }
            set {
                rightX = value.x;
                rightY = value.y;
            }
        }

        public override bool Equals(object obj) {
            if (obj == null || !(obj is Line2D))
                return false;

            return Equals((Line2D)obj);
        }

        public bool Equals(Line2D other) {
            return 
                other.leftX == leftX &&
                other.leftY == leftY &&
                other.rightX == rightX &&
                other.rightY == rightY;
        }

        public override int GetHashCode() {
            return (int)((leftX + leftY) * (rightX + rightY));
        }

        public override string ToString() {
            return string.Format("left (x: {0}, y: {1}), right (x: {2}, y: {3})", leftX, leftY, rightX, rightY);
        }
    }
}