using TMPro;
using UnityEngine;

public class BillboardText : MonoBehaviour
{

    private Camera _mainCamera;
    private TextMeshProUGUI _text;
    private string _content;
    private float _yOffset;
    
    void Awake()
    {
        _mainCamera = Camera.main;
        _text = GetComponentInChildren<TextMeshProUGUI>();
        if (_content != null)
        {
            SetText(_content);
            SetYOffset(_yOffset);
        }
    }
    
    void LateUpdate()
    {
        Vector3 newRotation = _mainCamera.transform.eulerAngles;
        newRotation.x = 0;
        newRotation.z = 0;
        transform.eulerAngles = newRotation;
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
