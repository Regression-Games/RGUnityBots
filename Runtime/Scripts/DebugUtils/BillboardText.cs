using TMPro;
using UnityEngine;

public class BillboardText : MonoBehaviour
{
    private TextMeshProUGUI _text;
    public string content = "";
    public float yOffset = 2f;
    
    void Awake()
    {
        _text = GetComponentInChildren<TextMeshProUGUI>();
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
        else
        {
            Debug.Log("UOH");
        }
        _text.transform.localPosition = new Vector3(0, yOffset, 0);
        if (content != _text.text)
        {
            _text.text = content;
        }
    }

}
