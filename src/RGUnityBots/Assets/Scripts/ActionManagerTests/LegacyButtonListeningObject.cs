using UnityEngine;

namespace ActionManagerTests
{
    public class LegacyButtonListeningObject : MonoBehaviour
    {
        private const string ButtonName = "Jump";
        
        void Update()
        {
            if (Input.GetButton(ButtonName))
            {
                Debug.Log("Input.GetButton(ButtonName)");
            }

            string btn = "Fire1";
            if (Input.GetButtonDown(btn))
            {
                Debug.Log("Input.GetButtonDown(btn)");
            }

            if (Input.GetButtonUp("Fire2"))
            {
                Debug.Log("Input.GetButtonUp(\"Fire2\")");
            }
        }
    }
}