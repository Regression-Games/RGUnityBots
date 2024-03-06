using System.Collections;
using System.Collections.Generic;
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

    void Start()
    {

    }

    public void Initialize(GameObject runtimeObject, string behavior)
    {
        gameObjectName.text = runtimeObject.name;
        behaviorName.text = behavior;
        if (string.IsNullOrEmpty(behavior))
        {
            behaviorName.text = "Empty";
        }
        deleteButton.onClick.AddListener(() =>
        {
            GameObject.Destroy(runtimeObject);
            GameObject.Destroy(gameObject);
        });
    }

    public void Delete()
    {
        deleteButton.onClick.Invoke();
    }
}
