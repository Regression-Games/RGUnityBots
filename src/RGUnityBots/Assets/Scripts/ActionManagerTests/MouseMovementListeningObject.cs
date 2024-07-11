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
            Vector3 mousePos;
            if (Random.Range(0, 2) == 1)
            {
                mousePos = Mouse.current.position.value;
            }
            else
            {
                mousePos = Input.mousePosition;
            }
            if (mousePos != lastMousePos)
            {
                Debug.Log("mousePos != lastMousePos");
                lastMousePos = mousePos;
            }

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