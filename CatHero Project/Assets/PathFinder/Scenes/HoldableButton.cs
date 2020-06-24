using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace K_PathFinder.Samples {
    public class HoldableButton : Button, IPointerDownHandler, IPointerUpHandler {
        bool pointerDown = false, pointerOnButton = false;

        void Update() {
            if (pointerDown & pointerOnButton)
                onClick.Invoke();
        } 

        public override void OnPointerDown(PointerEventData eventData) {
            base.OnPointerDown(eventData);
            pointerDown = true;
        }

        public override void OnPointerUp(PointerEventData eventData) {
            base.OnPointerUp(eventData);
            pointerDown = false;
        }

        public override void OnPointerEnter(PointerEventData eventData) {
            base.OnPointerEnter(eventData);
            pointerOnButton = true;
        }

        public override void OnPointerExit(PointerEventData eventData) {
            base.OnPointerExit(eventData);
            pointerOnButton = false;
        }
    }
}
