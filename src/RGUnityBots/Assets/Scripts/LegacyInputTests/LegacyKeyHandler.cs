using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegacyKeyHandler : MonoBehaviour
{
    public void Update()
    {
        if (Input.GetKey(KeyCode.X))
        {
            Debug.Log("GetKey(X)");
        }

        if (Input.GetKeyDown("x"))
        {
            Debug.Log("GetKeyDown(\"x\")");
        }

        if (Input.GetKeyUp(KeyCode.X))
        {
            Debug.Log("GetKeyUp(X)");
        }

        if (Input.anyKey)
        {
            Debug.Log("anyKey");
        }

        if (Input.anyKeyDown)
        {
            Debug.Log("anyKeyDown");
        }

        if (Input.GetButton("Jump"))
        {
            Debug.Log("GetButton(\"Jump\")");
        } else if (Input.GetButtonUp("Jump"))
        {
            Debug.Log("GetButtonUp(\"Jump\")");
        }

        if (Input.GetButtonDown("Fire1"))
        {
            Debug.Log("GetButtonDown(\"Fire1\")");
        }
    }
}
