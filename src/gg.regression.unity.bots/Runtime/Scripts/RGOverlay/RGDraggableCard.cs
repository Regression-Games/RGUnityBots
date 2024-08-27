using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class RGDraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string draggableCardName;
    
    public TMP_Text namePrefab;
    
    public GameObject restingStatePrefab;

    public GameObject draggingStatePrefab;

    private GameObject draggingStateInstance;

    private RGDropZone _dropZone;

    public void Start()
    {
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
                Debug.Log("DRAGGED CARD NAME SET");
            }
            
            instance.transform.SetParent(transform.root, false);
            draggingStateInstance = instance;
        }

        Vector2 mouseScreenSpacePosition = Input.mousePosition;

        var potentialDropZones = GameObject.FindObjectsOfType<RGDropZone>();
        foreach (RGDropZone dz in potentialDropZones)
        {
            var dropZoneRectTransform = dz.GetComponent<RectTransform>();

            // get screenspace location of the mouse cursor, within the bounds of this drop zone
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dropZoneRectTransform,
                mouseScreenSpacePosition,
                null, 
                out localPoint
            ); 

            if (localPoint.x >= 0 &&
                localPoint.x <= dropZoneRectTransform.rect.width &&
                localPoint.y >= 0 &&
                localPoint.y <= dropZoneRectTransform.rect.height)
            {
                var ev = new PointerEventData(EventSystem.current);
                ev.pointerDrag = this.gameObject;
                dz.OnPointerEnter(ev);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingStateInstance != null)
        {
            draggingStateInstance.transform.position = eventData.position;        
        }

        _dropZone = eventData.pointerEnter?.GetComponent<RGDropZone>();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggingStateInstance != null)
        {
            Destroy(draggingStateInstance);
        }

        if (_dropZone == null)
        {
            return;
        }

        var newCard = Instantiate(restingStatePrefab, new Vector3(), Quaternion.identity);
        _dropZone.AddChild(newCard);
        _dropZone = null;
    }
}