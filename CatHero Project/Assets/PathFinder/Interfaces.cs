using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    /// <summary>
    /// since owner of path now object and path returned to anything
    /// this anything should have sort of "fall back" position in case there is no path
    /// this position will be returned in case there is no valid path
    /// </summary>
    public interface IPathOwner {
        Vector3 pathFallbackPosition { get; }
    }


    /// <summary>
    /// interface to add bounds to chunk map
    /// userful if you want to inclide some information that can be described as bounds to navmesh generation
    /// read PathFinderMainChunkContentMap.cs to understand how to use it 
    /// </summary>
    public interface IChunkContent {
        /// <summary>
        /// use it to define space that this content take
        /// this value should be cached in case it is using Unity API cause it called in sepparated thread
        /// </summary>
        Bounds chunkContentBounds { get; }
    }

    /// <summary>
    /// basic value that added to Cell on navmesh. 
    /// only have it's position 
    /// used to add values during navmesh generation cause it have nothing to tell if it should be added (or not) (or how)
    /// </summary>
    public interface ICellContentValue {
        Vector3 position { get; }
    }

    /// <summary>
    /// value that added externaly on navmesh
    /// have position and maximum distance from navmesh that acceptable
    /// to understand how to use it read PathFinderMainCellContentMap.cs
    /// In general only 2 methods are used to fiddle with this:
    /// PathFinder.ProcessCellContent - add or update value on navmesh
    /// PathFinder.RemoveCellContent - remove value from navmesh
    /// this value can be extracted in various ways with help of queries
    /// only navmesh values are serialized. your own values should be serialized by you
    /// </summary>
    public interface ICellContentValueExternal : ICellContentValue {
        int pathFinderID { get; set; }
        float maxNavmeshDistance { get; }
    }
}