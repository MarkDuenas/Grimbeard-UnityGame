using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Pool {
    public static class GenericPool<T> where T : class, new() {
        public const int INITIAL_SIZE = 1;

        static Stack<T> pool = new Stack<T>();

        static GenericPool(){
            for (int i = 0; i < INITIAL_SIZE; i++) {
                pool.Push(new T());
            }

#if UNITY_EDITOR
            PFDebuger.Debuger_K.ObjectPoolGenericRegisterEvent(typeof(T), 0, pool.Count, INITIAL_SIZE);
#endif
        }

        public static T Take() {
            //Debug.Log(typeof(T).Name + " :  take");
            lock (pool) {
                T result;

                if (pool.Count > 0) {
                    result = pool.Pop();
                    #if UNITY_EDITOR
                    PFDebuger.Debuger_K.ObjectPoolGenericRegisterEvent(typeof(T), 1, pool.Count, 0);
                    #endif
                }
                else {
                    result = new T();
                    #if UNITY_EDITOR
                    PFDebuger.Debuger_K.ObjectPoolGenericRegisterEvent(typeof(T), 0, pool.Count, 1);
                    #endif
                }

                return result;         
            }
        }

        public static void ReturnToPool(ref T obj) {
            //Debug.Log(typeof(T).Name + " :  return");
            lock (pool) {
                pool.Push(obj);
                #if UNITY_EDITOR
                PFDebuger.Debuger_K.ObjectPoolGenericRegisterEvent(typeof(T), -1, pool.Count, 0);
                #endif
            }

            obj = null;
        }
        
        public static void ReturnToPool(T obj) {
            ReturnToPool(ref obj);
        }
    }
}