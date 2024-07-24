using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class MouseMovementListeningObject : MonoBehaviour
    {
        private Vector3 lastMousePos;

        void Start()
        {
            lastMousePos = Input.mousePosition;
        }
        
        void Update()
        {
            Vector3 mousePos1 = Input.mousePosition;
            Vector3 mousePos2 = Mouse.current.position.value;
            if (mousePos1 != lastMousePos)
            {
                Debug.Log("mousePos1 != lastMousePos");
            }
            if (mousePos2 != lastMousePos)
            {
                Debug.Log("mousePos2 != lastMousePos");
            }

            lastMousePos = mousePos1;

            if (Input.mouseScrollDelta.sqrMagnitude > 0.1f)
            {
                Debug.Log("Input.mouseScrollDelta.sqrMagnitude");
            }

            if (Mouse.current.scroll.value.sqrMagnitude > 0.1f)
            {
                Debug.Log("Mouse.current.scroll.value.sqrMagnitude");
            }
        }
    }
}