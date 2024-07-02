using System;
using UnityEngine;

namespace ActionManagerTests
{
    public class MouseHandlerObject : MonoBehaviour
    {
        void OnMouseOver()
        {
            Debug.Log($"OnMouseOver {gameObject.name}");
        }

        void OnMouseEnter()
        {
            Debug.Log($"OnMouseEnter {gameObject.name}");
        }

        void OnMouseExit()
        {
            Debug.Log($"OnMouseExit {gameObject.name}");
        }

        void OnMouseDown()
        {
            Debug.Log($"OnMouseDown {gameObject.name}");
        }

        void OnMouseUp()
        {
            Debug.Log($"OnMouseUp {gameObject.name}");
        }

        void OnMouseUpAsButton()
        {
            Debug.Log($"OnMouseUpAsButton {gameObject.name}");
        }

        void OnMouseDrag()
        {
            Debug.Log($"OnMouseDrag {gameObject.name}");
        }
    }
}