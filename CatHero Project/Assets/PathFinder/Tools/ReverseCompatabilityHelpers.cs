using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    //Backward Compatibility
    public class BC {
        public static Quaternion GetRotation(Matrix4x4 matrix) {
#if UNITY_2018_1_OR_NEWER
            return matrix.rotation;
#else
            return Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
#endif
        }

        public static Vector3 GetLossyScale(Matrix4x4 matrix) {
#if UNITY_2018_1_OR_NEWER
            return matrix.lossyScale;
#else
            return new Vector3(
                matrix.GetColumn(0).magnitude,
                matrix.GetColumn(1).magnitude,
                matrix.GetColumn(2).magnitude
            );
#endif


        }
    }
}