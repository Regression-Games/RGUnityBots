using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class MouseRaycast3DObject : MonoBehaviour
    {
        public LayerMask layerMask;
        
        void Update()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            var mousePos = Input.mousePosition;
            #else
            var mousePos = Mouse.current.position.value;
            #endif
            var ray = Camera.main.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, layerMask))
            {
                Debug.Log("Hit 3D game object " + hit.collider.gameObject.name);
            }

        }
    }
}