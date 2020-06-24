using K_PathFinder.Pool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace K_PathFinder {
    //public class AreaFilter {
    //    public Area[] areaArray;
    //    public int areaArrayLength = 0;
    //    public SellectionState areaArrayMode = SellectionState.ExeptTarget;
        
    //    public void SetAreaState(SellectionState sellectionState, params Area[] areas) {
    //        lock (this) {
    //            areaArrayMode = sellectionState;

    //            if (areaArray != null)
    //                GenericPoolArray<Area>.ReturnToPool(ref areaArray);

    //            areaArrayLength = areas.Length;
    //            areaArray = GenericPoolArray<Area>.Take(areaArrayLength);

    //            for (int i = 0; i < areas.Length; i++) {
    //                areaArray[i] = areas[i];
    //            }
    //        }
    //    }

    //    public void SetAreaSellectionState(SellectionState sellectionState) {
    //        lock (this) {
    //            areaArrayMode = sellectionState;
    //        }
    //    }

    //    public void AddArea(Area area) {
    //        if (area == null)
    //            throw new ArgumentException("Area cannot be null");

    //        lock (this) {
    //            if (areaArray == null) {
    //                areaArray = GenericPoolArray<Area>.Take(4);
    //                areaArray[areaArrayLength++] = area;
    //                return;
    //            }

    //            if (!ContainsArea(area)) {
    //                if (areaArrayLength == areaArray.Length)
    //                    GenericPoolArray<Area>.IncreaseSize(ref areaArray);
    //                areaArray[areaArrayLength++] = area;
    //            }
    //        }
    //    }

    //    public bool ContainsArea(Area area) {
    //        if (area == null)
    //            throw new ArgumentException("Area cannot be null");

    //        lock (this) {
    //            for (int i = 0; i < areaArrayLength; i++) {
    //                if (areaArray[i] == area)
    //                    return true;
    //            }
    //            return false;
    //        }
    //    }

    //    public bool RemoveArea(Area area) {
    //        if (area == null)
    //            throw new ArgumentException("Area cannot be null");

    //        lock (this) {
    //            for (int check = 0; check < areaArrayLength; check++) {
    //                if (areaArray[check] == area) {
    //                    for (int shifted = check; shifted < areaArrayLength - 1; shifted++) {
    //                        areaArray[shifted] = areaArray[shifted + 1];
    //                    }
    //                    areaArrayLength--;
    //                    if (areaArrayLength == 0)
    //                        GenericPoolArray<Area>.ReturnToPool(ref areaArray);
    //                    return true;
    //                }
    //            }

    //            return false;
    //        }
    //    }

    //    public void CopyForThreads(out Area[] areas, out int areasCount, out SellectionState areasMode) {
    //        lock (this) {
    //            areas = GenericPoolArray<Area>.Take(areaArray);
    //            areasCount = areaArrayLength;
    //            areasMode = areaArrayMode;
    //        }
    //    }

    //    public int areaCount {
    //        get { lock (this) { return areaArrayLength; } }
    //    }

    //    public string AreasToString() {
    //        lock (this) {
    //            if (areaArrayLength == 0)
    //                return "no groups assigned";

    //            //i belive there no String.Join<T> in this version of C#
    //            StringBuilder sb = new StringBuilder();
    //            string format = "{0}, ";
    //            for (int i = 0; i < areaArrayLength - 1; i++) {
    //                sb.AppendFormat(format, areaArray[i]);
    //            }
    //            sb.Append(areaArray[areaArrayLength - 1]);
    //            return sb.ToString();
    //        }
    //    }
    //}
}