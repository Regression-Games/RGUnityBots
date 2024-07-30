using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class PlayerInputListeningObject : MonoBehaviour
    {
        public void OnMove(InputValue val)
        {
            Debug.Log("OnMove()");
        }
        
        public void OnJump(InputValue val)
        {
            Debug.Log("OnJump()");
        }

        public void OnAim(InputValue val)
        {
            Debug.Log("OnAim()");
        }
    }
}