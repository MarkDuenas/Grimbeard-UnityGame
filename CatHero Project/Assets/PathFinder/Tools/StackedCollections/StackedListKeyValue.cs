using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder.CoolTools {

    //ripp off from StackedList
    //kinda should be inhereted but when i start to implement inheretence it was crap
    public class StackedListWithKeys<TKey, TValue> where TKey : struct where TValue : struct {
        protected const int INVALID_ROOT = -2;
        protected const int INVALID_INDEX = -1;

        [SerializeField] public int count;                //amount of total items in this collection
        [SerializeField] public int baseSize;             //amount of indexes reserved for base

        //nodes
        [SerializeField] public int[] nextIndexes;
        [SerializeField] public TKey[] keys;
        [SerializeField] public TValue[] values;
        [SerializeField] public int filledDataIndexes;    //amount of filled indexes in nodes array

        //nodes free indexes
        [SerializeField] public int[] freeIndexStack;     //array with unused indexes
        [SerializeField] public int freeIndexStackLength; //amount of filled indexes in that array

        //public struct LinkedNode {
        //    [SerializeField] public int nextIndex;
        //    [SerializeField] public TKey key;
        //    [SerializeField] public TValue value;

        //    public void SetValues(TKey Key, int NextIndex) {
        //        key = Key;
        //        nextIndex = NextIndex;
        //        value = default(TValue);
        //    }

        //    public void SetValues(TKey Key, TValue Value, int NextIndex) {
        //        key = Key;
        //        nextIndex = NextIndex;
        //        value = Value;
        //    }

        //    public void MakeDefault() {
        //        key = default(TKey);
        //        value = default(TValue);
        //        nextIndex = INVALID_INDEX;
        //    }

        //    public void SetPair(KeyValue<TKey, TValue> pair, int NextIndex) {
        //        key = pair.key;
        //        value = pair.value;
        //        nextIndex = NextIndex;
        //    }

        //    public KeyValue<TKey, TValue> getKeyValue {
        //        get { return new KeyValue<TKey, TValue>(key, value); }
        //    }

        //    public override string ToString() {
        //        return string.Format("key: {0}, value: {1}, next: {2}", key.ToString(), value == null ? "null" : value.ToString(), nextIndex);
        //    }
        //}

        #region pool
        /// <summary>
        /// Total size is flexible. 2 Numbers just to tell how much indexes are taken from pool on top
        /// </summary>
        public static StackedListWithKeys<TKey, TValue> PoolTake(int BaseSize, int ExtraSize) {
            StackedListWithKeys<TKey, TValue> result = GenericPool<StackedListWithKeys<TKey, TValue>>.Take();
            result.OnTakeFromPool(BaseSize, ExtraSize);
            return result;
        }

        public static void PoolReturn(ref StackedListWithKeys<TKey, TValue> obj) {
            obj.OnReturnToPool();
            GenericPool<StackedListWithKeys<TKey, TValue>>.ReturnToPool(ref obj);
        }

        void OnTakeFromPool(int BaseSize, int ExtraSize) {
            if (BaseSize < 0)
                throw new ArgumentOutOfRangeException("BaseSize in StackedList cannot be less than 0");

            if (ExtraSize < 0)
                throw new ArgumentOutOfRangeException("ExtraSize in StackedList cannot be less than 0");

            baseSize = filledDataIndexes = BaseSize;
            nextIndexes = GenericPoolArray<int>.Take(BaseSize + ExtraSize);
            keys = GenericPoolArray<TKey>.Take(BaseSize + ExtraSize);
            values = GenericPoolArray<TValue>.Take(BaseSize + ExtraSize);
            
            for (int i = 0; i < BaseSize; i++) {
                nextIndexes[i] = INVALID_ROOT;
            }

            freeIndexStack = GenericPoolArray<int>.Take(32);
            freeIndexStackLength = 0;
        }

        void OnReturnToPool() {
            GenericPoolArray<int>.ReturnToPool(ref nextIndexes);
            GenericPoolArray<TKey>.ReturnToPool(ref keys);
            GenericPoolArray<TValue>.ReturnToPool(ref values);
            GenericPoolArray<int>.ReturnToPool(ref freeIndexStack);
        }
        #endregion

        protected int GetFreeNodeIndex() {
            int result = freeIndexStackLength > 0 ? freeIndexStack[--freeIndexStackLength] : filledDataIndexes++;
            if (result == nextIndexes.Length) {
                GenericPoolArray<int>.IncreaseSize(ref nextIndexes);
                GenericPoolArray<TKey>.IncreaseSize(ref keys);
                GenericPoolArray<TValue>.IncreaseSize(ref values);
            }
            return result;
        }
        protected void ReturnFreeNodeIndex(int index) {
            nextIndexes[index] = INVALID_INDEX;
            keys[index] = default(TKey);
            values[index] = default(TValue);    
   
            if (freeIndexStack.Length == freeIndexStackLength)
                GenericPoolArray<int>.IncreaseSize(ref freeIndexStack);
            freeIndexStack[freeIndexStackLength++] = index;
        }

        public bool AddKey(int index, TKey key) {
            if (index >= baseSize)
                throw new ArgumentException("Index cannot be greater than size", "index");

            if (nextIndexes[index] == INVALID_ROOT) { //if nothing on that index then add it as first item
                nextIndexes[index] = INVALID_INDEX;
                keys[index] = key;
                values[index] = default(TValue);
                count++;
                return true;
            }
            else {
                int lastIndex = index;
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    if (keys[index].Equals(key))
                        return false;
                    lastIndex = index;
                }
                int freeNodeIndex = GetFreeNodeIndex();
                nextIndexes[lastIndex] = freeNodeIndex;

                nextIndexes[freeNodeIndex] = INVALID_INDEX;
                keys[freeNodeIndex] = key;
                values[freeNodeIndex] = default(TValue);
                count++;
                return true;
            }
        }

        //add value to key if key exist
        public bool SetValue(int index, TKey key, TValue value) {
            if (index >= baseSize)
                throw new ArgumentException("Index cannot be greater than size", "index");

            if (nextIndexes[index] == INVALID_ROOT) //if nothing on that index then add it as first item
                return false;
            else {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    if (keys[index].Equals(key)) {
                        values[index] = value;
                        return true;
                    }
                }
                return false;
            }
        }


        public bool AddKeyValue(int index, TKey key, TValue value) {
            //Debug.LogFormat("i {0} k {1} v {2}", index, key, value);

            if (index >= baseSize)
                throw new ArgumentException("Index cannot be greater than size", "index");

            if (nextIndexes[index] == INVALID_ROOT) { //if nothing on that index then add it as first item
                nextIndexes[index] = INVALID_INDEX;
                keys[index] = key;
                values[index] = value;
                count++;
                return true;
            }
            else {
                int lastIndex = index;
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    if (keys[index].Equals(key))
                        return false;
                    lastIndex = index;
                }
                int freeNodeIndex = GetFreeNodeIndex();
                nextIndexes[lastIndex] = freeNodeIndex;

                nextIndexes[freeNodeIndex] = INVALID_INDEX;
                keys[freeNodeIndex] = key;
                values[freeNodeIndex] = value;
                count++;
                return true;
            }
        }

        //remove value ta target key without removing key
        public bool RemoveValue(int index, TKey key) {
            if (index >= baseSize)
                throw new ArgumentException("Index cannot be greater than size", "index");

            if (nextIndexes[index] == INVALID_ROOT) //if nothing on that index then add it as first item
                return false;
            else {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    if (keys[index].Equals(key)) {
                        values[index] = default(TValue);
                        return true;
                    }
                }
                return false;
            }
        }

        public bool RemoveKey(int index, TKey key) {
            if (nextIndexes[index] == INVALID_ROOT)
                return false;

            int prevIndex = INVALID_INDEX;
            while (true) {                
                if (keys[index].Equals(key)) {
                    if (prevIndex == INVALID_INDEX) { //first value in row
                        if (nextIndexes[index] == INVALID_INDEX) {//this is only value in row
                            nextIndexes[index] = INVALID_ROOT;
                            keys[index] = default(TKey);
                            values[index] = default(TValue);
                        }
                        else { //there is something after it
                            int nextIndex = nextIndexes[index];

                            //copy next data to current index
                            nextIndexes[index] = nextIndexes[nextIndex];//nextIndexes[index] = nextIndexes[nextIndexes[index]]; !!!
                            keys[index] = keys[nextIndex];
                            values[index] = values[nextIndex];
                            //return next data to pool
                            ReturnFreeNodeIndex(nextIndex);
                        }
                    }
                    else {//not first value so just set index of precious value to next value
                        //set prevoius node next index to current node next index (even if there is nothing)
                        nextIndexes[prevIndex] = nextIndexes[index];
                        //return current node to pool
                        ReturnFreeNodeIndex(index);
                    }
                    count--;
                    return true;
                }

                prevIndex = index;
                index = nextIndexes[index];

                if (index == INVALID_INDEX)
                    return false;
            }
        }



        public bool RemoveKey(int index, TKey key, out TValue valueOnThisKey) {
            if (nextIndexes[index] == INVALID_ROOT) {
                valueOnThisKey = default(TValue);
                return false;
            }

            int prevIndex = INVALID_INDEX;
            while (true) {
                //LinkedNode curNode = nodes[index];

                if (keys[index].Equals(key)) {
                    valueOnThisKey = values[index];
                    if (prevIndex == INVALID_INDEX) { //first value in row
                        if (nextIndexes[index] == INVALID_INDEX) {//this is only value in row
                            nextIndexes[index] = INVALID_ROOT;
                            keys[index] = default(TKey);
                            values[index] = default(TValue);
                        }
                        else { //there is something after it
                            int nextIndex = nextIndexes[index];

                            //copy next data to current index
                            nextIndexes[index] = nextIndexes[nextIndex];//nextIndexes[index] = nextIndexes[nextIndexes[index]]; !!!
                            keys[index] = keys[nextIndex];
                            values[index] = values[nextIndex];
                            //return next data to pool
                            ReturnFreeNodeIndex(nextIndex);
                        }
                    }
                    else {//not first value so just set index of precious value to next value
                        //set prevoius node next index to current node next index (even if there is nothing)
                        nextIndexes[prevIndex] = nextIndexes[index];
                        //return current node to pool
                        ReturnFreeNodeIndex(index);
                    }
                    count--;
                    return true;
                }

                prevIndex = index;
                index = nextIndexes[index];

                if (index == INVALID_INDEX) {
                    valueOnThisKey = default(TValue);
                    return false;
                }
            }
        }
        
        /// <summary>
        ///collect all data into tight array.
        ///layout contain index where to start and amount of values laying out on that index
        ///dont forget to return this arrays to pool
        ///layout length can differ so also dont use array.length
        /// </summary>
        public void GetOptimizedData(out TKey[] optimizedKeys, out TValue[] optimizedValues, out IndexLengthInt[] layout) {          
            optimizedKeys = GenericPoolArray<TKey>.Take(count);
            optimizedValues = GenericPoolArray<TValue>.Take(count);
            layout = GenericPoolArray<IndexLengthInt>.Take(baseSize);

            int dataIndex = 0;
            for (int index = 0; index < baseSize; index++) {
                //LinkedNode curNode = nodes[index];
                if (nextIndexes[index] == INVALID_ROOT) {
                    layout[index] = new IndexLengthInt(dataIndex, 0);
                }
                else {
                    int dataIndexStart = dataIndex;
                    for (int curIndex = index; curIndex != INVALID_INDEX; curIndex = nextIndexes[curIndex]) {
                        if (optimizedKeys.Length == dataIndex) {
                            GenericPoolArray<TKey>.IncreaseSize(ref optimizedKeys);
                            GenericPoolArray<TValue>.IncreaseSize(ref optimizedValues);
                        }
                        optimizedKeys[dataIndex] = keys[curIndex];
                        optimizedValues[dataIndex] = values[curIndex];
                        dataIndex++;
                    }
                    layout[index] = new IndexLengthInt(dataIndexStart, dataIndex - dataIndexStart);
                }
            }
        }
        
        /// <summary>
        /// clear current data and set this instead
        /// </summary>
        public void SetOptimizedData(TKey[] optimizedKeys, TValue[] optimizedValues, IndexLengthInt[] layout, int validLayoutIndexes) {
            if (optimizedKeys == null)
                Debug.LogError(string.Format("optimized keys passed to stacked list cannot be null"));
            if (optimizedValues == null)
                Debug.LogError(string.Format("optimized values passed to stacked list cannot be null"));
            if (layout == null)
                Debug.LogError(string.Format("optimized layout passed to stacked list cannot be null"));
            if(validLayoutIndexes == 0)
                Debug.LogError(string.Format("optimized layout indexes are 0"));

            baseSize = filledDataIndexes = validLayoutIndexes;                                //set base andfilled indexes to new layout
            freeIndexStackLength = 0;                                                         //zeroing free indexes stack size
            IndexLengthInt lastLayout = layout[validLayoutIndexes - 1];                       //take last index of layout
            count = lastLayout.index + lastLayout.length + validLayoutIndexes + 1;            //used indexes in data array plus size of base in case its all on same index for some reason

            //increase nodes array if they are too small
            if (keys.Length < count) {
                GenericPoolArray<int>.IncreaseSizeTo(ref nextIndexes, count);
                GenericPoolArray<TKey>.IncreaseSizeTo(ref keys, count);
                GenericPoolArray<TValue>.IncreaseSizeTo(ref values, count);
            }

            //in order things to work all values should be setted up
            //so pay close attention to what in base and where next index after base
            TKey defaultKey = default(TKey);
            TValue defaultValue = default(TValue);

            for (int index = 0; index < validLayoutIndexes; index++) {
                IndexLengthInt curLayout = layout[index];

                if (curLayout.length == 0) { //layout have zero size. make target index invalid
                    nextIndexes[index] = INVALID_ROOT;
                    keys[index] = defaultKey;
                    values[index] = defaultValue;
                }
                else if (curLayout.length == 1) { //layout have 1 size. just set up first value 
                    nextIndexes[index] = INVALID_INDEX;
                    keys[index] = optimizedKeys[curLayout.index];
                    values[index] = optimizedValues[curLayout.index];
                }
                else { //it have at least 2 values
                    nextIndexes[index] = filledDataIndexes;
                    keys[index] = optimizedKeys[curLayout.index];
                    values[index] = optimizedValues[curLayout.index];
                    
                    for (int layoutIndex = 1; layoutIndex < curLayout.length; layoutIndex++) {
                        nextIndexes[filledDataIndexes] = filledDataIndexes + 1;
                        keys[filledDataIndexes] = optimizedKeys[curLayout.index + layoutIndex];
                        values[filledDataIndexes] = optimizedValues[curLayout.index + layoutIndex];
                        filledDataIndexes++;
                    }
                    //trim off last index so it's not point to any index
                    nextIndexes[filledDataIndexes - 1] = INVALID_INDEX;          
                }
            }
        }

        private static string ShowMe(int[] d) {
            string s="";
            for (int i = 0; i < d.Length; i++) {
                s += " " + d[i];
            }
            return s;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            string format = null;

            for (int i = 0; i < baseSize; i++) {
                sb.AppendFormat("index {0}:\n", i);
                
                if (nextIndexes[i] == INVALID_ROOT) {
                    sb.Append(" Nothing");
                }
                else {
                    int index = i;
                    for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                        sb.AppendFormat(" key: {0}, value: {1}, next: {2}\n", keys[index], values[index], nextIndexes[index]);
                        
                    }
                }
                sb.Append("\n");
            }
            sb.AppendLine("");

            sb.AppendFormat("free ({0}): ", freeIndexStackLength);
            format = " {0}";
            for (int i = 0; i < freeIndexStackLength; i++) {
                sb.AppendFormat(format, freeIndexStack[i]);
            }

            sb.AppendLine("");
            sb.AppendLine("nodes");
            format = "{0}) {1}\n";
            for (int i = 0; i < keys.Length; i++) {
                sb.AppendFormat(format, i, string.Format(" key: {0}, value: {1}, next: {2}", keys[i], values[i], nextIndexes[i]));
            }
            return sb.ToString();
        }
    }
}