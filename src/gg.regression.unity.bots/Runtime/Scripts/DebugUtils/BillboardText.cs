using RegressionGames;
using TMPro;
using UnityEngine;

public class BillboardText : MonoBehaviour
{
    private TextMeshProUGUI _text;
    public string content = "";
    public Vector3 offset = new Vector3(0f, 2f, 0f);
    public GameObject target;

    void Awake()
    {
        _text = GetComponentInChildren<TextMeshProUGUI>();
    }

    void LateUpdate()
    {
        // First, if the entity we were a part of is gone, destroy ourselves
        if (target == null || !target.activeInHierarchy)
        {
            RGDebug.LogVerbose("Deleting billboard text because target is gone");
            Destroy(this);
            return;
        }

        // Now update our position to be the same as the target
        transform.position = target.transform.position + offset;

        // Then rotate to face the camera
        Transform camTransform = Camera.main != null ? Camera.main.transform : null;
        if (camTransform != null)
        {
            // Set the point of focus of the object to far behind the camera
            Vector3 lookPoint = camTransform.position +
                                (camTransform.forward * 100_000);
            transform.LookAt(lookPoint);
        }

        // Finally, update the text of the billboard
        if (content != _text.text)
        {
            _text.text = content;
        }
    }

}
