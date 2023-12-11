using UnityEngine;

namespace RGThirdPersonDemo
{
    public class LookAtCamera : MonoBehaviour
    {
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (mainCamera != null)
            {
                // Get the direction from the Canvas to the main camera.
                Vector3 directionToCamera = transform.position - mainCamera.transform.position;

                // Make the Canvas look at the main camera.
                transform.forward = directionToCamera.normalized;
            }
        }
    }
}