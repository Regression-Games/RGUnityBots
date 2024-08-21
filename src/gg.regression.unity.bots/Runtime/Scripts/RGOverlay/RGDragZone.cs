using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class RGDragZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    void Start()
    {
        Debug.Log("Start");
    }

    public void AddChild(GameObject newChild)
    {
        newChild.transform.SetParent(this.transform, false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            Debug.Log($"Draggable Card is over the Drag Zone: {eventData.pointerDrag.name}");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            Debug.Log($"Draggable Card is exiting the Drag Zone: {eventData.pointerDrag.name}");
        }
    }
}