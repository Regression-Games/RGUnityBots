using UnityEngine;
using UnityEngine.UI;

namespace StateRecorder
{
    public class VirtualMouseCursor : MonoBehaviour
    {
        public RectTransform mouseTransform;

        private Image icon;

        private void Awake()
        {
            icon = mouseTransform.GetComponent<Image>();
        }

        public void SetPosition(Vector2 position, bool clicked = false)
        {
            mouseTransform.position = position;
            var color = icon.color;
            color.a = clicked ? 1f : 0.7f;
            icon.color = color;
        }
    }
}
