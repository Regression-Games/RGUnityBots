using UnityEngine;

namespace ActionManagerTests
{
    public class LegacyAxisListeningObject : MonoBehaviour
    {
        private string axisName = "Horizontal";
        private string axisName2 = "Vertical";
        
        void Update()
        {
            string axis;
            if (Random.Range(0, 2) == 1)
            {
                axis = axisName;
            }
            else
            {
                axis = axisName2;
            }

            if (Input.GetAxis(axis) > 0.0f)
            {
                Debug.Log($"Input.GetAxis({axis})");
            }

            if (Input.GetAxisRaw("Mouse X") > 0.0f)
            {
                Debug.Log("Input.GetAxis(\"Mouse X\")");
            }

            if (Input.GetAxis("Mouse ScrollWheel") > 0.0f)
            {
                Debug.Log("Input.GetAxis(\"Mouse ScrollWheel\")");
            }
        }
    }
}