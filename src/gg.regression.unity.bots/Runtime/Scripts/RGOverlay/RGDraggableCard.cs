using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class RGDraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool IsReordering;
    
    public string draggableCardName;
    
    public TMP_Text namePrefab;
    
    public GameObject restingStatePrefab;

    public GameObject draggingStatePrefab;

    private GameObject draggingStateInstance;

    private RGDropZone _dropZone;

    public void Start()
    {
        IsReordering = false;
        
        if (namePrefab != null)
        {
            namePrefab.text = draggableCardName;
        }
    }
    
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
            var dragged = instance.GetComponent<RGDraggedCard>();
            if (dragged != null)
            {
                dragged.draggedCardName = draggableCardName;
            }
            
            instance.transform.SetParent(transform.root, false);
            draggingStateInstance = instance;
        }

        Vector2 mouseScreenSpacePosition = Input.mousePosition;

        var potentialDropZones = GameObject.FindObjectsOfType<RGDropZone>();
        foreach (RGDropZone dz in potentialDropZones)
        {
            var dropZoneRectTransform = dz.GetComponent<RectTransform>();
            var localMousePos = dropZoneRectTransform.InverseTransformPoint(mouseScreenSpacePosition);
            
            if (dropZoneRectTransform.rect.Contains(localMousePos))
            {
                var ev = new PointerEventData(EventSystem.current);
                ev.pointerDrag = this.gameObject;
                dz.OnPointerEnter(ev);
                IsReordering = true;
                _dropZone = dz;
                break;
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingStateInstance != null)
        {
            draggingStateInstance.transform.position = eventData.position;        
        }

        if (_dropZone == null)
        {
            _dropZone = eventData.pointerEnter?.GetComponent<RGDropZone>();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggingStateInstance != null)
        {
            Destroy(draggingStateInstance);
        }

        if (_dropZone == null)
        {
            Debug.Log("NO DROP SONWE");
            return;
        }

        if (IsReordering)
        {
            IsReordering = false;
            _dropZone.FinishReordering();
            _dropZone = null;
            return;
        }
        
        var newCard = Instantiate(restingStatePrefab);
        _dropZone.AddChild(newCard);
        _dropZone = null;
    }

    public void OnExitDropZone()
    {
        // if (draggingStateInstance != null)
        // {
        //     Destroy(draggingStateInstance);
        // }
        
        _dropZone = null;
    }
}