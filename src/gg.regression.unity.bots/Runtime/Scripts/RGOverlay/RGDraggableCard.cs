using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * A component that can be dragged and dropped, and also reordered, within an RGDropZone instance
     * </summary>
     */
    public class RGDraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool IsReordering;

        public string draggableCardName;

        public Sprite icon;
        
        public GameObject iconPrefab;

        public TMP_Text namePrefab;

        public GameObject restingStatePrefab;

        public GameObject draggingStatePrefab;

        private GameObject _draggingStateInstance;

        private RGDropZone _dropZone;

        public void Start()
        {
            IsReordering = false;

            if (namePrefab != null)
            {
                namePrefab.text = draggableCardName;
            }

            if (iconPrefab != null)
            {
                iconPrefab.GetComponent<Image>().overrideSprite = icon;
            }
        }

        /**
         * <summary>
         * When starting to drag this card, create the prefab that represents this card in motion
         * </summary>
         * <param name="eventData">Cursor event data</param>
         */
        public void OnBeginDrag(PointerEventData eventData)
        {
            var position = new Vector3
            {
                x = eventData.position.x,
                y = eventData.position.y,
                z = 0
            };

            // create the in-motion state of this card and set its details
            var instance = Instantiate(draggingStatePrefab, position, draggingStatePrefab.transform.rotation);
            if (instance != null)
            {
                var dragged = instance.GetComponent<RGDraggedCard>();
                if (dragged != null)
                {
                    dragged.draggedCardName = draggableCardName;
                    dragged.iconPrefab.GetComponent<Image>().overrideSprite = icon;
                }

                instance.transform.SetParent(transform.root, false);
                _draggingStateInstance = instance;
            }

            // check if this card is within a drop zone the moment it begins dragging. If so, this card is being
            // reordered or deleted, not added as a new child
            Vector2 mouseScreenSpacePosition = Input.mousePosition;
            var potentialDropZones = GameObject.FindObjectsOfType<RGDropZone>();
            foreach (var dz in potentialDropZones)
            {
                // get the potential cursor position within this drop zone 
                var dropZoneRectTransform = dz.GetComponent<RectTransform>();
                var localMousePos = dropZoneRectTransform.InverseTransformPoint(mouseScreenSpacePosition);

                if (dropZoneRectTransform.rect.Contains(localMousePos))
                {
                    // this card has begun dragging within a drop zone. It is being reordered (or deleted) 
                    var ev = new PointerEventData(EventSystem.current)
                    {
                        pointerDrag = this.gameObject
                    };
                    dz.OnPointerEnter(ev);
                    IsReordering = true;
                    _dropZone = dz;
                    break;
                }
            }
        }

        /**
         * <summary>
         * When this card begins dragging, update the dragging state's position and set the current drop zone
         * </summary>
         * <param name="eventData">Cursor event data</param>
         */
        public void OnDrag(PointerEventData eventData)
        {
            if (_draggingStateInstance != null)
            {
                _draggingStateInstance.transform.position = eventData.position;
            }

            if (_dropZone == null)
            {
                _dropZone = eventData.pointerEnter?.GetComponent<RGDropZone>();
            }
        }

        /**
         * <summary>
         * When this card ends dragging destroy the dragging state instance, and resolve if this card should be added,
         * reordered, or destroyed
         * </summary>
         * <param name="eventData">Cursor event data</param>
         */
        public void OnEndDrag(PointerEventData eventData)
        {
            if (_draggingStateInstance == null)
            {
                return;
            }
            
            // this card is not in a drop zone, and is being reordered, this card is being deleted from its drop zone
            if (_dropZone == null && IsReordering)
            {
                var potentialDropZones = GameObject.FindObjectsOfType<RGDropZone>();
                foreach (var dz in potentialDropZones)
                {
                    if (dz.Contains(this.gameObject))
                    {
                        dz.RemoveChild(this.gameObject);
                        break;
                    }
                }
                
                // destroy the dragging state after a fade out animation 
                _draggingStateInstance.GetComponent<RGDraggedCard>()?.FadeOutAndDestroy();
                return;
            }

            // this card is not in a drop zone. Ignore it
            if (_dropZone == null)
            {
                // destroy the dragging state after a fade out animation 
                _draggingStateInstance.GetComponent<RGDraggedCard>()?.FadeOutAndDestroy();
                return;
            }

            // this card is being reordered. Reset its drop zone's state
            if (IsReordering)
            {
                IsReordering = false;
                _dropZone.ResetState();
                _dropZone = null;
                Destroy(_draggingStateInstance);
                return;
            }

            // this card is being added to its drop zone
            var newCard = Instantiate(restingStatePrefab);
            _dropZone.AddChild(newCard);
            _dropZone = null;
            Destroy(_draggingStateInstance);
        }

        /**
         * <summary>
         * Unset this card's drop zone if dragged outside of it
         * </summary>
         */
        public void OnExitDropZone()
        {
            _dropZone = null;
        }
    }
}