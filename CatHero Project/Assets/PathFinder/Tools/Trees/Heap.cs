using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using K_PathFinder.Pool;

namespace K_PathFinder {       
    public class HeapGeneric<T> where T : struct, IComparable<T> {
        public List<T> heap = new List<T>();
        public int heapCount = 0;

        //public StringBuilder log = new StringBuilder();
        
        private int ParentIndex(int index) {
            return (index - 1) / 2;
        }

        private void ChildIndex(int index, out int left, out int right) {
            left = index * 2 + 1;
            right = index * 2 + 2;
        }

        public void Add(T value) {
            //log.AppendLine(string.Format("added {0}", value));

            if (heap.Count == heapCount)
                heap.Add(value);
            else
                heap[heapCount] = value;
            
            HeapSortUp(heapCount);
            heapCount++;
        }

        public T RemoveFirst() {
            T first = heap[0];

            //log.AppendLine(string.Format("removed first {0}", first));

            heapCount--;
            heap[0] = heap[heapCount];        
            HeapSortDown(0);
            return first;
        }

        private void HeapSortUp(int index) {
            if (index == 0) return;

            //log.AppendLine(string.Format("sort up {0}", index));

            T item = heap[index];
            int parentIndex;

            while (true) {
                parentIndex = (index - 1) / 2;
                T parentItem = heap[parentIndex];

                if (item.CompareTo(parentItem) > 0) {
                    HeapSwap(index, parentIndex);
                    index = parentIndex;
                }
                else
                    break;
            }
        }

        private void HeapSortDown(int index) {
            //log.AppendLine(string.Format("sort down {0}", index));

            T item = heap[index];
            int childIndexLeft, childIndexRight, swapIndex;

            while (true) {
                childIndexLeft = index * 2 + 1;
                childIndexRight = index * 2 + 2;
                swapIndex = 0;

                if (childIndexLeft < heapCount) {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < heapCount && heap[childIndexLeft].CompareTo(heap[childIndexRight]) < 0)
                        swapIndex = childIndexRight;

                    if (item.CompareTo(heap[swapIndex]) < 0) {
                        HeapSwap(index, swapIndex);
                        index = swapIndex;
                    }
                    else
                        return;
                }
                else
                    return;
            }
        }

        private void HeapSwap(int indexA, int indexB) {
            //log.AppendLine(string.Format("swap {0} : {1}", indexA, indexB));

            T valA = heap[indexA];
            heap[indexA] = heap[indexB];
            heap[indexB] = valA;
        }


        public int count {
            get { return heapCount; }
        }

        public void Clear() {
            heapCount = 0;
            heap.Clear();
        }


        public HeapGUI GetGUI {
            get { return new HeapGUI(this); }
        }
        public class HeapGUI {
            HeapGeneric<T> heap;
            List<T> heapValues;

            List<Vector3> debug = new List<Vector3>();
            const float SIZE = 30;
            Vector2 size = new Vector2(SIZE, SIZE);


            public HeapGUI(HeapGeneric<T> Heap) {
                heap = Heap;
                heapValues = Heap.heap;
            }

            public void OnGUI() {
                if (heap.count == 0)
                    return;

                debug.Clear();
                for (int h = 0; h < heap.count; h++) {
                    debug.Add(new Vector3());
                }

                float screen = Screen.width;
                debug[0] = new Vector3(SIZE, 0, screen);



                for (int h = 0; h < heap.count; h++) {
                    Vector4 curV4 = debug[h];


                    float height = curV4.x;
                    float start = curV4.y;
                    float end = curV4.z;
                    float x = (start + end) / 2;

                    Vector2 curPos = new Vector2(x - SIZE, height - SIZE);
                    GUI.Box(new Rect(curPos, size * 2f), heapValues[h].ToString());


                    float half = (end - start) * 0.5f;


                    int L, R;
                    ChildIndex(h, out L, out R);

                    if (L < heap.count) {
                        float Lheight = height + SIZE + SIZE;
                        float Ls = start;
                        float Le = Ls + half;

                        debug[L] = new Vector3(Lheight, Ls, Le);
                        if (R < heap.count) {
                            float Rheight = Lheight;
                            float Rs = Le;
                            float Re = end;
                            debug[R] = new Vector4(Rheight, Rs, Re);
                        }
                    }
                }
            }

