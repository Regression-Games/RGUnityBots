using UnityEngine;

namespace ActionManagerTests
{
    public class MouseRaycast3DObject : MonoBehaviour
    {
        public LayerMask layerMask;
        
        void Update()
        {
            var mousePos = Input.mousePosition;
            var ray = Camera.main.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, layerMask))
            {
                Debug.Log("Hit 3D game object " + hit.collider.gameObject.name);
            }

        }
    }
}