using UnityEngine;

namespace ActionManagerTests
{
    public class MouseRaycast3DObject : MonoBehaviour
    {
        void Update()
        {
            var mousePos = Input.mousePosition;
            var ray = Camera.main.ScreenPointToRay(mousePos);
            int layerMask = 3;
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, layerMask))
            {
                Debug.Log("Hit game object " + hit.collider.gameObject.name);
            }

        }
    }
}