            private int ParentIndex(int index) {
                return (index - 1) / 2;
            }

            private void ChildIndex(int index, out int left, out int right) {
                left = index * 2 + 1;
                right = index * 2 + 2;
            }
        }

    }
    public class HeapFloatFirstLowest<T> : IEnumerable<T> {
        public Holder[] heap;
        public int heapCount = 0;
        
        public struct Holder {
            public T value;
            public float heapValue;
            public Holder(T Value, float Weight) {
                value = Value;
                heapValue = Weight;
            }
        }

        public static HeapFloatFirstLowest<T> TakeFromPool() {       
            return GenericPool<HeapFloatFirstLowest<T>>.Take();
        }

        public void TakeFromPoolAllocatedData(int initialSize) {
            heap = GenericPoolArray<Holder>.Take(initialSize);
            heapCount = 0;
        }

        public void ReturnToPoolAllocatedData() {
            GenericPoolArray<Holder>.ReturnToPool(ref heap);
            heapCount = 0;
        }

        public IEnumerator<T> GetEnumerator() {
            for (int i = 0; i < heapCount; i++) {
                yield return heap[i].value;
            }
        }
        
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private int ParentIndex(int index) {
            return (index - 1) / 2;
        }

        private void ChildIndex(int index, out int left, out int right) {
            left = index * 2 + 1;
            right = index * 2 + 2;
        }

        public void Add(T value, float weight) {
            if (heap.Length == heapCount) 
                GenericPoolArray<Holder>.IncreaseSize(ref heap);
            
            heap[heapCount] = new Holder(value, weight);

            if (heapCount > 0) {
                int index = heapCount;
                int parentIndex;

                Holder item = heap[index];                

                while (true) {
                    parentIndex = (index - 1) / 2;
                    Holder parentItem = heap[parentIndex];

                    if (item.heapValue < parentItem.heapValue) {                
                        Holder valA = heap[index];
                        heap[index] = heap[parentIndex];
                        heap[parentIndex] = valA;
                        index = parentIndex;
                    }
                    else 
                        break;                    
                }
            }

            heapCount++;
        }

        public T RemoveFirst() {
            Holder first = heap[0];
            heapCount--;
            heap[0] = heap[heapCount];
            //HeapSortDown(0);

            int index = 0;
            Holder item = heap[index];
            int childIndexLeft, childIndexRight, swapIndex;

            while (true) {
                childIndexLeft = index * 2 + 1;
                childIndexRight = index * 2 + 2;
                swapIndex = 0;

                if (childIndexLeft < heapCount) {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < heapCount && heap[childIndexLeft].heapValue > heap[childIndexRight].heapValue)
                        swapIndex = childIndexRight;

                    if (item.heapValue > heap[swapIndex].heapValue) {
                        //HeapSwap(index, swapIndex);
                        Holder valA = heap[index];
                        heap[index] = heap[swapIndex];
                        heap[swapIndex] = valA;

                        index = swapIndex;
                    }
                    else
                        break;
                }
                else
                    break;
            }

            return first.value;
        }

        public T RemoveFirst(out float weight) {
            Holder first = heap[0];
            heapCount--;
            heap[0] = heap[heapCount];
            //HeapSortDown(0);

            int index = 0;
            Holder item = heap[index];
            int childIndexLeft, childIndexRight, swapIndex;

            while (true) {
                childIndexLeft = index * 2 + 1;
                childIndexRight = index * 2 + 2;
                swapIndex = 0;

                if (childIndexLeft < heapCount) {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < heapCount && heap[childIndexLeft].heapValue > heap[childIndexRight].heapValue)
                        swapIndex = childIndexRight;

                    if (item.heapValue > heap[swapIndex].heapValue) {
                        //HeapSwap(index, swapIndex);
                        Holder valA = heap[index];
                        heap[index] = heap[swapIndex];
                        heap[swapIndex] = valA;

                        index = swapIndex;
                    }
                    else
                        break;
                }
                else
                    break;
            }
            weight = first.heapValue;
            return first.value;
        }

        //private void HeapSortUp(int index) {
        //    if (index == 0) return;

        //    //log.AppendLine(string.Format("sort up {0}", index));

        //    Holder item = heap[index];
        //    int parentIndex;

        //    while (true) {
        //        parentIndex = (index - 1) / 2;
        //        Holder parentItem = heap[parentIndex];

        //        if (item.weight < parentItem.weight) {
        //            HeapSwap(index, parentIndex);
        //            index = parentIndex;
        //        }
        //        else {
        //            break;
        //        }
        //    }
        //}

        //private void HeapSortDown(int index) {
        //    //log.AppendLine(string.Format("sort down {0}", index));

        //    Holder item = heap[index];
        //    int childIndexLeft, childIndexRight, swapIndex;

        //    while (true) {
        //        childIndexLeft = index * 2 + 1;
        //        childIndexRight = index * 2 + 2;
        //        swapIndex = 0;

        //        if (childIndexLeft < heapCount) {
        //            swapIndex = childIndexLeft;

        //            if (childIndexRight < heapCount && heap[childIndexLeft].weight > heap[childIndexRight].weight)
        //                swapIndex = childIndexRight;

        //            if (item.weight > heap[swapIndex].weight) {
        //                HeapSwap(index, swapIndex);
        //                index = swapIndex;
        //            }
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //}

        //private void HeapSwap(int indexA, int indexB) {
        //    //log.AppendLine(string.Format("swap {0} : {1}", indexA, indexB));

        //    Holder valA = heap[indexA];
        //    heap[indexA] = heap[indexB];
        //    heap[indexB] = valA;
        //}

        public int count {
            get { return heapCount; }
        }
                
        public HeapGUI GetGUI {
            get { return new HeapGUI(this); }
        }
        public class HeapGUI {
            HeapFloatFirstLowest<T> heap;
            Holder[] heapValues;

            List<Vector3> debug = new List<Vector3>();
            const float SIZE = 50;
            Vector2 size = new Vector2(SIZE, SIZE);

            public HeapGUI(HeapFloatFirstLowest<T> Heap) {
                heap = Heap;
                heapValues = Heap.heap;
            }

            public void OnGUI() {
                if (heap.count == 0)
                    return;

                debug.Clear();
                for (int h = 0; h < heap.count; h++) {
                    debug.Add(new Vector3());
                }

                float screen = Screen.width;
                debug[0] = new Vector3(SIZE, 0, screen);



                for (int h = 0; h < heap.count; h++) {
                    Vector4 curV4 = debug[h];


                    float height = curV4.x;
                    float start = curV4.y;
                    float end = curV4.z;
                    float x = (start + end) / 2;

                    Vector2 curPos = new Vector2(x - SIZE, height - SIZE);
                    GUI.Box(new Rect(curPos, size * 2f), heapValues[h].heapValue.ToString());


                    float half = (end - start) * 0.5f;


                    int L, R;
                    ChildIndex(h, out L, out R);

                    if (L < heap.count) {
                        float Lheight = height + SIZE + SIZE;
                        float Ls = start;
                        float Le = Ls + half;

                        debug[L] = new Vector3(Lheight, Ls, Le);
                        if (R < heap.count) {
                            float Rheight = Lheight;
                            float Rs = Le;
                            float Re = end;
                            debug[R] = new Vector4(Rheight, Rs, Re);
                        }
                    }
                }
            }

            private int ParentIndex(int index) {
                return (index - 1) / 2;
            }

            private void ChildIndex(int index, out int left, out int right) {
                left = index * 2 + 1;
                right = index * 2 + 2;
            }
        }

    }
    public class HeapFloatFirstHighest<T> : IEnumerable<T> {
        const int INITIAL_SIZE = 4;
        Holder[] heap;
        int heapCount = 0;

        struct Holder {
            public T value;
            public float weight;
            public Holder(T Value, float Weight) {
                value = Value;
                weight = Weight;
            }
        }

        public static HeapFloatFirstHighest<T> TakeFromPool() {
            return GenericPool<HeapFloatFirstHighest<T>>.Take();
        }

        public void Init() {
            heap = GenericPoolArray<Holder>.Take(INITIAL_SIZE);
            heapCount = 0;
        }

        public void Clear() {
            GenericPoolArray<Holder>.ReturnToPool(ref heap);
            heapCount = 0;
        }

        public IEnumerator<T> GetEnumerator() {
            for (int i = 0; i < heapCount; i++) {
                yield return heap[i].value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private int ParentIndex(int index) {
            return (index - 1) / 2;
        }

        private void ChildIndex(int index, out int left, out int right) {
            left = index * 2 + 1;
            right = index * 2 + 2;
        }

        public void Add(T value, float weight) {
            if (heap.Length == heapCount) 
                GenericPoolArray<Holder>.IncreaseSize(ref heap);
            
            heap[heapCount] = new Holder(value, weight);


            //HeapSortUp(heapCount);

            if (heapCount > 0) {
                int index = heapCount;
                int parentIndex;

                Holder item = heap[index];

                while (true) {
                    parentIndex = (index - 1) / 2;
                    Holder parentItem = heap[parentIndex];

                    if (item.weight > parentItem.weight) {
                        //HeapSwap(index, parentIndex);
                        Holder valA = heap[index];
                        heap[index] = heap[parentIndex];
                        heap[parentIndex] = valA;

                        index = parentIndex;
                    }
                    else {
                        break;
                    }
                }
            }

            heapCount++;
        }

        public T RemoveFirst() {
            Holder first = heap[0];
            heapCount--;
            heap[0] = heap[heapCount];
            //HeapSortDown(0);

            int index = 0;
            Holder item = heap[index];
            int childIndexLeft, childIndexRight, swapIndex;

            while (true) {
                childIndexLeft = index * 2 + 1;
                childIndexRight = index * 2 + 2;
                swapIndex = 0;

                if (childIndexLeft < heapCount) {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < heapCount && heap[childIndexLeft].weight < heap[childIndexRight].weight)
                        swapIndex = childIndexRight;

                    if (item.weight < heap[swapIndex].weight) {
                        //HeapSwap(index, swapIndex);
                        Holder valA = heap[index];
                        heap[index] = heap[swapIndex];
                        heap[swapIndex] = valA;

                        index = swapIndex;
                    }
                    else
                        break;
                }
                else
                    break;
            }

            return first.value;
        }

        public T RemoveFirst(out float weight) {
            Holder first = heap[0];
            heapCount--;
            heap[0] = heap[heapCount];
            //HeapSortDown(0);

            int index = 0;
            Holder item = heap[index];
            int childIndexLeft, childIndexRight, swapIndex;

            while (true) {
                childIndexLeft = index * 2 + 1;
                childIndexRight = index * 2 + 2;
                swapIndex = 0;

                if (childIndexLeft < heapCount) {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < heapCount && heap[childIndexLeft].weight < heap[childIndexRight].weight)
                        swapIndex = childIndexRight;

                    if (item.weight < heap[swapIndex].weight) {
                        //HeapSwap(index, swapIndex);
                        Holder valA = heap[index];
                        heap[index] = heap[swapIndex];
                        heap[swapIndex] = valA;

                        index = swapIndex;
                    }
                    else
                        break;
                }
                else
                    break;
            }
            weight = first.weight;
            return first.value;
        }

        //private void HeapSortUp(int index) {
        //    if (index == 0) return;

        //    //log.AppendLine(string.Format("sort up {0}", index));

        //    Holder item = heap[index];
        //    int parentIndex;

        //    while (true) {
        //        parentIndex = (index - 1) / 2;
        //        Holder parentItem = heap[parentIndex];

        //        if (item.weight < parentItem.weight) {
        //            HeapSwap(index, parentIndex);
        //            index = parentIndex;
        //        }
        //        else {
        //            break;
        //        }
        //    }
        //}

        //private void HeapSortDown(int index) {
        //    //log.AppendLine(string.Format("sort down {0}", index));

        //    Holder item = heap[index];
        //    int childIndexLeft, childIndexRight, swapIndex;

        //    while (true) {
        //        childIndexLeft = index * 2 + 1;
        //        childIndexRight = index * 2 + 2;
        //        swapIndex = 0;

        //        if (childIndexLeft < heapCount) {
        //            swapIndex = childIndexLeft;

        //            if (childIndexRight < heapCount && heap[childIndexLeft].weight > heap[childIndexRight].weight)
        //                swapIndex = childIndexRight;

        //            if (item.weight > heap[swapIndex].weight) {
        //                HeapSwap(index, swapIndex);
        //                index = swapIndex;
        //            }
        //            else
        //                break;
        //        }
        //        else
        //            break;
        //    }
        //}

        //private void HeapSwap(int indexA, int indexB) {
        //    //log.AppendLine(string.Format("swap {0} : {1}", indexA, indexB));

        //    Holder valA = heap[indexA];
        //    heap[indexA] = heap[indexB];
        //    heap[indexB] = valA;
        //}

        public int count {
            get { return heapCount; }
        }
        
        public HeapGUI GetGUI {
            get { return new HeapGUI(this); }
        }
        public class HeapGUI {
            HeapFloatFirstHighest<T> heap;
            Holder[] heapValues;

            List<Vector3> debug = new List<Vector3>();
            const float SIZE = 50;
            Vector2 size = new Vector2(SIZE, SIZE);


            public HeapGUI(HeapFloatFirstHighest<T> Heap) {
                heap = Heap;
                heapValues = Heap.heap;
            }

            public void OnGUI() {
                if (heap.count == 0)
                    return;

                debug.Clear();
                for (int h = 0; h < heap.count; h++) {
                    debug.Add(new Vector3());
                }

                float screen = Screen.width;
                debug[0] = new Vector3(SIZE, 0, screen);



                for (int h = 0; h < heap.count; h++) {
                    Vector4 curV4 = debug[h];


                    float height = curV4.x;
                    float start = curV4.y;
                    float end = curV4.z;
                    float x = (start + end) / 2;

                    Vector2 curPos = new Vector2(x - SIZE, height - SIZE);
                    GUI.Box(new Rect(curPos, size * 2f), heapValues[h].weight.ToString());


                    float half = (end - start) * 0.5f;


                    int L, R;
                    ChildIndex(h, out L, out R);

                    if (L < heap.count) {
                        float Lheight = height + SIZE + SIZE;
                        float Ls = start;
                        float Le = Ls + half;

                        debug[L] = new Vector3(Lheight, Ls, Le);
                        if (R < heap.count) {
                            float Rheight = Lheight;
                            float Rs = Le;
                            float Re = end;
                            debug[R] = new Vector4(Rheight, Rs, Re);
                        }
                    }
                }
            }

            private int ParentIndex(int index) {
                return (index - 1) / 2;
            }

            private void ChildIndex(int index, out int left, out int right) {
                left = index * 2 + 1;
                right = index * 2 + 2;
            }
        }

    }
}