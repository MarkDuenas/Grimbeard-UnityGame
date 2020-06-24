using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace K_PathFinder.Samples {
    public class Example17Door : MonoBehaviour {
        public Transform doorTransform;
        public Transform[] handles;
        public float doorAngle = 100;
        public float doorTime = 0.5f;
        public bool startClosed = true;

        [LayerPF] public int walkableLayer; //layer that signifies that area is passable
        public AreaWorldMod closedDoorMod;
        [LayerPF] public int closedDoorTargetLayer; //layer that signifies that closed area is not passable
        public AreaWorldMod openedDoorMod;
        [LayerPF] public int openedDoorTargetLayer; //layer that signifies that open area is not passable

          
        Quaternion doorClosedRotation;
        Quaternion doorOpenedRotation;

        CellPathContentDoorData doorData;
        List<DoorHandle> doorHandlesData;

        void Awake() {
            if (startClosed) {
                doorClosedRotation = doorTransform.rotation;
                doorOpenedRotation = doorTransform.rotation * Quaternion.AngleAxis(doorAngle, Vector3.up);
            }
            else {
                doorOpenedRotation = doorTransform.rotation * Quaternion.Inverse(Quaternion.AngleAxis(doorAngle, Vector3.up));
                doorClosedRotation = doorTransform.rotation;
            }

            doorData = new CellPathContentDoorData(this, startClosed);
            closedDoorMod.AddCellPathContent(doorData);

            doorHandlesData = new List<DoorHandle>();
            foreach (var handle in handles) {
                DoorHandle newHandle = new DoorHandle(handle.position, doorData);
                doorHandlesData.Add(newHandle);
                PathFinder.ProcessCellContent(newHandle);
            }
        }

        private void OnDestroy() {
            foreach (var item in doorHandlesData) {
                PathFinder.RemoveCellContent(item);
            }
        }

        void OnDrawGizmos() {
            Color color = Gizmos.color;
            Gizmos.color = Color.blue;
            foreach (var item in handles) {
                Gizmos.DrawLine(doorTransform.position, item.position);
            }
            Gizmos.color = color;
        }

        public void UseHandle(Action callback) {
            //ot only opens door
            StartCoroutine(ToggleDoorCoroutine(false, callback));
        }

        IEnumerator ToggleDoorCoroutine(bool close, Action callback) {
            if (doorData.closed == close)
                yield return null;

            Quaternion curRotation, targetRotation;
            if (close) {
                curRotation = doorOpenedRotation;
                targetRotation = doorClosedRotation;
            }
            else {
                targetRotation = doorOpenedRotation;
                curRotation = doorClosedRotation;
            }
            
            int steps = (int)(doorTime / Time.deltaTime);
            float lerpStep = 1f / steps;

            for (int i = 0; i < steps + 1; i++) {
                doorTransform.rotation = Quaternion.Lerp(curRotation, targetRotation, i * lerpStep);
                yield return null;
            }

            doorTransform.rotation = targetRotation;
            doorData.closed = close;
            if (close) {
                closedDoorMod.SetCellsLayer(closedDoorTargetLayer);
                openedDoorMod.SetCellsLayer(walkableLayer);
            }
            else {
                closedDoorMod.SetCellsLayer(walkableLayer);
                openedDoorMod.SetCellsLayer(openedDoorTargetLayer);
            }
            callback.Invoke();
            yield return null;
        }
    }

    class CellPathContentDoorData : CellPathContentAbstract {
        public Example17Door sceneDoor;
        public bool closed;

        public CellPathContentDoorData(Example17Door sceneDoor, bool closed) {
            this.sceneDoor = sceneDoor;
            this.closed = closed;
        }
    }

    class DoorHandle : ICellContentValueExternal {
        public CellPathContentDoorData door;
        public Vector3 position { get; private set; }
        public int pathFinderID { get; set; }

        public DoorHandle(Vector3 position, CellPathContentDoorData door) {
            this.position = position;
            this.door = door;
        }

        public float maxNavmeshDistance {
            get { return float.MaxValue; }
        }
    }
}