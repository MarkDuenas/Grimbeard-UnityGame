using K_PathFinder.Pool;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

#if UNITY_EDITOR
using K_PathFinder.PFDebuger;
#endif

//serve more as prototype but can be userful later on
namespace K_PathFinder.CoolTools {
    public interface IKDTreeMember {
        float x { get; }
        float y { get; }
    }

    //not optimal at all cause it is sorted on every iteration
    public class Generic2dKDTree<T> where T : struct, IKDTreeMember {
        private int root, maxDepth;            
        private T[] values;   
        private int[] branches;     //index * 2 = lower value, (index * 2) + 1 = higher value

        const int INVALID = -1;

        struct Box {
            public float x, y, size;
            public Box(float X, float Y, float Size) {
                x = X; y = Y; size = Size;
            }
        }

        struct BoxIteration {
            public int node, depth;   
            public void Set(int N, int D) {
                node = N; depth = D;
            }
        }
        
        public void BuildTree(List<T> data) {
            Stopwatch swTotal = new Stopwatch();
            swTotal.Start();   
   
            int count = data.Count;
      
            if (values != null) {
                if (values.Length < count)
                    GenericPoolArray<T>.IncreaseSizeTo(ref values, count);
            }
            else
                values = GenericPoolArray<T>.Take(count);

            if (branches != null) {
                if (branches.Length < count * 2)
                    GenericPoolArray<int>.IncreaseSizeTo(ref branches, count * 2);
            }
            else
                branches = GenericPoolArray<int>.Take(count * 2);

            for (int i = 0; i < count; i++) {
                values[i] = data[i];
                branches[i * 2] = branches[(i * 2) + 1] = -1;
            }

            root = BuildRecursive(0, count, 0);

            swTotal.Stop();
            //UnityEngine.Debug.LogFormat("tree build time {0}", swTotal.Elapsed);
        }

        public void Clear() {
            GenericPoolArray<T>.ReturnToPool(ref values);
            GenericPoolArray<int>.ReturnToPool(ref branches);
        }

        //return index of branch
        private int BuildRecursive(int start, int end, int depth) {
            //Debug.LogFormat("start {0}, end {1}, depth {2}", start, end, depth);

            if (depth > maxDepth)
                maxDepth = depth;

            int count = end - start;

            if (count < 1)
                return -1;          

            if (count == 1) {
                branches[start * 2] = branches[(start * 2) + 1] = -1;
                return start;
            }

            int pivot;
            if (depth % 2 == 0) {//true = X
                QuicksortX(values, start, end - 1);
                pivot = start + (count / 2);
                branches[pivot * 2] = BuildRecursive(start, pivot - 1, depth + 1);
                branches[(pivot * 2) + 1] = BuildRecursive(pivot + 1, end, depth + 1);
            }
            else {//false = Y
                QuicksortY(values, start, end - 1);
                pivot = start + (count / 2);
                branches[pivot * 2] = BuildRecursive(start, pivot - 1, depth + 1);
                branches[(pivot * 2) + 1] = BuildRecursive(pivot + 1, end, depth + 1);
            }
                     

            return pivot;
        }

        private static void QuicksortX(T[] dataArray, int leftStart, int rightStart) {
            int left = leftStart, right = rightStart;
            float min, max, pivot;
            min = max = dataArray[leftStart].x;
            for (int i = leftStart + 1; i < rightStart; i++) {
                T val = dataArray[i];
                if (val.x < min) min = val.x;
                if (val.x > max) max = val.x;
            }

            pivot = (min + max) * 0.5f;

            while (left <= right) {
                while (true) {
                    if (dataArray[left].x < pivot) left++;
                    else break;
                }

                while (true) {
                    if (dataArray[right].x > pivot) right--;
                    else break;
                }

                if (left <= right) {
                    T tempData = dataArray[left];
                    dataArray[left++] = dataArray[right];
                    dataArray[right--] = tempData;
                }
            }

            if (leftStart < right) { QuicksortX(dataArray, leftStart, right); }
            if (left < rightStart) { QuicksortX(dataArray, left, rightStart); }
        }

        private static void QuicksortY(T[] dataArray, int leftStart, int rightStart) {
            int left = leftStart, right = rightStart;
            float min, max, pivot;
            min = max = dataArray[leftStart].y;

            for (int i = leftStart + 1; i < rightStart; i++) {
                T val = dataArray[i];
                if (val.y < min) min = val.y;
                if (val.y > max) max = val.y;
            }

            pivot = (min + max) * 0.5f;


            while (left <= right) {
                while (true) {
                    if (dataArray[left].y < pivot) left++;
                    else break;
                }

                while (true) {
                    if (dataArray[right].y > pivot) right--;
                    else break;
                }

                if (left <= right) {
                    T tempData = dataArray[left];
                    dataArray[left++] = dataArray[right];
                    dataArray[right--] = tempData;
                }
            }

            if (leftStart < right) { QuicksortY(dataArray, leftStart, right); }
            if (left < rightStart) { QuicksortY(dataArray, left, rightStart); }
        }
        
