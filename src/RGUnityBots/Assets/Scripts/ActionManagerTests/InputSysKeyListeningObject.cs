

using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class InputSysKeyListeningObject : MonoBehaviour
    {
        private Key fireKey = Key.Backspace;
    
        void Update()
        {
            if (Keyboard.current[Key.F2].isPressed)
            {
                Debug.Log("Keyboard.current[Key.A].isPressed");
            }
        
            if (Keyboard.current.backslashKey.isPressed)
            {
                Debug.Log("Keyboard.current.backslashKey.isPressed");
            }

            Key key = fireKey;
            var keyboard = Keyboard.current;
            if (keyboard[key].isPressed)
            {
                Debug.Log("keyboard[key].isPressed");
            }

            if (keyboard.altKey.wasPressedThisFrame)
            {
                Debug.Log("keyboard.altKey.wasPressedThisFrame");
            }

            if (Keyboard.current.anyKey.wasPressedThisFrame)
            {
                Debug.Log("Keyboard.current.anyKey.wasPressedThisFrame");
            }
        }
    }
    
}