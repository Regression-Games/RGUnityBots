using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder
{
    public class VirtualMouseCursor : MonoBehaviour
    {
        public RectTransform mouseTransform;
        public Image clickHighlight;

        public void SetPosition(Vector2 position, bool clicked = false)
        {
            mouseTransform.position = position;
            var color = clickHighlight.color;
            color.a = clicked ? 1f : 0f;
            clickHighlight.color = color;
        }
    }
}
