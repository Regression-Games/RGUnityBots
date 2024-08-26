using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class RGDraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject restingStatePrefab;

    public GameObject draggingStatePrefab;

    private GameObject draggingStateInstance;

    private GameObject dropTarget;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Vector3 position = new Vector3
        {
            x = eventData.position.x, 
            y = eventData.position.y,
            z = 0
        };

        var instance = Instantiate(draggingStatePrefab, position, draggingStatePrefab.transform.rotation);
        if (instance != null)
        {
            instance.transform.SetParent(transform.root, false);
            draggingStateInstance = instance;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingStateInstance != null)
        {
            draggingStateInstance.transform.position = eventData.position;
        }

        dropTarget = eventData.pointerEnter;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggingStateInstance != null)
        {
            Destroy(draggingStateInstance);
        }

        dropTarget = eventData.pointerEnter;

        var dropZoneScript = dropTarget?.GetComponent<RGDropZone>();
        if (dropZoneScript != null)
        {
            var newCard = Instantiate(restingStatePrefab, new Vector3(), Quaternion.identity);
            dropZoneScript.AddChild(newCard);
        }
    }
}