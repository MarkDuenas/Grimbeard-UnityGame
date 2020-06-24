using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder.CoolTools {
    /// <summary>
    /// Idea: This list store multiple values on single index stacked together. this is list with one sided linked list
    /// base indexes have relatively fixed size. while values on those indexes can be expanded. 
    /// functionaly this is array of lists but in single array
    /// dont call constructor directly. use pools
    /// </summary>
    public class StackedList<T> {
        protected const int INVALID_ROOT = -2;
        protected const int INVALID_INDEX = -1;

        [SerializeField] public int count;                //amount of total items in this collection (right now probably broken)
        [SerializeField] public int baseSize;             //amount of indexes reserved for base

        //nodes
        [SerializeField] public int[] nextIndexes;        //contains next indexof this particular node
        [SerializeField] public T[] values;               //contains actual value
        [SerializeField] public int filledNodeIndexes;    //amount of filled indexes in nodes array

        //nodes free indexes
        [SerializeField] public int[] freeIndexStack;     //array with unused indexes
        [SerializeField] public int freeIndexStackLength; //amount of filled indexes in that array
 
        #region pool
        /// <summary>
        /// Total size is flexible. 2 Numbers just to tell how much indexes are taken from pool on top
        /// </summary>
        public static StackedList<T> PoolTake(int baseSize, int extraSize) {
            StackedList<T> result = GenericPool<StackedList<T>>.Take();
            result.OnTakeFromPool(baseSize, extraSize);
            return result;
        }

        public static void PoolReturn(ref StackedList<T> stackedList) {
            stackedList.OnReturnToPool();
            GenericPool<StackedList<T>>.ReturnToPool(ref stackedList);
        }

        void OnTakeFromPool(int baseSize, int extraSize) {
            if (baseSize < 0)
                throw new ArgumentOutOfRangeException("Base Size in StackedList cannot be less than 0");
            if (extraSize < 0)
                throw new ArgumentOutOfRangeException("Extra Size in StackedList cannot be less than 0");

            this.baseSize = filledNodeIndexes = baseSize;
            freeIndexStackLength = count = 0;
            freeIndexStack = GenericPoolArray<int>.Take(32);
            nextIndexes = GenericPoolArray<int>.Take(baseSize + extraSize);
            values = GenericPoolArray<T>.Take(baseSize + extraSize);
            for (int i = 0; i < baseSize; i++) {
                nextIndexes[i] = -2;//invalid root
            }
            for (int i = baseSize; i < nextIndexes.Length; i++) {
                nextIndexes[i] = -1;//invalid index
            }
        }
        void OnReturnToPool() {       
            GenericPoolArray<int>.ReturnToPool(ref nextIndexes);
            GenericPoolArray<T>.ReturnToPool(ref values);
            GenericPoolArray<int>.ReturnToPool(ref freeIndexStack);
        }
        #endregion

        protected int GetFreeNodeIndex() {
            int result = freeIndexStackLength > 0 ? freeIndexStack[--freeIndexStackLength] : filledNodeIndexes++;
            if (result == nextIndexes.Length) {
                GenericPoolArray<int>.IncreaseSize(ref nextIndexes);
                GenericPoolArray<T>.IncreaseSize(ref values);
            }
            return result;
        }

        /// <summary>
        /// CAUTION: do not return base indexes. they are outside pool
        /// </summary>
        protected void ReturnFreeNodeIndex(int index) {
            nextIndexes[index] = INVALID_INDEX;
            values[index] = default(T);

            if (freeIndexStack.Length == freeIndexStackLength)
                GenericPoolArray<int>.IncreaseSize(ref freeIndexStack);
            freeIndexStack[freeIndexStackLength++] = index;
        }
        

        /// <summary>
        /// add this value at the end in target index
        /// </summary>
        public void AddLast(int index, T value) {
            if (index >= baseSize) {
                throw new ArgumentException(string.Format("Index cannot be greater than base size. Base {0}, Index {1}", baseSize, index), "index");
            }

            if (nextIndexes[index] == INVALID_ROOT) { //if nothing on that index then add it as first item
                nextIndexes[index] = INVALID_INDEX;
                values[index] = value;
            }
            else {
                int freeNodeIndex = GetFreeNodeIndex();
                int lastIndex = index;
                for (; index != INVALID_INDEX; index = nextIndexes[index]) lastIndex = index;
                nextIndexes[lastIndex] = freeNodeIndex;

                nextIndexes[freeNodeIndex] = INVALID_INDEX;
                values[freeNodeIndex] = value;
            }
            count++;
        }
        /// <summary>
        /// will add this values at the end
        /// </summary>
        public void AddLast(int index, params T[] values) {
            if (index >= baseSize)
                Debug.LogError("Index cannot be greater than size");

            if (values.Length == 0)
                return;

            int lastIndex = index;
            int valueIndex = 0;

            if (nextIndexes[index] == INVALID_ROOT) {
                nextIndexes[index] = INVALID_INDEX;
                values[index] = values[0];
                valueIndex = 1;
                count++;
            }
            else {
                for (; index != INVALID_INDEX; index = nextIndexes[index]) 
                    lastIndex = index;                
            }

            for (; valueIndex < values.Length; valueIndex++) {
                T value = values[valueIndex];
                int freeNodeIndex = GetFreeNodeIndex();
                nextIndexes[lastIndex] = freeNodeIndex;
                //set value
                nextIndexes[freeNodeIndex] = INVALID_INDEX;
                values[freeNodeIndex] = value;
                lastIndex = freeNodeIndex;
                count++;
            }
        }

        /// <summary>
        /// will add this value at the begining (it will be harder to extract this information and it will be a lot more fragmented in array)
        /// </summary>
        public void AddFirst(int index, T value) {
            if (index >= baseSize)
                Debug.LogError("Index cannot be greater than size");

            if (nextIndexes[index] == INVALID_ROOT) {
                //if nothing on that index then add it as first item
                nextIndexes[index] = INVALID_INDEX;
                values[index] = value;
            }
            else {
                //move first value to first avaiable index and place new value at first index
                int freeNodeIndex = GetFreeNodeIndex();
                nextIndexes[freeNodeIndex] = nextIndexes[index];
                values[freeNodeIndex] = values[index];
                //add value to first index
                nextIndexes[index] = freeNodeIndex;
                values[index] = value;
            }
            count++;
        }

        //public void Merge(StackedList<T> stackedList) {
        //    if (stackedList.baseSize > baseSize)
        //        Debug.LogError("base size of added list cannot be greater than this base size");

        //    LinkedNode[] otherNodes = stackedList.nodes; //this is static data so it can be referenced
        //    for (int currentBase = 0; currentBase < stackedList.baseSize; currentBase++) {
        //        LinkedNode otherNode = otherNodes[currentBase];
        //        if (otherNode.nextIndex != INVALID_ROOT) {
        //            int lastIndex = currentBase;         

        //            if (nodes[currentBase].nextIndex == INVALID_ROOT) {
        //                nodes[currentBase].SetValues(otherNode.value, INVALID_INDEX);
        //                count++;
        //                if (otherNode.nextIndex == INVALID_INDEX)
        //                    continue;
        //                else
        //                    otherNode = otherNodes[otherNode.nextIndex];
        //            }
        //            else {
        //                for (int curIndex = currentBase; curIndex != INVALID_INDEX; curIndex = nodes[curIndex].nextIndex) {
        //                    lastIndex = curIndex;
        //                }
        //            }
        //            while (true) {
        //                int freeNodeIndex = GetFreeNodeIndex();
        //                nodes[lastIndex].nextIndex = freeNodeIndex;
        //                nodes[freeNodeIndex].SetValues(otherNode.value, INVALID_INDEX);
        //                lastIndex = freeNodeIndex;
        //                count++;

        //                if (otherNode.nextIndex == INVALID_INDEX)
        //                    break;

        //                otherNode = otherNodes[otherNode.nextIndex];
        //            }
        //        }
        //    }
        //}

        public void Merge(StackedList<T> stackedList) {
            if (stackedList.baseSize > baseSize)
                Debug.LogError("base size of added list cannot be greater than this base size");

            int[] otherNextIndexes = stackedList.nextIndexes;
            T[] otherValues = stackedList.values;

            for (int currentBase = 0; currentBase < stackedList.baseSize; currentBase++) {
                int otherNext = otherNextIndexes[currentBase];
                T otherValue = otherValues[currentBase];

                if (otherNext != INVALID_ROOT) {
                    int lastIndex = currentBase;

                    if (nextIndexes[currentBase] == INVALID_ROOT) {
                        nextIndexes[currentBase] = INVALID_INDEX;
                        values[currentBase] = otherValue;
                        count++;
                        if (otherNext == INVALID_INDEX)
                            continue;
                        else {
                            otherValue = otherValues[otherNext];
                            otherNext = otherNextIndexes[otherNext];
                        }
                    }
                    else {
                        for (int curIndex = currentBase; curIndex != INVALID_INDEX; curIndex = nextIndexes[curIndex])
                            lastIndex = curIndex;
                    }
                    while (true) {
                        int freeNodeIndex = GetFreeNodeIndex();
                        nextIndexes[lastIndex] = freeNodeIndex;

                        nextIndexes[freeNodeIndex] = INVALID_INDEX;
                        values[freeNodeIndex] = otherValue;

                        lastIndex = freeNodeIndex;
                        count++;

                        if (otherNext == INVALID_INDEX)
                            break;

                        otherValue = otherValues[otherNext];
                        otherNext = otherNextIndexes[otherNext];
                    }
                }
            }
        }



        public bool AddCheckDublicates(int index, T value) {
            if (index >= baseSize)
                Debug.LogError("Index cannot be greater than size");

            if (nextIndexes[index] == INVALID_ROOT) {//if nothing on that index then add it as first item
                nextIndexes[index] = INVALID_INDEX;
                values[index] = value;
            }
            else {
                int lastIndex = index;
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    if (values[index].Equals(value))
                        return false;
                    lastIndex = index;
                }
                int freeNodeIndex = GetFreeNodeIndex();
                nextIndexes[lastIndex] = freeNodeIndex;

                nextIndexes[freeNodeIndex] = INVALID_INDEX;
                values[freeNodeIndex] = value;
            }
            count++;
            return true;
        }

        /// <summary>
        /// copy existed data to temp arrays, expand values, add old data again
        /// </summary>
        public void ExpandBaseSize(int amount) {      
            int newBase = baseSize + amount;
            T[] optimizedData;
            IndexLengthInt[] optimizedLayout;         
 
            GetOptimizedData(out optimizedData, out optimizedLayout);
            SetOptimizedData(optimizedData, optimizedLayout, baseSize, newBase);

            GenericPoolArray<IndexLengthInt>.ReturnToPool(ref optimizedLayout);
            GenericPoolArray<T>.ReturnToPool(ref optimizedData);
        }

        public void Read(int index, ICollection<T> collection, bool clearCollectionBeforeCollect = true) {
            if (index >= baseSize) 
                Debug.LogError("Index cannot be greater than size");

            if(clearCollectionBeforeCollect)
                collection.Clear();

            if (nextIndexes[index] == INVALID_ROOT)
                return;

            for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                collection.Add(values[index]);
            }
        }

        public void Read(int index, ref T[] pooledArray, out int length) {
            if (index >= baseSize)
                Debug.LogError("Index cannot be greater than size");

            length = 0;
            if (nextIndexes[index] == INVALID_ROOT)
                return;

            for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                if (pooledArray.Length == length)
                    GenericPoolArray<T>.IncreaseSize(ref pooledArray);
                pooledArray[length++] = values[index];
            }
        }
        
        /// <summary>
        ///collect all data into tight array.
        ///layout contain index where to start and amount of values laying out on that index
        ///dont forget to return this arrays to pool
        ///layout length can differ so also dont use array.length
        /// </summary>
        public void GetOptimizedData(out T[] data, out IndexLengthInt[] layout) {
            layout = GenericPoolArray<IndexLengthInt>.Take(baseSize);
            data = GenericPoolArray<T>.Take(count);

            int dataIndex = 0;
            for (int index = 0; index < baseSize; index++) {           
                if (nextIndexes[index] == INVALID_ROOT)
                    layout[index] = new IndexLengthInt(dataIndex, 0);
                else {
                    int dataIndexStart = dataIndex;
                    for (int curIndex = index; curIndex != INVALID_INDEX; curIndex = nextIndexes[curIndex]) {
                        if (data.Length == dataIndex)
                            GenericPoolArray<T>.IncreaseSize(ref data);

                        data[dataIndex++] = values[curIndex];
                    }
                    layout[index] = new IndexLengthInt(dataIndexStart, dataIndex - dataIndexStart);
                }
            }
        }


        /// <summary>
        /// clear current data and set this instead
        /// </summary>
        public void SetOptimizedData(T[] data, IndexLengthInt[] layout, int validLayoutCount, int newBase) {
            if(data == null)
                Debug.LogError("Optimized data passed to stacked list cannot be null");
            if (layout == null)
                Debug.LogError("Optimized layout passed to stacked list cannot be null");

            OnReturnToPool();
            OnTakeFromPool(newBase, layout[validLayoutCount - 1].indexPlusLength);                   

            //in order things to work all values should be setted up
            //so pay close attention to what in base and where next index after base
            for (int curLayoutIndex = 0; curLayoutIndex < validLayoutCount; curLayoutIndex++) {
                IndexLengthInt curLayout = layout[curLayoutIndex];
                count += curLayout.length;

                if (curLayout.length == 0)        //layout have zero size. make target index invalid
                    nextIndexes[curLayoutIndex] = INVALID_ROOT;
                else if (curLayout.length == 1) {  //layout have 1 size. just set up first value 
                    nextIndexes[curLayoutIndex] = INVALID_INDEX;
                    values[curLayoutIndex] = data[curLayout.index];                
                }
                else {                          //it have at least 2 values
                    nextIndexes[curLayoutIndex] = filledNodeIndexes;
                    values[curLayoutIndex] = data[curLayout.index];
                    
                    for (int layoutIndex = 1; layoutIndex < curLayout.length; layoutIndex++) {
                        nextIndexes[filledNodeIndexes] = filledNodeIndexes + 1;
                        values[filledNodeIndexes] = data[curLayout.index + layoutIndex];
                        filledNodeIndexes++;
                    }
                    nextIndexes[filledNodeIndexes - 1] = INVALID_INDEX;
                }
            }
        }

        public void SetOptimizedData(T[] data, IndexLengthInt[] layout, int validLayoutIndexes) {
            SetOptimizedData(data, layout, validLayoutIndexes, validLayoutIndexes);
        }

        /// <summary>
        /// Count values on target index
        /// </summary>
        public int Count(int index) {
            if (index >= baseSize)
                Debug.LogError("Index cannot be greater than size");

            if (nextIndexes[index] == INVALID_ROOT)
                return 0;

            int result = 0;
            for (; index != INVALID_INDEX; index = nextIndexes[index]) result++;            
            return result;
        }
        
        public void Clear() {
            Array.Clear(values, 0, values.Length); 
            for (int i = 0; i < baseSize; i++) {
                nextIndexes[i] = -2;
            }
            for (int i = baseSize; i < nextIndexes.Length; i++) {
                nextIndexes[i] = -1;
            }

            filledNodeIndexes = baseSize;
            freeIndexStackLength = count = 0;
        }

        public bool Remove(int index, T value) {
            if (nextIndexes[index] == INVALID_ROOT)
                return false;

            int prevIndex = INVALID_INDEX;
            while (true) {
                int curNext = nextIndexes[index];
  
                //LinkedNode curNode = nodes[index];
                if (values[index].Equals(value)) {
                    if (prevIndex == INVALID_INDEX) { //first value in row
                        if (curNext == INVALID_INDEX) {//this is only value in row
                            nextIndexes[index] = INVALID_ROOT;
                        }
                        else { //there is something after it
                            //copy next data to base index
                            nextIndexes[index] = nextIndexes[curNext];
                            values[index] = values[curNext];
                            ReturnFreeNodeIndex(curNext);//return next node to pool
                        }
                    }
                    else {//not first value so just set index of precious value to next value
                        nextIndexes[prevIndex] = curNext;//set prevoius node next index to current node next index (even if there is nothing)
                        ReturnFreeNodeIndex(index);//return current node to pool
                    }
                    count--;
                    return true;
                }

                prevIndex = index;
                index = curNext;

                if (index == INVALID_INDEX)
                    return false;
            }
        }

        public bool Remove(int index, Predicate<T> predicate) {
            if (nextIndexes[index] == INVALID_ROOT)
                return false;

            int prevIndex = INVALID_INDEX;
            while (true) {
                int curNext = nextIndexes[index];

                if (predicate(values[index])) {
                    if (prevIndex == INVALID_INDEX) { //first value in row
                        if (curNext == INVALID_INDEX) {//this is only value in row
                            nextIndexes[index] = INVALID_ROOT;
                        }
                        else { //there is something after it
                            //copy next data to base index
                            nextIndexes[index] = nextIndexes[curNext];
                            values[index] = values[curNext];
                            ReturnFreeNodeIndex(curNext);//return next node to pool
                        }
                    }
                    else {//not first value so just set index of precious value to next value
                        nextIndexes[prevIndex] = curNext;//set prevoius node next index to current node next index (even if there is nothing)
                        ReturnFreeNodeIndex(index);//return current node to pool
                    }
                    count--;
                    return true;
                }

                prevIndex = index;
                index = curNext;

                if (index == INVALID_INDEX)
                    return false;
            }
        }

        public void Clear(int index) {
            if (nextIndexes[index] == INVALID_ROOT)
                return;
     
            int curIndex = nextIndexes[index];
            nextIndexes[index] = INVALID_ROOT; //we dont add first node to free index stack 
            values[index] = default(T);
            count--;

            if (curIndex != INVALID_INDEX) {
                while (true) {
                    int nextIndex = nextIndexes[curIndex];
                    ReturnFreeNodeIndex(curIndex);
                    count--;

                    if (nextIndex == INVALID_INDEX)
                        break;
                    curIndex = nextIndex;
                }
            }           
        }

        public void Swap(int index1, int index2) {
            int tempIndex = nextIndexes[index1];
            T tempValue = values[index1];

            nextIndexes[index1] = nextIndexes[index2];
            values[index1] = values[index2];

            nextIndexes[index2] = tempIndex;
            values[index2] = tempValue;
        }

        public bool Any(int index) {
            return nextIndexes[index] != -2;
        }

        public bool Any() {
            for (int i = 0; i < baseSize; i++) {
                if (nextIndexes[i] != -2)
                    return true;
            }
            return false;
        }

        public bool AnyEmptyBase() {
            for (int i = 0; i < baseSize; i++) {
                if (nextIndexes[i] == -2)
                    return true;
            }
            return false;
        }

        public bool First(int index, out T value) {
            value = values[index];
            return nextIndexes[index] != -2;
        }
        
        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            string format = null;

            for (int i = 0; i < baseSize; i++) {
                sb.AppendFormat("{0}: ", i);

                if (nextIndexes[i] == INVALID_ROOT) {
                    sb.Append(" Nothing");
                }
                else {
                    int index = i;
                    for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                        sb.AppendFormat(" {0} ", values[index]);
                    }
                }
                sb.Append("\n");
            }
            sb.AppendLine("");

            sb.AppendLine("free: ");
            format = " {0}";
            for (int i = 0; i < freeIndexStackLength; i++) {
                sb.AppendFormat(format, freeIndexStack[i]);
            }

            sb.AppendLine("");
            sb.AppendLine("nodes");
            format = "{0}) {1}\n";
            for (int i = 0; i < nextIndexes.Length; i++) {
                sb.AppendFormat(format, i, string.Format("item: {0}, next {1}", values[i].ToString(), nextIndexes[i]));
            }
            return sb.ToString();
        }

        public string ToString(int targetIndex) {
            StringBuilder sb = new StringBuilder();
            if (nextIndexes[targetIndex] == INVALID_ROOT) {
                sb.Append(" Nothing");
            }
            else {
                int index = targetIndex;
                for (; index != INVALID_INDEX; index = nextIndexes[index]) {
                    sb.AppendFormat(" {0} ", values[index]);
                }
            }
            return sb.ToString();
        }

    }
}