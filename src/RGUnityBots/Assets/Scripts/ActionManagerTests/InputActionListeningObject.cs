using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class InputActionListeningObject : MonoBehaviour
    {
        public InputActionAsset actionAsset;
        
        private InputAction moveAction;
        private InputAction jumpAction;

        public InputAction fireAction;

        private Vector2 lastAim;

        void Start()
        {
            lastAim = ReadAim();

            var actionMap = actionAsset.FindActionMap("ActionMap1");
            moveAction = actionMap.FindAction("Move");
            moveAction.Enable();
            
            jumpAction = actionAsset.FindAction("Jump");
            jumpAction.Enable();
            
            fireAction.Enable();
        }

        private Vector2 ReadAim()
        {
            InputAction aimAction = actionAsset.FindAction("Aim");
            aimAction.Enable();
            return aimAction.ReadValue<Vector2>();
        }
        
        void Update()
        {
            Vector2 moveVal = moveAction.ReadValue<Vector2>();
            if (moveVal.sqrMagnitude > 0.1f)
            {
                Debug.Log("moveVal.sqrMagnitude");
            }

            if (jumpAction.IsPressed())
            {
                Debug.Log("jumpAction.IsPressed()");
            }
            
            var act = fireAction;
            if (act.WasPressedThisFrame())
            {
                Debug.Log("fireAction pressed this frame");
            }

            var aim = ReadAim();
            if ((aim - lastAim).sqrMagnitude > 0.1f)
            {
                Debug.Log("Aim changed");
            }
            lastAim = aim;
        }
    }
}