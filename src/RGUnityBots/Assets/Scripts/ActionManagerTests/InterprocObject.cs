using UnityEngine;

namespace ActionManagerTests
{
    public class InterprocObject : MonoBehaviour
    {
        void Update()
        {
            InputUtil.CheckPlayerJump(gameObject);
        }
    }
}