        public T FindNearest(float targetX, float targetY) {
            T result = values[root];
            float closestDistSqr = float.MaxValue;

            for (int current = root, depth = 0; current != -1;) {
                T currentValue = values[current];
                float curDistSqr =
                    ((targetX - currentValue.x) * (targetX - currentValue.x)) +
                    ((targetY - currentValue.y) * (targetY - currentValue.y));

                if (curDistSqr < closestDistSqr) {
                    result = currentValue;
                    closestDistSqr = curDistSqr;
                }

                if (depth % 2 == 0) //true = X    
                    current = targetX < currentValue.x ? branches[current * 2] : branches[(current * 2) + 1];
                else // false = Y
                    current = targetY < currentValue.y ? branches[current * 2] : branches[(current * 2) + 1];                
                depth++;
            }
                        
            float closestDist = Mathf.Sqrt(closestDistSqr);
            BoxIteration[] iterationStack = GenericPoolArray<BoxIteration>.Take(16);
            int iterationStackLength = 1;
            iterationStack[0].Set(root, 0);
            
            while (true) {
                BoxIteration command = iterationStack[--iterationStackLength];
                T val = values[command.node];
                int branch;

                if (val.x >= targetX - closestDist &&
                    val.x <= targetX + closestDist &&
                    val.y >= targetY - closestDist &&
                    val.y <= targetY + closestDist) {
                    float curDist =
                        ((targetX - val.x) * (targetX - val.x)) +
                        ((targetY - val.y) * (targetY - val.y));

                    if (curDist < closestDistSqr) {
                        closestDistSqr = curDist;
                        result = val;
                    }
                }

                if (iterationStackLength + 2 >= iterationStack.Length) 
                    GenericPoolArray<BoxIteration>.IncreaseSize(ref iterationStack);                

                if (command.depth % 2 == 0) {//true = X, false = Y    
                    if (targetX > val.x) {
                        if (targetX - closestDist < val.x) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only bigger branch
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                    else {
                        if (targetX + closestDist > val.x) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only smaller branch                                
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                }
                else {
                    if (targetY > val.y) {
                        if (targetY - closestDist < val.y) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only bigger branch
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                    else {
                        if (targetY + closestDist > val.y) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only smaller branch
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                }

                if (iterationStackLength == 0)
                    break;
            }

            GenericPoolArray<BoxIteration>.ReturnToPool(ref iterationStack);
            return result;
        }

        public void BoxSearch(float targetX, float targetY, float boxSize, T[] resultArray, float[] resultSqrDist, int maxResults, out int resultsCount) {    
            BoxIteration[] iterationStack = GenericPoolArray<BoxIteration>.Take(16);
            int iterationStackLength = 1;
            iterationStack[0].Set(root, 0);
            resultsCount = 0;

            while (true) {
                BoxIteration command = iterationStack[--iterationStackLength];
                T val = values[command.node];
                int branch;

                if (val.x >= targetX - boxSize &&
                    val.x <= targetX + boxSize &&
                    val.y >= targetY - boxSize &&
                    val.y <= targetY + boxSize) {
                    float curDist =
                        ((targetX - val.x) * (targetX - val.x)) +
                        ((targetY - val.y) * (targetY - val.y));

                    if (resultsCount < maxResults) {
                        resultArray[resultsCount] = val;
                        resultSqrDist[resultsCount] = curDist;
                        resultsCount++;
                    }
                    else {
                        for (int i = 0; i < resultsCount; i++) {
                            if (resultSqrDist[i] < curDist) {
                                resultSqrDist[i] = curDist;
                                resultArray[i] = val;
                                break;
                            }
                        }
                    }
                }

                if (iterationStackLength + 2 >= iterationStack.Length)
                    GenericPoolArray<BoxIteration>.IncreaseSize(ref iterationStack);

                if (command.depth % 2 == 0) {//true = X, false = Y    
                    if (targetX > val.x) {
                        if (targetX - boxSize < val.x) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only bigger branch
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                    else {
                        if (targetX + boxSize > val.x) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only smaller branch                                
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                }
                else {
                    if (targetY > val.y) {
                        if (targetY - boxSize < val.y) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only bigger branch
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                    else {
                        if (targetY + boxSize > val.y) {//include bouth branches
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                            branch = branches[(command.node * 2) + 1];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                        else {//only smaller branch
                            branch = branches[command.node * 2];
                            if (branch != INVALID)
                                iterationStack[iterationStackLength++].Set(branch, command.depth + 1);
                        }
                    }
                }

                if (iterationStackLength == 0)
                    break;
            }

            GenericPoolArray<BoxIteration>.ReturnToPool(ref iterationStack); 
        }

        #region bud stuff that for somereason can be faster
        private int BadSearch(float targetX, float targetY) {
            int current = root;
            int depth = 0;
            int result = root;
            float closest = float.MaxValue;

            while (current != -1) {
                T cur = values[current];
                float curDist = SomeMath.SqrDistance(cur.x, cur.y, targetX, targetY);
                if (curDist < closest) {
                    result = current;
                    closest = curDist;
                }

                if (depth % 2 == 0) //true = X    
                    current = targetX < cur.x ? branches[current * 2] : branches[(current * 2) + 1];
                else {// false = Y
                    current = targetY < cur.y ? branches[current * 2] : branches[(current * 2) + 1];
                }
                depth++;
            }

            return result;
        }

        private T FindNearestB(float targetX, float targetY) {
            T result = values[BadSearch(targetX, targetY)];

            int[] array = GenericPoolArray<int>.Take(16);
            int size = 0;
            BoxSearchRecursive(new Box(targetX, targetY, SomeMath.Distance(targetX, targetY, result.x, result.y)), root, 0, ref array, ref size);

            float maxDist = SomeMath.SqrDistance(result.x, result.y, targetX, targetY);
            for (int i = 0; i < size; i++) {
                T val = values[array[i]];
                float curDist = SomeMath.SqrDistance(val.x, val.y, targetX, targetY);
                if (curDist < maxDist) {
                    maxDist = curDist;
                    result = val;
                }
            }

            GenericPoolArray<int>.ReturnToPool(ref array);
            return result;
        }
        #endregion


        void BoxSearchRecursive(Box box, int node, int depth, ref int[] array, ref int arrayLength) {
            if (node == -1)
                return;

            T val = values[node];

            if (val.x >= box.x - box.size &&
                val.x <= box.x + box.size &&
                val.y >= box.y - box.size &&
                val.y <= box.y + box.size) {
                if(array.Length == arrayLength) 
                    GenericPoolArray<int>.IncreaseSize(ref array);
                array[arrayLength++] = node;
            }

            if (depth % 2 == 0) {//true = X, false = Y    
                if (box.x > val.x) {
                    if (box.x - box.size < val.x) {
                        //include bouth branches
                        BoxSearchRecursive(box, branches[node * 2], depth + 1, ref array, ref arrayLength);
                        BoxSearchRecursive(box, branches[(node * 2) + 1], depth + 1, ref array, ref arrayLength);
                    }
                    else {
                        //only bigger branch
                        BoxSearchRecursive(box, branches[(node * 2) + 1], depth + 1, ref array, ref arrayLength);
                    }
                }
                else{
                    if (box.x + box.size > val.x) {
                        //include bouth branches
                        BoxSearchRecursive(box, branches[node * 2], depth + 1, ref array, ref arrayLength);
                        BoxSearchRecursive(box, branches[(node * 2) + 1], depth + 1, ref array, ref arrayLength);
                    }
                    else {
                        //only smaller branch
                        BoxSearchRecursive(box, branches[node * 2], depth + 1, ref array, ref arrayLength);
                    }
                }
            }
            else {
                if (box.y > val.y) {
                    if (box.y - box.size < val.y) {
                        //include bouth branches
                        BoxSearchRecursive(box, branches[node * 2], depth + 1, ref array, ref arrayLength);
                        BoxSearchRecursive(box, branches[(node * 2) + 1], depth + 1, ref array, ref arrayLength);
                    }
                    else {
                        //only bigger branch
                        BoxSearchRecursive(box, branches[(node * 2) + 1], depth + 1, ref array, ref arrayLength);
                    }
                }
                else {
                    if (box.y + box.size > val.y) {
                        //include bouth branches
                        BoxSearchRecursive(box, branches[node * 2], depth + 1, ref array, ref arrayLength);
                        BoxSearchRecursive(box, branches[(node * 2) + 1], depth + 1, ref array, ref arrayLength);
                    }
                    else {
                        //only smaller branch
                        BoxSearchRecursive(box, branches[node * 2], depth + 1, ref array, ref arrayLength);
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void DebugMe() {
            DebugRecursive(root, 0);
        }

        void DebugRecursive(int index, int depth) {
            T val = values[index];

            int branchLow = branches[index * 2];
            int branchHigh = branches[(index * 2) + 1];            

            Vector3 p1 = new Vector3(val.x, 0, val.y);
            Debuger_K.AddLabel(p1, depth);

            if (branchLow != -1) {
                T memderLow = values[branchLow];
                Vector3 p2_low = new Vector3(memderLow.x, 0, memderLow.y);
                Debuger_K.AddLine(p1, p2_low, Color.blue);
                Debuger_K.AddDot(p2_low, Color.green, 0.05f);
                DebugRecursive(branchLow, depth + 1);
            }

            if (branchHigh != -1) {
                T memderHigh = values[branchHigh];
                Vector3 p2_high = new Vector3(memderHigh.x, 0, memderHigh.y);
                Debuger_K.AddLine(p1, p2_high, Color.blue);
                Debuger_K.AddDot(p2_high, Color.green, 0.05f);
                DebugRecursive(branchHigh, depth + 1);
            }
        }
#endif
    }

    public class Generic2dKDTreeWithBounds<T> where T : struct, IKDTreeMember {
        private int membersPerBranch;
        private int root;
        private T[] values;
        private kDTreeBoundsBranch[] boundBranches;
        private int branchesLength = 0;
        const int INVALID_VALUE = -1;
        
        private struct kDTreeBoundsBranch {
            public int start, end, branchLow, branchHigh;
            public float minX, minZ, maxX, maxZ;
            public SplitAxis splitAxis;

            public kDTreeBoundsBranch(int start, int end, int branchLow, int branchHigh, float minX, float minZ, float maxX, float maxZ, SplitAxis splitAxis) {
                this.start = start;
                this.end = end;
                this.branchLow = branchLow;
                this.branchHigh = branchHigh;
                this.minX = minX;
                this.minZ = minZ;
                this.maxX = maxX;
                this.maxZ = maxZ;
                this.splitAxis = splitAxis;
            }

#if UNITY_EDITOR
            public void Draw(Color color, float height) {
                Vector3 v1 = new Vector3(minX, height, minZ);
                Vector3 v2 = new Vector3(minX, height, maxZ);
                Vector3 v3 = new Vector3(maxX, height, maxZ);
                Vector3 v4 = new Vector3(maxX, height, minZ);
                Debuger_K.AddLine(color, 0f, 0.001f, true, v1, v2, v3, v4);
            }
#endif

            public Vector3 center {
                get { return new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2); }
            }

            public override string ToString() {
                return string.Format("BL {0}, BH {1}, S{2}, E{3}, minX{4}, minZ{5}, maxX{6}, maxZ{7}", branchLow, branchHigh, start, end, minX, minZ, maxX, maxZ);
            }
        }

        private enum SplitAxis : int {
            X = 0,
            Z = 1,
            END = 2
        }


        public void BuildTree(List<T> data, int membersPerBranch = 10) {
            if (membersPerBranch < 1)
                membersPerBranch = 1;

            int count = data.Count;

            if (boundBranches != null)
                GenericPoolArray<kDTreeBoundsBranch>.ReturnToPool(ref boundBranches);
            boundBranches = GenericPoolArray<kDTreeBoundsBranch>.Take(32);
            branchesLength = 0;

            if (values != null)
                GenericPoolArray<T>.ReturnToPool(ref values);
            values = GenericPoolArray<T>.Take(count);
            
            float minX, minY, maxX, maxY;
            T first = data[0];
            minX = first.x;
            minY = first.y;
            maxX = first.x;
            maxY = first.y;

            for (int i = 0; i < count; i++) {
                T cur = data[i];
                values[i] = cur;
                minX = Mathf.Min(minX, cur.x);
                minY = Mathf.Min(minY, cur.y);
                maxX = Mathf.Max(maxX, cur.x);
                maxY = Mathf.Max(maxY, cur.y);
            }
            
            root = BuildRecursive(0, count - 1, membersPerBranch);
        }

        //return index of branch
        private int BuildRecursive(int leftStart, int rightStart, int membersPerBranch) {
            int count = rightStart - leftStart;

            float minX, minY, maxX, maxY;
            T root = values[leftStart];
            minX = maxX = root.x;
            minY = maxY = root.y;

            for (int i = leftStart + 1; i < rightStart; i++) {
                root = values[i];
                if (root.x < minX) minX = root.x;
                if (root.y < minY) minY = root.y;
                if (root.x > maxX) maxX = root.x;
                if (root.y > maxY) maxY = root.y;
            }

            if (count <= membersPerBranch) {
                if (boundBranches.Length == branchesLength)
                    GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref boundBranches);
                boundBranches[branchesLength++] = new kDTreeBoundsBranch(leftStart, rightStart, INVALID_VALUE, INVALID_VALUE, minX, minY, maxX, maxY, SplitAxis.END);
                return branchesLength - 1;
            }
            else {
                int left = leftStart;
                int right = rightStart;
                float pivot = 0f;
                SplitAxis axis;

                if ((maxX - minX) > (maxY - minY)) {//deside how to split. if size X > size Y then X            
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += values[i].x;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (values[left].x < pivot) left++;
                        while (values[right].x > pivot) right--;
                        if (left <= right) {
                            T tempData = values[left];
                            values[left++] = values[right];
                            values[right--] = tempData;
                        }
                    }
                    axis = SplitAxis.X;
                }
                else {//y
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += values[i].y;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (values[left].y < pivot) left++;
                        while (values[right].y > pivot) right--;
                        if (left <= right) {
                            T tempData = values[left];
                            values[left++] = values[right];
                            values[right--] = tempData;
                        }
                    }
                    axis = SplitAxis.Z;
                }

                int L = BuildRecursive(leftStart, right, membersPerBranch);
                int H = BuildRecursive(left, rightStart, membersPerBranch);
                if (boundBranches.Length == branchesLength)
                    GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref boundBranches);
                boundBranches[branchesLength++] = new kDTreeBoundsBranch(leftStart, rightStart, L, H, minX, minY, maxX, maxY, axis);
                return branchesLength - 1;
            }
        }

        private T FindNearest(float targetX, float targetZ) {
            T result = values[root];
            float closestDistSqr = float.MaxValue;
            kDTreeBoundsBranch branch = boundBranches[root];

            while (branch.splitAxis != SplitAxis.END) {
                if (branch.splitAxis == SplitAxis.X) {
                    kDTreeBoundsBranch curBranchLow = boundBranches[branch.branchLow];
                    kDTreeBoundsBranch curBranchHigh = boundBranches[branch.branchHigh];
                    branch = (curBranchLow.maxX - targetX) * -1 < curBranchHigh.minX - targetX ? curBranchLow : curBranchHigh;
                }
                else {
                    kDTreeBoundsBranch curBranchLow = boundBranches[branch.branchLow];
                    kDTreeBoundsBranch curBranchHigh = boundBranches[branch.branchHigh];
                    branch = (curBranchLow.maxZ - targetZ) * -1 < curBranchHigh.minZ - targetZ ? curBranchLow : curBranchHigh;
                }
            }
            for (int i = branch.start; i < branch.end; i++) {
                T val = values[i];
                float curDistSqr = SomeMath.SqrDistance(val.x, val.y, targetX, targetZ);
                if (curDistSqr < closestDistSqr) {
                    closestDistSqr = curDistSqr;
                    result = val;
                }
            }

            float closestDist = Mathf.Sqrt(closestDistSqr);
            int[] iterationStack = GenericPoolArray<int>.Take(16);
            int iterationStackLength = 1;
            iterationStack[0] = root;

            while (iterationStackLength != 0) {
                branch = boundBranches[iterationStack[--iterationStackLength]];
                if (branch.minX <= targetX + closestDist &&
                    branch.maxX >= targetX - closestDist &&
                    branch.minZ <= targetZ + closestDist &&
                    branch.maxZ >= targetZ - closestDist) {


                    if (branch.splitAxis == SplitAxis.END) {
                        //Debuger_K.AddLine(new Vector3(targetX, 0, targetZ), branch.center, Color.yellow);
                        for (int i = branch.start; i < branch.end; i++) {
                            T val = values[i];
                            float curDistSqr = SomeMath.SqrDistance(val.x, val.y, targetX, targetZ);
                            if (curDistSqr < closestDistSqr) {
                                closestDistSqr = curDistSqr;
                                result = val;
                            }
                        }
                    }
                    else {
                        if (iterationStack.Length <= iterationStackLength + 2)
                            GenericPoolArray<int>.IncreaseSize(ref iterationStack);
                        iterationStack[iterationStackLength++] = branch.branchHigh;
                        iterationStack[iterationStackLength++] = branch.branchLow;
                    }
                }
            }
            return result;
        }
        
        private static void QuicksortX(T[] values, int leftStart, int rightStart) {
            int left = leftStart;
            int right = rightStart;
            float min, max, pivotFloat;

            min = max = values[leftStart].x;
            for (int i = leftStart + 1; i < rightStart; i++) {
                T val = values[i];
                if (val.x < min) min = val.x;
                if (val.x > max) max = val.x;
            }

            pivotFloat = (min + max) * 0.5f;

            while (left <= right) {
                while (values[left].x < pivotFloat) left++;
                while (values[right].x > pivotFloat) right--;

                if (left <= right) {
                    T tempData = values[left];
                    values[left++] = values[right];
                    values[right--] = tempData;
                }
            }

            if (leftStart < right) QuicksortX(values, leftStart, right);
            if (left < rightStart) QuicksortX(values, left, rightStart);
        }

        private static void QuicksortY(T[] values, int leftStart, int rightStart) {
            int left = leftStart;
            int right = rightStart;
            float min, max, pivotFloat;

            min = max = values[leftStart].y;

            for (int i = leftStart + 1; i < rightStart; i++) {
                T val = values[i];
                if (val.y < min) min = val.y;
                if (val.y > max) max = val.y;
            }

            pivotFloat = (min + max) * 0.5f;

            while (left <= right) {
                while (values[left].y < pivotFloat) left++;
                while (values[right].y > pivotFloat) right--;

                if (left <= right) {
                    T tempData = values[left];
                    values[left++] = values[right];
                    values[right--] = tempData;
                }
            }

            if (leftStart < right) QuicksortY(values, leftStart, right);
            if (left < rightStart) QuicksortY(values, left, rightStart);
        }

        public void DebugBoundsTree() {
            DebugRecursive(root, 0);
        }

        void DebugRecursive(int target, int depth) {
            //float height = depth * 5;
            //var branch = boundBranches[target];

            //branch.Draw(new Color(rndDebug.Next(0, 1000) / 1000f, rndDebug.Next(0, 1000) / 1000f, rndDebug.Next(0, 1000) / 1000f, 1f), height);

            //if (branch.splitAxis == SplitAxis.END) {
            //    Vector3 center = branch.center;
            //    center = new Vector3(center.x, height, center.z);
            //    Debuger_K.AddLabel(center, branch.end - branch.start);
            //    for (int i = branch.start; i < branch.end; i++) {
            //        T memder = values[i];
            //        Debuger_K.AddLine(center, new Vector3(memder.x, height, memder.z), Color.blue);
            //        Debuger_K.AddDot(new Vector3(memder.x, height, memder.z), Color.green, 0.05f);
            //    }
            //}
            //else {
            //    DebugRecursive(branch.branchLow, depth + 1);
            //    DebugRecursive(branch.branchHigh, depth + 1);
            //}
        }
    }

    public class Generic2dBoundsTree<T> where T : struct, IKDTreeMember { 
        int rootBoundsTreeRoot;//index of first node
        ValueHolder[] rootInfoArray;
        int rootInfoArrayLength;
        kDTreeBoundsBranch[] rootInfoBoundBranches;
        int boundBranchesLength = 0;

        enum TreeSplitAxis : int {
            X = 0,
            Y = 1,
            END = 2
        }
        struct ValueHolder {
            public T val;
            public float x, y;

            public ValueHolder(T Val, float X, float Z) {
                val = Val;
                x = X;
                y = Z;
            }
        }
        struct kDTreeBoundsBranch {
            public int start, end, branchLow, branchHigh;
            public float minX, minY, maxX, maxY;
            public TreeSplitAxis splitAxis;

            public kDTreeBoundsBranch(int start, int end, int branchLow, int branchHigh, float minX, float minY, float maxX, float maxY, TreeSplitAxis splitAxis) {
                this.start = start;
                this.end = end;
                this.branchLow = branchLow;
                this.branchHigh = branchHigh;
                this.minX = minX;
                this.minY = minY;
                this.maxX = maxX;
                this.maxY = maxY;
                this.splitAxis = splitAxis;
            }

            public Vector3 center {
                get { return new Vector3((minX + maxX) / 2, 0, (minY + maxY) / 2); }
            }

            public override string ToString() {
                return string.Format("BL {0}, BH {1}, S{2}, E{3}, minX{4}, minZ{5}, maxX{6}, maxZ{7}", branchLow, branchHigh, start, end, minX, minY, maxX, maxY);
            }
        }

        public void BuildTree(List<T> values, int membersPerBranch) {
            rootInfoArrayLength = values.Count;

            if (rootInfoArray == null)
                rootInfoArray = GenericPoolArray<ValueHolder>.Take(rootInfoArrayLength);
            else if (rootInfoArray.Length <= rootInfoArrayLength) {
                GenericPoolArray<ValueHolder>.ReturnToPool(ref rootInfoArray);
                rootInfoArray = GenericPoolArray<ValueHolder>.Take(rootInfoArrayLength);
            }         

            for (int i = 0; i < rootInfoArrayLength; i++) {
                T val = values[i];
                rootInfoArray[i] = new ValueHolder(val, val.x, val.y);
            }

            BuildRootInfoBoundsTree(membersPerBranch);
        }

        public void BuildTree(T[] values, int valuesCount, int membersPerBranch) {
            rootInfoArrayLength = valuesCount;

            if (rootInfoArray == null)
                rootInfoArray = GenericPoolArray<ValueHolder>.Take(rootInfoArrayLength);
            else if (rootInfoArray.Length <= rootInfoArrayLength) {
                GenericPoolArray<ValueHolder>.ReturnToPool(ref rootInfoArray);
                rootInfoArray = GenericPoolArray<ValueHolder>.Take(rootInfoArrayLength);
            }

            for (int i = 0; i < rootInfoArrayLength; i++) {
                T val = values[i];
                rootInfoArray[i] = new ValueHolder(val, val.x, val.y);
            }

            BuildRootInfoBoundsTree(membersPerBranch);
        }

        void BuildRootInfoBoundsTree(int membersPerBranch) {
            if (rootInfoBoundBranches != null)
                GenericPoolArray<kDTreeBoundsBranch>.ReturnToPool(ref rootInfoBoundBranches);
            rootInfoBoundBranches = GenericPoolArray<kDTreeBoundsBranch>.Take(32);
            boundBranchesLength = 0;
            rootBoundsTreeRoot = BuildRecursiveFootInfoBoundsTree(0, rootInfoArrayLength - 1, membersPerBranch);
        }

        int BuildRecursiveFootInfoBoundsTree(int leftStart, int rightStart, int membersPerBranch) {
            int count = rightStart - leftStart;

            float minX, minY, maxX, maxY;
            ValueHolder root = rootInfoArray[leftStart];
            minX = maxX = root.x;
            minY = maxY = root.y;

            for (int i = leftStart + 1; i <= rightStart; i++) {
                root = rootInfoArray[i];
                if (root.x < minX) minX = root.x;
                if (root.y < minY) minY = root.y;
                if (root.x > maxX) maxX = root.x;
                if (root.y > maxY) maxY = root.y;
            }

            if (count <= membersPerBranch) {
                if (rootInfoBoundBranches.Length == boundBranchesLength)
                    GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref rootInfoBoundBranches);
                rootInfoBoundBranches[boundBranchesLength++] = new kDTreeBoundsBranch(leftStart, rightStart, -1, -1, minX, minY, maxX, maxY, TreeSplitAxis.END);
                return boundBranchesLength - 1;
            }
            else {
                int left = leftStart;
                int right = rightStart;
                float pivot = 0f;
                TreeSplitAxis axis;

                if ((maxX - minX) > (maxY - minY)) {//deside how to split. if size X > size Y then X            
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += rootInfoArray[i].x;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (rootInfoArray[left].x < pivot) left++;
                        while (rootInfoArray[right].x > pivot) right--;
                        if (left <= right) {
                            ValueHolder tempData = rootInfoArray[left];
                            rootInfoArray[left++] = rootInfoArray[right];
                            rootInfoArray[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.X;
                }
                else {//y
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += rootInfoArray[i].y;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (rootInfoArray[left].y < pivot) left++;
                        while (rootInfoArray[right].y > pivot) right--;
                        if (left <= right) {
                            ValueHolder tempData = rootInfoArray[left];
                            rootInfoArray[left++] = rootInfoArray[right];
                            rootInfoArray[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.Y;
                }

                int L = BuildRecursiveFootInfoBoundsTree(leftStart, right, membersPerBranch);
                int H = BuildRecursiveFootInfoBoundsTree(left, rightStart, membersPerBranch);
                if (rootInfoBoundBranches.Length == boundBranchesLength)
                    GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref rootInfoBoundBranches);
                rootInfoBoundBranches[boundBranchesLength++] = new kDTreeBoundsBranch(leftStart, rightStart, L, H, minX, minY, maxX, maxY, axis);
                return boundBranchesLength - 1;
            }
        }

        ValueHolder GetNearestBoundsTree(float targetX, float targetZ, ref int[] iterationStack) {
            ValueHolder result = rootInfoArray[rootBoundsTreeRoot];
            float closestDistSqr = float.MaxValue;
            kDTreeBoundsBranch branch = rootInfoBoundBranches[rootBoundsTreeRoot];

            while (branch.splitAxis != TreeSplitAxis.END) {
                if (branch.splitAxis == TreeSplitAxis.X) {
                    kDTreeBoundsBranch curBranchLow = rootInfoBoundBranches[branch.branchLow];
                    kDTreeBoundsBranch curBranchHigh = rootInfoBoundBranches[branch.branchHigh];
                    branch = (curBranchLow.maxX - targetX) * -1 < curBranchHigh.minX - targetX ? curBranchLow : curBranchHigh;
                }
                else {
                    kDTreeBoundsBranch curBranchLow = rootInfoBoundBranches[branch.branchLow];
                    kDTreeBoundsBranch curBranchHigh = rootInfoBoundBranches[branch.branchHigh];
                    branch = (curBranchLow.maxY - targetZ) * -1 < curBranchHigh.minY - targetZ ? curBranchLow : curBranchHigh;
                }
            }
            for (int i = branch.start; i < branch.end; i++) {
                ValueHolder val = rootInfoArray[i];
                float curDistSqr = SomeMath.SqrDistance(val.x, val.y, targetX, targetZ);
                if (curDistSqr < closestDistSqr) {
                    closestDistSqr = curDistSqr;
                    result = val;
                }
            }

            float closestDist = Mathf.Sqrt(closestDistSqr);

            int iterationStackLength = 1;
            iterationStack[0] = rootBoundsTreeRoot;

            while (iterationStackLength != 0) {
                branch = rootInfoBoundBranches[iterationStack[--iterationStackLength]];
                if (branch.minX <= targetX + closestDist &&
                    branch.maxX >= targetX - closestDist &&
                    branch.minY <= targetZ + closestDist &&
                    branch.maxY >= targetZ - closestDist) {

                    if (branch.splitAxis == TreeSplitAxis.END) {
                        for (int i = branch.start; i < branch.end; i++) {
                            ValueHolder val = rootInfoArray[i];
                            float curDistSqr = SomeMath.SqrDistance(val.x, val.y, targetX, targetZ);
                            if (curDistSqr < closestDistSqr) {
                                closestDistSqr = curDistSqr;
                                result = val;
                            }
                        }
                    }
                    else {
                        if (iterationStack.Length <= iterationStackLength + 2)
                            GenericPoolArray<int>.IncreaseSize(ref iterationStack);
                        iterationStack[iterationStackLength++] = branch.branchHigh;
                        iterationStack[iterationStackLength++] = branch.branchLow;
                    }
                }
            }
            return result;
        }

        public void Get(float minX, float maxX, float minY, float maxY, ref T[] result, out int resultLength, ref int[] iterationStack) {
            resultLength = 0;
            int iterationStackLength = 1;
            iterationStack[0] = rootBoundsTreeRoot;
            kDTreeBoundsBranch branch;

            while (iterationStackLength != 0) {
                branch = rootInfoBoundBranches[iterationStack[--iterationStackLength]];
                if (branch.minX <= maxX && branch.maxX >= minX &&
                    branch.minY <= maxY && branch.maxY >= minY) {
                    
                    if (branch.splitAxis == TreeSplitAxis.END) {
                        for (int i = branch.start; i < branch.end + 1; i++) {
                            ValueHolder val = rootInfoArray[i];
                            if (val.x >= minX && val.x <= maxX && val.y >= minY && val.y <= maxY) {
                                if (result.Length == resultLength)
                                    GenericPoolArray<T>.IncreaseSize(ref result);
                                result[resultLength++] = val.val;
                            }
                        }
                    }
                    else {
                        if (iterationStack.Length <= iterationStackLength + 2)
                            GenericPoolArray<int>.IncreaseSize(ref iterationStack);
                        iterationStack[iterationStackLength++] = branch.branchHigh;
                        iterationStack[iterationStackLength++] = branch.branchLow;
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void DebugBoundsTree(DebugSet set) {
            DebugRecursive(rootBoundsTreeRoot, 0, set);
        }

        private void DebugRecursive(int target, int depth, DebugSet set) {
            float height = depth;
            //float height = 0;
            if (rootInfoBoundBranches == null)
                return;

            var branch = rootInfoBoundBranches[target];

            System.Random rand = new System.Random();

            Color color = new Color(
                rand.Next(0, 255) / 255f,
                rand.Next(0, 255) / 255f,
                rand.Next(0, 255) / 255f, 1f);

            Vector3 v1 = new Vector3(branch.minX, height, branch.minY);
            Vector3 v2 = new Vector3(branch.minX, height, branch.maxY);
            Vector3 v3 = new Vector3(branch.maxX, height, branch.maxY);
            Vector3 v4 = new Vector3(branch.maxX, height, branch.minY);
            set.AddLine(v1, v2, color);
            set.AddLine(v2, v3, color);
            set.AddLine(v3, v4, color);
            set.AddLine(v4, v1, color);

            if (branch.splitAxis == TreeSplitAxis.END) {
                Vector3 center = branch.center;
                center = new Vector3(center.x, height, center.z);
                set.AddLabel(center, branch.end - branch.start + 1);
                for (int i = branch.start; i <= branch.end; i++) {
                    ValueHolder memder = rootInfoArray[i];
                    set.AddLine(center, new Vector3(memder.x, height, memder.y), Color.blue, 0.0025f);
                    set.AddDot(new Vector3(memder.x, height, memder.y), Color.green, 0.1f);
                }
            }
            else {
                DebugRecursive(branch.branchLow, depth + 1, set);
                DebugRecursive(branch.branchHigh, depth + 1, set);
            }
        }
#endif

    }

    public class Generic2dBoundsTreePureIndexes<T> where T : IKDTreeMember {
        ValueHolder[] sortableData;
        int sortableDataLength;
        kDTreeBoundsBranch[] tree;
        int treeCount = 0;
        int treeRoot;

        enum TreeSplitAxis : int {
            X = 0,
            Y = 1,
            END = 2
        }
        struct ValueHolder {
            public int index;
            public float x, y;
        }

        struct kDTreeBoundsBranch {
            public int start, end, branchLow, branchHigh;
            public float minX, minY, maxX, maxY;
            public TreeSplitAxis splitAxis;

            public kDTreeBoundsBranch(int start, int end, int branchLow, int branchHigh, float minX, float minY, float maxX, float maxY, TreeSplitAxis splitAxis) {
                this.start = start;
                this.end = end;
                this.branchLow = branchLow;
                this.branchHigh = branchHigh;
                this.minX = minX;
                this.minY = minY;
                this.maxX = maxX;
                this.maxY = maxY;
                this.splitAxis = splitAxis;
            }

            public Vector3 center {
                get { return new Vector3((minX + maxX) / 2, 0, (minY + maxY) / 2); }
            }

            public override string ToString() {
                return string.Format("BL {0}, BH {1}, S{2}, E{3}, minX{4}, minZ{5}, maxX{6}, maxZ{7}", branchLow, branchHigh, start, end, minX, minY, maxX, maxY);
            }
        }

        public void BuildTree(List<T> values, int membersPerBranch) {
            sortableDataLength = values.Count;

            if (sortableData == null)
                sortableData = GenericPoolArray<ValueHolder>.Take(sortableDataLength);
            else if (sortableData.Length <= sortableDataLength) {
                GenericPoolArray<ValueHolder>.ReturnToPool(ref sortableData);
                sortableData = GenericPoolArray<ValueHolder>.Take(sortableDataLength);
            }

            for (int i = 0; i < sortableDataLength; i++) {
                T val = values[i];
                sortableData[i].index = i;
                sortableData[i].x = val.x;
                sortableData[i].y = val.y;
            }

            BuildRootInfoBoundsTree(membersPerBranch);
        }

        public void BuildTree(T[] values, int valuesCount, int membersPerBranch) {
            sortableDataLength = valuesCount;

            if (sortableData == null)
                sortableData = GenericPoolArray<ValueHolder>.Take(sortableDataLength);
            else if (sortableData.Length <= sortableDataLength) {
                GenericPoolArray<ValueHolder>.ReturnToPool(ref sortableData);
                sortableData = GenericPoolArray<ValueHolder>.Take(sortableDataLength);
            }

            for (int i = 0; i < sortableDataLength; i++) {
                T val = values[i];
                sortableData[i].index = i;
                sortableData[i].x = val.x;
                sortableData[i].y = val.y;
            }

            BuildRootInfoBoundsTree(membersPerBranch);
        }

        void BuildRootInfoBoundsTree(int membersPerBranch) {
            if (tree != null)
                GenericPoolArray<kDTreeBoundsBranch>.ReturnToPool(ref tree);
            tree = GenericPoolArray<kDTreeBoundsBranch>.Take(32);
            treeCount = 0;
            treeRoot = BuildRecursiveFootInfoBoundsTree(0, sortableDataLength - 1, membersPerBranch);    
        }

        int BuildRecursiveFootInfoBoundsTree(int leftStart, int rightStart, int membersPerBranch) {
            int count = rightStart - leftStart;
            float minX, minY, maxX, maxY;

            minX = maxX = sortableData[leftStart].x;
            minY = maxY = sortableData[leftStart].y;

            for (int i = leftStart + 1; i <= rightStart; i++) {  
                if (sortableData[i].x < minX) minX = sortableData[i].x;
                if (sortableData[i].y < minY) minY = sortableData[i].y;
                if (sortableData[i].x > maxX) maxX = sortableData[i].x;
                if (sortableData[i].y > maxY) maxY = sortableData[i].y;
            }

            int L = -1;
            int H = -1;
            TreeSplitAxis axis = TreeSplitAxis.END;

            if (count > membersPerBranch) {
                int left = leftStart;
                int right = rightStart;
                float pivot = 0f;

                if ((maxX - minX) > (maxY - minY)) {//deside how to split. if size X > size Y then X            
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += sortableData[i].x;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (sortableData[left].x < pivot) left++;
                        while (sortableData[right].x > pivot) right--;
                        if (left <= right) {
                            ValueHolder tempData = sortableData[left];
                            sortableData[left++] = sortableData[right];
                            sortableData[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.X;
                }
                else {//y
                    for (int i = leftStart; i < rightStart; i++) {
                        pivot += sortableData[i].y;
                    }
                    pivot /= count;

                    while (left <= right) {
                        while (sortableData[left].y < pivot) left++;
                        while (sortableData[right].y > pivot) right--;
                        if (left <= right) {
                            ValueHolder tempData = sortableData[left];
                            sortableData[left++] = sortableData[right];
                            sortableData[right--] = tempData;
                        }
                    }
                    axis = TreeSplitAxis.Y;
                }

                L = BuildRecursiveFootInfoBoundsTree(leftStart, right, membersPerBranch);
                H = BuildRecursiveFootInfoBoundsTree(left, rightStart, membersPerBranch);
            }

            if (tree.Length == treeCount)
                GenericPoolArray<kDTreeBoundsBranch>.IncreaseSize(ref tree);

            tree[treeCount] = new kDTreeBoundsBranch(leftStart, rightStart, L, H, minX, minY, maxX, maxY, axis);
            return treeCount++;
        }

        public void Get(float minX, float maxX, float minY, float maxY, ref int[] result, out int resultCount, ref int[] iterationStack) {
            resultCount = 0;
            int iterationStackLength = 1;
            iterationStack[0] = treeRoot;   

            while (iterationStackLength != 0) {
                kDTreeBoundsBranch branch = tree[iterationStack[--iterationStackLength]];
                if (branch.minX <= maxX && branch.maxX >= minX &&
                    branch.minY <= maxY && branch.maxY >= minY) {

                    if (branch.splitAxis == TreeSplitAxis.END) {
                        for (int i = branch.start; i < branch.end + 1; i++) {
                            ValueHolder val = sortableData[i];
                            if (val.x >= minX && val.x <= maxX && val.y >= minY && val.y <= maxY) {
                                if (result.Length == resultCount)
                                    GenericPoolArray<int>.IncreaseSize(ref result);
                                result[resultCount++] = val.index;
                            }
                        }
                    }
                    else {
                        if (iterationStack.Length <= iterationStackLength + 2)
                            GenericPoolArray<int>.IncreaseSize(ref iterationStack);
                        iterationStack[iterationStackLength++] = branch.branchHigh;
                        iterationStack[iterationStackLength++] = branch.branchLow;
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void DebugBoundsTree(DebugSet set) {
            DebugRecursive(treeRoot, 0, set);
        }

        private void DebugRecursive(int target, int depth, DebugSet set) {
            float height = depth;
            //float height = 0;
            if (tree == null)
                return;

            var branch = tree[target];

            System.Random rand = new System.Random();

            Color color = new Color(
                rand.Next(0, 255) / 255f,
                rand.Next(0, 255) / 255f,
                rand.Next(0, 255) / 255f, 1f);

            Vector3 v1 = new Vector3(branch.minX, height, branch.minY);
            Vector3 v2 = new Vector3(branch.minX, height, branch.maxY);
            Vector3 v3 = new Vector3(branch.maxX, height, branch.maxY);
            Vector3 v4 = new Vector3(branch.maxX, height, branch.minY);
            set.AddLine(v1, v2, color);
            set.AddLine(v2, v3, color);
            set.AddLine(v3, v4, color);
            set.AddLine(v4, v1, color);

            if (branch.splitAxis == TreeSplitAxis.END) {
                Vector3 center = branch.center;
                center = new Vector3(center.x, height, center.z);
                set.AddLabel(center, branch.end - branch.start + 1);
                for (int i = branch.start; i <= branch.end; i++) {
                    ValueHolder memder = sortableData[i];
                    set.AddLine(center, new Vector3(memder.x, height, memder.y), Color.blue, 0.0025f);
                    set.AddDot(new Vector3(memder.x, height, memder.y), Color.green, 0.1f);
                }
            }
            else {
                DebugRecursive(branch.branchLow, depth + 1, set);
                DebugRecursive(branch.branchHigh, depth + 1, set);
            }
        }
#endif

    }
}
