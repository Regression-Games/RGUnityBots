using TMPro;
using UnityEngine;

public class BillboardText : MonoBehaviour
{

    private Camera _mainCamera;
    private TextMeshProUGUI _text;
    private string _content;

    // Start is called before the first frame update
    void Awake()
    {
        _mainCamera = Camera.main;
        _text = GetComponentInChildren<TextMeshProUGUI>();
        // Rotate the text 180 degrees, otherwise billboard will show it backwards
        _text.transform.Rotate(Vector3.up, 180);
        // Also shift the text up to be above the agent
        _text.transform.transform.position += new Vector3(0, 2, 0);
        if (_content != null)
        {
            SetText(_content);
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(_mainCamera.transform);
    }

    public void SetText(string content)
    {
        // In some cases, the content is set when the object is not awake. Instead, wait until the object is
        // awake
        _content = content;
        if (_text == null) return;
        _text.text = content;
    }

}
