using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class MouseBtnListeningObject : MonoBehaviour
    {
        private int mouseBtn = 1;
        private int otherMouseBtn = 2;
        
        void Update()
        {
            if (Input.GetMouseButton(0))
            {
                Debug.Log("Input.GetMouseButton(0)");
            }

            if (Input.GetMouseButtonDown(mouseBtn))
            {
                Debug.Log("Input.GetMouseButtonDown(mouseBtn)");
            }

            int btn = otherMouseBtn;
            if (Input.GetMouseButtonUp(btn))
            {
                Debug.Log("Input.GetMouseButtonUp(btn)");
            }

            if (Mouse.current.forwardButton.isPressed)
            {
                Debug.Log("Mouse.current.forwardButton.isPressed");
            }

            var mouse = Mouse.current;
            if (mouse.backButton.wasPressedThisFrame)
            {
                Debug.Log("mouse.backButton.wasPressedThisFrame");
            }
        }
    }
    
}