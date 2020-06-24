using UnityEngine;

namespace K_PathFinder.Samples {
    public class NiceCamera : MonoBehaviour {
        public GameObject cameraMount;
        public float cameraRotationSpeed = 30f;
        public GameObject cameraFollowTarget;
        public float cameraFollowLerp = 0.5f;

        public void RotateCamera(bool right) {
            cameraMount.transform.Rotate(0f, (right ? cameraRotationSpeed : -cameraRotationSpeed) * Time.deltaTime, 0f);  
        }

        private void Update() {
            if (cameraFollowTarget != null) 
                cameraMount.transform.position = Vector3.Lerp(cameraMount.transform.position, cameraFollowTarget.transform.position, cameraFollowLerp);            
        }
    }
}