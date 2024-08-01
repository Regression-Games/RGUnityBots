using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public static class InputUtil
    {
        #if ENABLE_LEGACY_INPUT_MANAGER
        public static KeyCode jumpKeyCode = KeyCode.Space;
        #else
        public static Key jumpKeyCode = Key.Space;
        #endif

        private static bool ReadPlayerJump()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(jumpKeyCode);
            #else
            return Keyboard.current[jumpKeyCode].isPressed;
            #endif
        }

        public static void CheckPlayerJump(GameObject gameObject)
        {
            if (ReadPlayerJump())
            {
                gameObject.transform.position += Vector3.up;
                Debug.Log("Player jumped");
            }
        }
        
    }
}