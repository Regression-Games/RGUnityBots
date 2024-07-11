using UnityEngine;

namespace ActionManagerTests
{

    public class Keybindings
    {
        public KeyCode jumpKey = KeyCode.UpArrow;
        public string fireKey = "space";
        public KeyCode CrouchKey => KeyCode.DownArrow;
    }

    public struct GameSettings
    {
        public Keybindings bindings;
    }

    public class LegacyKeyListeningObject : MonoBehaviour
    {
        public const KeyCode MOVE_RIGHT_KEY = KeyCode.RightArrow;
        private GameSettings _gameSettings;

        void Start()
        {
            _gameSettings.bindings = new Keybindings();
        }

        private void HandleOtherInputs()
        {
            if (Input.GetKey(MOVE_RIGHT_KEY))
            {
                Debug.Log("GetKey(MOVE_RIGHT_KEY)");
            } else if (Input.GetKeyDown(_gameSettings.bindings.fireKey))
            {
                Debug.Log("GetKeyDown(_gameSettings.bindings.fireKey)");
            }

            if (Input.GetKeyUp(_gameSettings.bindings.jumpKey))
            {
                Debug.Log("GetKeyUp(_gameSettings.bindings.jumpKey)");
            }

            var crouchKey = _gameSettings.bindings.CrouchKey;
            if (Input.GetKey(crouchKey))
            {
                Debug.Log("Input.GetKey(crouchKey)");
            }

            if (Input.anyKey)
            {
                Debug.Log("Input.anyKey");
            }

            if (Input.anyKeyDown)
            {
                Debug.Log("Input.anyKeyDown");
            }
        }
    
        void Update()
        {
            var aimKey = KeyCode.LeftShift;
            if (Input.GetKey(aimKey))
            {
                Debug.Log("GetKey(aimKey)");
            }
            HandleOtherInputs();
        }
    }
    
}