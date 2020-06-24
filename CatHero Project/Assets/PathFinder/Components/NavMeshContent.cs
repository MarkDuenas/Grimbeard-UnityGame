using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder {
    /// <summary>
    /// Basic class for NavMesh content that are laying down on MonoBehaviour 
    /// It is not mandatory to use this class as your base class
    /// Only thing it is realy doing is deriving from ICellContentValueExternal (this is important)
    /// And calling PathFinder.ProcessCellContent if it moved or PathFinder.RemoveCellContent if it no longer exist
    /// PathFinder.ProcessCellContent are adding AND moving target content on navmesh
    /// PathFinder.RemoveCellContent obviously removing it from navmesh
    /// i advice you to cache position cause you really cant use transform.position since updating are done on sepparated thread. as the matter of fact cache everything :)
    /// </summary>
    public class NavMeshContent : MonoBehaviour, ICellContentValueExternal {
        [Range(0f, 10f)]
        public float maxDistance = 2.5f;
        public int pathFinderID { get; set; }
        private Vector3 cachedPosition;


        //process on start
        public virtual void Start() {
            cachedPosition = transform.position;
            PathFinder.ProcessCellContent(this);    
        }

        //also process when enabled
        public virtual void OnEnable() {
            cachedPosition = transform.position;
            PathFinder.ProcessCellContent(this);
        }

        //when disabled remove from navmesh so it cant be found
        public virtual void OnDisable() {
            cachedPosition = transform.position;
            PathFinder.RemoveCellContent(this);
        }

        //on destroy obviously remove this cause it destroyed
        public virtual void OnDestroy() {
            cachedPosition = transform.position;
            PathFinder.RemoveCellContent(this);
        }
        
        //if this moved then check it in update and move it on navmesh
        public virtual void Update() {
            if (transform.hasChanged) {
                transform.hasChanged = false;
                cachedPosition = transform.position;
                PathFinder.ProcessCellContent(this);
            }
        }

        //position are cahced cause we cant use transform in threads. Unity dont allow that
        public virtual Vector3 position {
            get { return cachedPosition; }
        }
        
        //farthest acceptable distance from navmesh
        public float maxNavmeshDistance {
            get { return maxDistance; }
        }
    }
}