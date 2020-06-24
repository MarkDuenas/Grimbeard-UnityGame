using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace K_PathFinder.Samples {
    public class NicePointer : MonoBehaviour {
        public float lineHeight = 1.5f;
        public float lineWidth = 0.15f;
        public GameObject collum;
        public GameObject pointerPoint;

        public GameObject[] lineTop;
        public GameObject[] lineBottom;

        public Vector3 positionTop { get; private set; }
        public Vector3 positionBottom { get; private set; }

        public GameObject cameraGameObject;

        PointerEventData _pointerEventData;
        List<RaycastResult> _eventHits = new List<RaycastResult>();
        Camera _camera;

        public bool debugNavmeshPosition = true;
        public AgentProperties properties;

        void Start() {
            _pointerEventData = new PointerEventData(EventSystem.current);
            _camera = cameraGameObject.GetComponent<Camera>();
        }
        
        void Update() {
            if (collum == null)
                return;

            //detecting if pointer over button  
            bool pointerOverButton = false;
            _pointerEventData.position = Input.mousePosition;
            EventSystem.current.RaycastAll(_pointerEventData, _eventHits);

            foreach (var item in _eventHits) {
                //dont do anything if mouse over button
                if (item.gameObject.GetComponent<Button>() != null) {
                    pointerOverButton = true;
                    break;
                }
            }

            //update pointer position in scene
            RaycastHit hit;
            if (pointerOverButton == false && Physics.Raycast(_camera.ScreenPointToRay(Input.mousePosition), out hit, 10000f, 1)) {
                transform.position = hit.point;

                positionBottom = hit.point;
                positionTop = new Vector3(positionBottom.x, positionBottom.y + lineHeight, positionBottom.z);

                collum.transform.position = (positionBottom + positionTop) * 0.5f;
                collum.transform.localScale = new Vector3(lineWidth, lineHeight, lineWidth);

                foreach (var item in lineBottom) { item.transform.position = positionBottom; }
                foreach (var item in lineTop) { item.transform.position = positionTop; }
            }


            if (debugNavmeshPosition && properties != null) {
                var sample = PathFinder.TryGetClosestCell(transform.position, properties);

                if (sample.type != NavmeshSampleResultType.InvalidNoNavmeshFound) {
                    if (sample.type == NavmeshSampleResultType.OutsideNavmesh)
                        Debug.DrawLine(transform.position, sample.position, Color.blue);
                    else
                        Debug.DrawLine(transform.position, sample.position, Color.blue);

                    if (sample.type == NavmeshSampleResultType.OutsideNavmesh)
                        Debug.DrawLine(sample.cell.centerVector3, sample.position, Color.cyan);
                }
            }
        }
    }
}