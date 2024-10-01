using UnityEngine;

public class AutoDisable : MonoBehaviour
{

    public float duration = 1f;

    private float startTime = -1f;

    public void OnEnable()
    {
        startTime = Time.time;
    }

    public void Update()
    {
        if (Time.time - startTime > duration)
        {
            this.gameObject.SetActive(false);
        }
    }
}
