using UnityEngine;

namespace ActionManagerTests
{
    public static class InputUtil
    {
        public static KeyCode jumpKeyCode = KeyCode.Space;

        private static bool ReadPlayerJump()
        {
            return Input.GetKey(jumpKeyCode);
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