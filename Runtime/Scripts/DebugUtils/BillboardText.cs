using TMPro;
using UnityEngine;

public class BillboardText : MonoBehaviour
{
    private TextMeshProUGUI _text;
    private string _content;
    private float _yOffset;
    
    void Awake()
    {
        _text = GetComponentInChildren<TextMeshProUGUI>();
        if (_content != null)
        {
            SetText(_content);
            SetYOffset(_yOffset);
        }
    }
    
    void LateUpdate()
    {
        Transform camTransform = Camera.main != null ? Camera.main.transform : null;
        if (camTransform != null)
        {
            // Set the point of focus of the object to far behind the camera
            Vector3 lookPoint = camTransform.position +
                                ( camTransform.forward) * 100_000;
            transform.LookAt(lookPoint);
        }
    }

    public void SetText(string content)
    {
        // In some cases, the content is set when the object is not awake. Instead, wait until the object is
        // awake
        _content = content;
        if (_text == null) return;
        _text.text = content;
    }

    public void SetYOffset(float yOffset)
    {
        _yOffset = yOffset;
        if (_text == null) return;
        _text.transform.localPosition = new Vector3(0, yOffset, 0);
    }

}
