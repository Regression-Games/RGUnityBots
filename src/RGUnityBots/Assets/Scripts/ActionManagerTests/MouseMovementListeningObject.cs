using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class MouseMovementListeningObject : MonoBehaviour
    {
        #if ENABLE_LEGACY_INPUT_MANAGER
        private Vector3 lastMousePos1;
        #endif
        private Vector3 lastMousePos2;

        void Start()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            lastMousePos1 = Input.mousePosition;
            #endif
            lastMousePos2 = Mouse.current.position.value;
        }
        
        void Update()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            Vector3 mousePos1 = Input.mousePosition;
            if (mousePos1 != lastMousePos1)
            {
                Debug.Log("mousePos1 != lastMousePos1");
            }
            lastMousePos1 = mousePos1;
            #endif
            
            Vector3 mousePos2 = Mouse.current.position.value;
            if (mousePos2 != lastMousePos2)
            {
                Debug.Log("mousePos2 != lastMousePos2");
            }
            lastMousePos2 = mousePos2;

            #if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.mouseScrollDelta.sqrMagnitude > 0.1f)
            {
                Debug.Log("Input.mouseScrollDelta.sqrMagnitude");
            }
            #endif

            if (Mouse.current.scroll.value.sqrMagnitude > 0.1f)
            {
                Debug.Log("Mouse.current.scroll.value.sqrMagnitude");
            }
        }
    }
}