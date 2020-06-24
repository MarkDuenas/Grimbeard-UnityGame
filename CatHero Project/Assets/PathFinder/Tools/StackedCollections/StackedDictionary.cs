using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.CoolTools {
    public class StackedDictionary<TKey, TValue> {
        const int INVALID_INDEX = -1;
        const int INITIAL_FREE_INDEX_POOL_SIZE = 100;

        Dictionary<TKey, int> roots = new Dictionary<TKey, int>();

        //nodes
        public int[] nextIndexes;        //contains next indexof this particular node
        public TValue[] values;          //contains actual value
        public int filledNodeIndexes;    //amount of filled indexes in nodes array
        
        int[] freeIndexStack;
        int freeIndexStackLength;

        struct StackedDictionaryNode {
            public int nextIndex;
            public TValue value;

            public void SetItem(int NextIndex, TValue Value) {
                nextIndex = NextIndex;
                value = Value;
            }

            public override string ToString() {
                return string.Format("value: {0}, next: {1}", value.ToString(), nextIndex);
            }
        }

        public StackedDictionary(int initialSize = 128) {
            values = GenericPoolArray<TValue>.Take(initialSize);
            nextIndexes = GenericPoolArray<int>.Take(initialSize);
            //nodes = GenericPoolArray<StackedDictionaryNode>.Take(initialSize);
            freeIndexStack = GenericPoolArray<int>.Take(INITIAL_FREE_INDEX_POOL_SIZE);
        }

        int GetFreeIndex() {
            int result = freeIndexStackLength > 0 ? freeIndexStack[--freeIndexStackLength] : filledNodeIndexes++;
            if (result == nextIndexes.Length) {
                GenericPoolArray<int>.IncreaseSize(ref nextIndexes);
                GenericPoolArray<TValue>.IncreaseSize(ref values);
            }

            return result;
        }
        
        private void ReturnFreeIndex(int index) {
            if (freeIndexStack.Length == freeIndexStackLength) 
                GenericPoolArray<int>.IncreaseSize(ref freeIndexStack);

            freeIndexStack[freeIndexStackLength++] = index;
        }
        
        public void Add(TKey key, TValue value) {
            int freeIndex = GetFreeIndex();
            int index;

            if (roots.TryGetValue(key, out index)) {
                int lastIndex = index;
                for (; index != INVALID_INDEX; index = nextIndexes[index]) { lastIndex = index; }
                nextIndexes[lastIndex] = freeIndex;

                nextIndexes[freeIndex] = INVALID_INDEX;
                values[freeIndex] = value;
            }
            else {
                //if dont contain key then just add
                roots.Add(key, freeIndex);
                nextIndexes[freeIndex] = INVALID_INDEX;
                values[freeIndex] = value;
            }   
        }

        public bool Remove(TKey key, TValue value) {  
            int index;
            if (roots.TryGetValue(key, out index)) {
                int prevIndex = INVALID_INDEX;
                while (true) {
                    TValue curValue = values[index];
                    int curNext = nextIndexes[index];
                    //StackedDictionaryNode curNode = nodes[index];
                    if(curValue.Equals(value)) {
                        if(prevIndex == INVALID_INDEX) { //first value in row
                            if (curNext == INVALID_INDEX) {//this is only value in row
                                roots.Remove(key);
                            }
                            else { //there is something after it
                                roots[key] = curNext;
                            }
                        }
                        else {//not first value so just set index of precious value to next value
                            nextIndexes[prevIndex] = curNext;
                        }

                        nextIndexes[index] = INVALID_INDEX;
                        values[index] = default(TValue);                        
                        ReturnFreeIndex(index);
                        return true;
                    }

                    prevIndex = index;
                    index = curNext;

                    if (index == INVALID_INDEX) 
                        return false;
                }
            }
            else {
                return false;
            }
        }

        public void Read(TKey key, ref TValue[] pooledArray, out int count) {
            count = 0;
            int index;
            if (roots.TryGetValue(key, out index)) {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    if (pooledArray.Length == count)
                        GenericPoolArray<TValue>.IncreaseSize(ref pooledArray);
                    pooledArray[count++] = values[index];
                }
            }
        }

        public void Read(TKey key, ICollection<TValue> collection) {
            int index;
            if (roots.TryGetValue(key, out index)) {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    collection.Add(values[index]);
                }
            }   
        }
        public void Read(TKey key, ICollection<TValue> collection, Predicate<TValue> match) {
            int index;
            if (roots.TryGetValue(key, out index)) {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    TValue value = values[index];
                    if(match(value))
                        collection.Add(value);
                }
            }
        }
        public void Read<T>(TKey key, ICollection<T> collection) where T : class {
            int index;
            if (roots.TryGetValue(key, out index)) {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    TValue value = values[index];
                    if (value is T)
                        collection.Add(value as T);
                }
            }
        }
        public void Read<T>(TKey key, ICollection<T> collection, Predicate<T> match) where T : class {
            int index;
            if (roots.TryGetValue(key, out index)) {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    TValue value = values[index];
                    if (value is T && match(value as T))
                        collection.Add(value as T);
                }
            }
        }

        public int Count(TKey key) {
            int index;
            if (roots.TryGetValue(key, out index)) {
                int result = 0;
                for (; index != INVALID_INDEX; index = nextIndexes[index]) { result++; }
                return result;
            }
            else {
                return 0;
            }
        }

        public void Clear() {
            roots.Clear();
            TValue def = default(TValue);
            for (int i = 0; i < filledNodeIndexes; i++) {
                nextIndexes[i] = INVALID_INDEX;
                values[i] = def;
            }
            filledNodeIndexes = 0;
            freeIndexStackLength = 0;
        }

        public IEnumerable<TKey> Keys {
            get { return roots.Keys; }
        }
    }
}