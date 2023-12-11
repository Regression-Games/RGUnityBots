using UnityEngine;
using UnityEngine.Events;

namespace RGThirdPersonDemo
{
    public class CheckCameraVisibility : MonoBehaviour
    {
        public UnityEvent onBecameVisible = new UnityEvent();
        public UnityEvent onBecameInvisible = new UnityEvent();

        private Camera _mainCamera;
        private bool _wasVisible;
        
        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            Vector3 viewportPoint = _mainCamera.WorldToViewportPoint(transform.position);

            if (viewportPoint.x > 0 && viewportPoint.x < 1 && viewportPoint.y > 0 && viewportPoint.y < 1 &&
                viewportPoint.z > 0)
            {
                if (!_wasVisible)
                {
                    _wasVisible = true;
                    onBecameVisible.Invoke();
                }
            }
            else
            {
                if (_wasVisible)
                {
                    _wasVisible = false;
                    onBecameInvisible.Invoke();
                }
            }
        }
    }
}