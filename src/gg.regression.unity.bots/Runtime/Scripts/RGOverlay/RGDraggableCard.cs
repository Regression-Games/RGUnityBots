using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * A component that can be dragged and dropped, and also reordered, within an RGDropZone instance. A draggable card's
     * `payload` field is used to transfer information from its dragging state to its resting state.
     * </summary>
     */
    public class RGDraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Dictionary<string, string> payload;

        public bool IsReordering;

        public string draggableCardName;

        public string draggableCardResourcePath;

        public string draggableCardDescription;

        public Sprite icon;

        public GameObject iconPrefab;

        public TMP_Text namePrefab;

        public TMP_Text resourcePathPrefab;

        public TMP_Text descriptionPrefab;

        public GameObject restingStatePrefab;

        public GameObject draggingStatePrefab;

        private GameObject _draggingStateInstance;

        private RGDropZone _dropZone;

        private bool _isHighlighted;

        // the card's alpha value when inert, or being dragged
        private const float HIGHLIGHTED_ALPHA = 1.0f;

        // the card's alpha value when another card is being manipulated
        private const float MUTED_ALPHA = 0.2f;

        private const int EXPANDED_HEIGHT = 80;

        private float originalHeight = 40;

        public void Start()
        {
            IsReordering = false;

            _isHighlighted = false;

            // if we have a name set
            if (!string.IsNullOrEmpty(draggableCardName))
            {
                if (namePrefab != null)
                {
                    namePrefab.text = draggableCardName;
                }

                if (resourcePathPrefab != null)
                {
                    resourcePathPrefab.text = draggableCardResourcePath;
                    resourcePathPrefab.gameObject.SetActive(true);
                }
            }
            else
            {
                // use the resource path as the name and hide the hint path
                namePrefab.text = draggableCardResourcePath;
                if (resourcePathPrefab != null)
                {
                    resourcePathPrefab.text = "";
                    resourcePathPrefab.gameObject.SetActive(false);
                }
            }

            if (descriptionPrefab != null)
            {
                descriptionPrefab.text = draggableCardDescription;
            }

            if (iconPrefab != null)
            {
                iconPrefab.GetComponent<Image>().overrideSprite = icon;
            }

            var rect = GetComponent<RectTransform>();
            var size = rect.sizeDelta;
            originalHeight = size.y;
        }

        /**
         * <summary>
         * If this draggable card is over a valid Drop Zone
         * </summary>
         */
        public bool IsOverDropZone()
        {
            return _dropZone != null;
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
                    dragged.payload = payload;
                    dragged.draggedCardName = draggableCardName;
                    dragged.iconPrefab.GetComponent<Image>().overrideSprite = icon;
                }

                instance.transform.SetParent(transform.root, false);
                _draggingStateInstance = instance;

                // darken the cards not being dragged
                ToggleHighlight();
                ToggleExpand(true);
            }

            // check if this card is within a drop zone the moment it begins dragging. If so, this card is being
            // reordered or deleted, not added as a new child
            Vector2 mouseScreenSpacePosition = Input.mousePosition;
            var potentialDropZones = GameObject.FindObjectsOfType<RGDropZone>();
            foreach (var dz in potentialDropZones)
            {
                // get the potential cursor position within this drop zone
                var dropZoneRectTransform = dz.Content.GetComponent<RectTransform>();
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
                    SetDropZone(dz);
                    break;
                }
            }
        }

        /**
         * <summary>
         * When this card begins dragging, show the dragging state and set its position
         * </summary>
         * <param name="eventData">Cursor event data</param>
         */
        public void OnDrag(PointerEventData eventData)
        {
            if (_draggingStateInstance != null)
            {
                _draggingStateInstance.transform.position = eventData.position;
            }
        }

        /**
         * <summary>
         * Sets the Drop Zone for this card
         * </summary>
         * <param name="dropZone">The Drop Zone this card is within, or null if not</param>
         */
        public void SetDropZone(RGDropZone dropZone)
        {
            _dropZone = dropZone;
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
            ToggleHighlight();

            if (_draggingStateInstance == null)
            {
                // this card is not dragging currently
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
                _dropZone.ResetDraggableTracking();
                SetDropZone(null);
                Destroy(_draggingStateInstance);
                return;
            }

            // this card is being added to its drop zone
            var newCard = Instantiate(restingStatePrefab);
            newCard.GetComponent<RGDraggableCard>().payload = payload;
            _dropZone.AddChild(newCard);
            SetDropZone(null);
            Destroy(_draggingStateInstance);
        }

        /**
         * <summary>
         * Expand this card when clicked on. This will show the card's description
         * </summary>
         */
        public void OnClick()
        {
            ToggleExpand();
        }

        /**
         * <summary>
         * Expand the card to show its description and full name, or shrink the card if already expanded
         * </summary>
         * <param name="forceShrink">Should this card shrink, despite if it is expanded or not</param>
         */
        private void ToggleExpand(bool forceShrink = false)
        {
            if (string.IsNullOrEmpty(descriptionPrefab.text) && !namePrefab.isTextOverflowing && !resourcePathPrefab.isTextOverflowing)
            {
                // don't expand cards without a description or overtly long name
                return;
            }

            var isActive = descriptionPrefab.gameObject.activeSelf || forceShrink;
            var newHeight = isActive ? originalHeight : EXPANDED_HEIGHT;

            // show full card name when expanded
            var newOverflow = isActive ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            namePrefab.overflowMode = newOverflow;

            // show full path with expanded
            resourcePathPrefab.overflowMode = newOverflow;

            descriptionPrefab.gameObject.SetActive(!isActive);

            var rect = GetComponent<RectTransform>();
            var size = rect.sizeDelta;
            size.y = newHeight;
            rect.sizeDelta = size;
        }

        /**
         * <summary>
         * Toggle the darkening state of any sibling cards when the current card is in use
         * </summary>
         */
        private void ToggleHighlight()
        {
            if (transform.parent == null)
            {
                // this card will have no siblings to toggle the highlight state for
                return;
            }

            var newAlpha = _isHighlighted ? HIGHLIGHTED_ALPHA : MUTED_ALPHA;

            // if the selfIndex is -1, we can know that all cards are being returned to their regular alpha value
            var selfIndex = _isHighlighted ?  -1 : transform.GetSiblingIndex();

            for (var i = 0; i < transform.parent.transform.childCount; ++i)
            {
                var child = transform.parent.transform.GetChild(i).GetComponent<RGDraggableCard>();
                if (child != null && i != selfIndex)
                {
                    child.namePrefab.CrossFadeAlpha(newAlpha, 0.1f, false);
                    child.resourcePathPrefab.CrossFadeAlpha(newAlpha, 0.1f, false);
                    child.descriptionPrefab.CrossFadeAlpha(newAlpha, 0.1f, false);
                    child.iconPrefab.GetComponent<Image>().color = new Color(1.0f, 1.0f, 1.0f, newAlpha);
                }
            }

            _isHighlighted = !_isHighlighted;
        }
    }
}
