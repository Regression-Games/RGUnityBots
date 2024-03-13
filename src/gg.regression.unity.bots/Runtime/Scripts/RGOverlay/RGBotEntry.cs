using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RGBotEntry : MonoBehaviour
{
    [SerializeField]
    private TMP_Text gameObjectName;

    [SerializeField]
    private TMP_Text behaviorName;

    [SerializeField]
    private Button deleteButton;

    private GameObject _runtimeObject;

    public void Initialize(GameObject runtimeObject, string behavior)
    {
        _runtimeObject = runtimeObject;
        gameObjectName.text = runtimeObject.name;
        behaviorName.text = behavior;
        if (string.IsNullOrEmpty(behavior))
        {
            behaviorName.text = "Empty";
        }
        deleteButton.onClick.AddListener(() =>
        {
            GameObject.Destroy(_runtimeObject);
            GameObject.Destroy(gameObject);
            _runtimeObject = null;
        });
    }

    public void Delete()
    {
        deleteButton.onClick.Invoke();
    }

    public bool IsRuntimeObjectDestroyed()
    {
        return _runtimeObject == null || !_runtimeObject.activeInHierarchy;
    }
}
