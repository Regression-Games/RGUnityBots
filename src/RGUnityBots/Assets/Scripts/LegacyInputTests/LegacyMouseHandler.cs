using UniRx;
using UniRx.Triggers;
using UnityEngine;

public class LegacyMouseHandler : MonoBehaviour
{
    public void Start()
    {
        gameObject.OnMouseDownAsObservable()
            .SelectMany(_ => this.gameObject.UpdateAsObservable())
            .TakeUntil(this.gameObject.OnMouseUpAsObservable())
            .Select(_ => Input.mousePosition)
            .RepeatUntilDestroy(this) // safety way
            .Subscribe(
                x =>
                {
                    Debug.Log($"{gameObject.name} OnMouseDownAsObservable()");
                });
    }

    public void Update()
    {
        if (!Mathf.Approximately(Input.GetAxisRaw("Mouse X"), 0.0f))
        {
            Debug.Log("GetAxisRaw(\"Mouse X\") != 0.0f");
        }
        if (!Mathf.Approximately(Input.GetAxisRaw("Mouse Y"), 0.0f))
        {
            Debug.Log("GetAxisRaw(\"Mouse Y\") != 0.0f");
        }
        if (Input.GetAxisRaw("Mouse ScrollWheel") > 0.0f)
        {
            Debug.Log("GetAxisRaw(\"Mouse ScrollWheel\") > 0.0f");
        }
        if (Input.mouseScrollDelta.x < 0.0f)
        {
            Debug.Log("mouseScrollDelta.x < 0.0f");
        }

        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("GetMouseButtonDown(1)");
        }
        if (Input.GetMouseButton(2))
        {
            Debug.Log("GetMouseButton(2)");
        }
        if (Input.GetMouseButtonUp(0))
        {
            Debug.Log("GetMouseButtonUp(0)");
        }
    }

    public void OnMouseDown()
    {
        Debug.Log($"{gameObject.name} OnMouseDown()");
    }

    public void OnMouseUpAsButton()
    {
        Debug.Log($"{gameObject.name} OnMouseUpAsButton()");
    }

    public void OnMouseUp()
    {
        Debug.Log($"{gameObject.name} OnMouseUp()");
    }

    public void OnMouseDrag()
    {
        Debug.Log($"{gameObject.name} OnMouseDrag()");
    }

    public void OnMouseEnter()
    {
        Debug.Log($"{gameObject.name} OnMouseEnter()");
    }

    public void OnMouseExit()
    {
        Debug.Log($"{gameObject.name} OnMouseExit()");
    }

    public void OnMouseOver()
    {
        Debug.Log($"{gameObject.name} OnMouseOver()");
    }
}
