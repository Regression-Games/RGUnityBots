using UnityEngine;

public class LegacyAxisHandler : MonoBehaviour
{
    void Update()
    {
        if (Input.GetAxis("Horizontal") > 0.0f)
        {
            Debug.Log("GetAxis(\"Horizontal\") > 0.0f");
        } else if (Input.GetAxis("Horizontal") < 0.0f)
        {
            Debug.Log("GetAxis(\"Horizontal\") < 0.0f");
        }

        if (Input.GetAxisRaw("Vertical") > 0.0f)
        {
            Debug.Log("GetAxisRaw(\"Vertical\") > 0.0f");
        } else if (Input.GetAxisRaw("Vertical") < 0.0f)
        {
            Debug.Log("GetAxisRaw(\"Vertical\") < 0.0f");
        }
    }
}