﻿using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionManagerTests
{
    public class MouseRaycast2DObject : MonoBehaviour
    {
        void Update()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            var mousePos = Input.mousePosition;
            #else
            var mousePos = Mouse.current.position.value;
            #endif
            var worldPt = Camera.main.ScreenToWorldPoint(mousePos);
            var hit2D = Physics2D.Raycast(worldPt, Vector2.zero);
            if (hit2D.collider != null)
            {
                Debug.Log("Hit 2D game object " + hit2D.collider.gameObject.name);
            }

            var pos = gameObject.transform.position;
            if (Physics2D.Raycast(pos, Vector2.right).collider != null) // Analysis should NOT include this one
            {
                transform.position += Vector3.right * Time.deltaTime;
            }
        }
    }
}