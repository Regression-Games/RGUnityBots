using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class InputCsWrapperListeningObject : MonoBehaviour, MyInputs.IActionMap2Actions
    {
        public MyInputs myInputs;
        
        void Start()
        {
            myInputs = new MyInputs();
            myInputs.ActionMap2.Enable();
            myInputs.ActionMap2.SetCallbacks(this);
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            Debug.Log("Crouch pressed");
        }

        public void OnHorizontal(InputAction.CallbackContext context)
        {
            float horizVal = context.ReadValue<float>();
            if (horizVal > 0.0f)
            {
                Debug.Log("horizVal > 0");
            } else if (horizVal < 0.0f)
            {
                Debug.Log("horizVal < 0");
            }
        }
    }
}