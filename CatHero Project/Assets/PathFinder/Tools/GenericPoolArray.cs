using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using K_PathFinder.GraphGeneration;
using UnityEngine;


namespace K_PathFinder.Pool {
    public static class GenericPoolArray<T> {

        static Stack<T[]>[] pool;
        static bool isReferenceType;

        static GenericPoolArray(){
            isReferenceType = typeof(T).IsValueType == false;
            //awww yeeeah
            pool = new Stack<T[]>[]{
                new Stack<T[]>(),//0
                new Stack<T[]>(),//1
                new Stack<T[]>(),//2
                new Stack<T[]>(),//3
                new Stack<T[]>(),//4
                new Stack<T[]>(),//5
                new Stack<T[]>(),//6
                new Stack<T[]>(),//7
                new Stack<T[]>(),//8
                new Stack<T[]>(),//9
                new Stack<T[]>(),//10
                new Stack<T[]>(),//11
                new Stack<T[]>(),//12
                new Stack<T[]>(),//13
                new Stack<T[]>(),//14
                new Stack<T[]>(),//15
                new Stack<T[]>(),//16
                new Stack<T[]>(),//17
                new Stack<T[]>(),//18
                new Stack<T[]>(),//19
                new Stack<T[]>(),//10
                new Stack<T[]>(),//21
                new Stack<T[]>(),//22
                new Stack<T[]>(),//23
                new Stack<T[]>(),//24
                new Stack<T[]>(),//25
                new Stack<T[]>(),//26
                new Stack<T[]>(),//27
                new Stack<T[]>(),//28
                new Stack<T[]>(),//29
                new Stack<T[]>(),//30
                new Stack<T[]>(),//31
                new Stack<T[]>()//32       
            };
        }

        public static int GetClosestNumberToNumericProgression(int value) {
            int curNumber = 1;
            for (int i = 1; i < 31; i++) {
                int dif = curNumber - value;
                if (dif > 0)
                    return curNumber;

                curNumber = curNumber + curNumber;
            }
            throw new Exception("GetClosestNumberToNumericProgression somehow failed. how?");
        }

        //RETURN ONLY CLAMPED TO POWER OF 2 SIZES
        public static T[] Take(int size, bool makeDefault = false) {
            int pow;
            int targetArraySize = 1;
            for (pow = 0; pow < 32; pow++) {
                targetArraySize = 1 << pow;
                if (targetArraySize >= size)
                    break;
            }

            T[] result = null;
            Stack<T[]> stack = pool[pow];

            lock (stack) {
                if(stack.Count > 0) {
                    result = stack.Pop();
#if UNITY_EDITOR
                    //PFDebuger.Debuger_K.ObjectPoolArrayRegisterEvent(typeof(T), result.Length, 1, stack.Count, 0);
#endif
                }
                else {
                    result = new T[targetArraySize];
#if UNITY_EDITOR
                    //PFDebuger.Debuger_K.ObjectPoolArrayRegisterEvent(typeof(T), result.Length, 0, stack.Count, 1);
#endif
                }
            }

            if (makeDefault) {
                T d = default(T);
                for (int i = 0; i < size; i++) {
                    result[i] = d;
                }
            }

            //Debug.LogFormat("taken type {0} size {1}", typeof(T).Name, size);
            return result;
        }

        //RETURN ONLY CLAMPED TO POWER OF 2 SIZES WITH COPY OF TARGET ARRAY
        public static T[] Take(T[] makeCopyOfThat) {
            int size = makeCopyOfThat.Length;

            T[] result = Take(size);

            for (int i = 0; i < makeCopyOfThat.Length; i++) {
                result[i] = makeCopyOfThat[i];
            }

            return result;
        }
        public static T[] Take(int size, T defaultValue) {
            T[] result = Take(size);
            
            for (int i = 0; i < size; i++) {
                result[i] = defaultValue;
            }

            //Debug.LogFormat("taken type {0} size {1}", typeof(T).Name, size);
            return result;
        }

        public static void ReturnToPool(ref T[] value) {
            if (value != null) {                
                if (isReferenceType) 
                    Array.Clear(value, 0, value.Length);

                if (value.Length == 0)
                    throw new Exception("you trying to return to pool array of 0 size. something is horrible wrong here");

                int arrayLength = value.Length;
                if ((arrayLength & (arrayLength - 1)) != 0)
                    throw new Exception("you trying to return to pool array that have size not in power of 2");
                
                
                int pow;
                for (pow = 0; pow < 32; pow++) {
                    if (1 << pow == arrayLength)
                        break;
                }

                //Debug.Log("returned size " + value.Length + " pow " + pow + " : " + (1 << pow));
                Stack<T[]> stack = pool[pow];

                lock (stack) {
                    stack.Push(value);
#if UNITY_EDITOR
                    //PFDebuger.Debuger_K.ObjectPoolArrayRegisterEvent(typeof(T), value.Length, -1, stack.Count, 0);
#endif
                }
                value = null;      
            }
        }
        
        ///take old array, take double size of this array, copy data from old array to new, array, set new array to old array, return old array to pool        
        public static void IncreaseSize(ref T[] array) {
            T[] newArray = Take(array.Length * 2);
            Array.Copy(array, newArray, array.Length);
            ReturnToPool(ref array);
            array = newArray;
        }

        ///take old array, take double size of this array, copy data from old array to new, array, set new array to old array, return old array to pool, set new values to target value  
        public static void IncreaseSize(ref T[] array, T defaultValueAfterCopy) {
            T[] newArray = Take(array.Length * 2);
            Array.Copy(array, newArray, array.Length);
        
            for (int i = array.Length; i < newArray.Length; i++) {
                newArray[i] = defaultValueAfterCopy;
            }

            ReturnToPool(ref array);
            array = newArray;
        }
        
        ///take old array, take double size of this array, copy data from old array to new, array, set new array to old array, return old array to pool        
        public static void IncreaseSizeTo(ref T[] array, int minLength) {
            if (array.Length > minLength)
                minLength = array.Length;

            T[] newArray = Take(minLength);
            Array.Copy(array, newArray, array.Length);
            ReturnToPool(ref array);
            array = newArray;
        }

        ///take old array, take double size of this array, copy data from old array to new, array, set new array to old array, return old array to pool        
        public static void IncreaseSizeTo(ref T[] array, int minLength, T defaultValueAfterCopy) {
            if (array.Length > minLength)
                minLength = array.Length;

            T[] newArray = Take(minLength);

            Array.Copy(array, newArray, array.Length);

            for (int i = array.Length; i < newArray.Length; i++) {
                newArray[i] = defaultValueAfterCopy;
            }

            ReturnToPool(ref array);
            array = newArray;
        }

        //public static void DebugState() {
        //    StringBuilder sb = new StringBuilder();
        //    sb.AppendFormat("State of {0} pool\n", typeof(T).Name);
        //    lock (poolDictionary) {
        //        foreach (var pair in poolDictionary) {
        //            sb.AppendFormat("size {0} count {1}\n", pair.Key, pair.Value.Count);
        //        }
        //    }
        //}
    }
}