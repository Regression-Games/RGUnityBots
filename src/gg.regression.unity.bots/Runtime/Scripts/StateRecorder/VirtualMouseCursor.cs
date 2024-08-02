using System;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder
{
    public class VirtualMouseCursor : MonoBehaviour
    {
        public RectTransform mouseTransform;
        public Image cursor;
        public Image clickHighlight;

        public void Start()
        {
            // set the click highlight NOT visible
            var color = clickHighlight.color;
            color.a = 0f;
            clickHighlight.color = color;

            // set the cursor NOT visible
            color = cursor.color;
            color.a = 0f;
            cursor.color = color;
        }

        public void SetPosition(Vector2 position, bool clicked = false)
        {
            mouseTransform.position = position;

            // set whether the click highlight is visible
            var color = clickHighlight.color;
            color.a = clicked ? 1f : 0f;
            clickHighlight.color = color;

            // set whether the cursor is visible
            color = cursor.color;
            color.a = clicked ? 1f : 0f;
            cursor.color = color;
        }
    }
}
