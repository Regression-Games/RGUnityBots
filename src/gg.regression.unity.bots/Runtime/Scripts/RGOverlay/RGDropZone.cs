using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * A vertical layout that can have RGDraggableCards dropped into it, and also reordered
     * </summary>
     */
    public class RGDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public List<GameObject> droppables;

        public GameObject SequenceEditor;

        public GameObject Content;

        public GameObject ScrollView;

        public GameObject potentialDropSpotPrefab;

        public GameObject emptyStatePrefab;

        private RGSequenceEditor _sequenceEditorScript;

        private GameObject _emptyStatePrefabInstance;

        private GameObject _potentialDropSpotInstance;

        private GameObject _currentDraggable;

        private int _currentDropIndex = -1;

        // if not specified, default to this child height
        private const int DEFAULT_CHILD_HEIGHT = 40;

        // if not specified, default to this spacing between children
        private const int DEFAULT_CHILD_SPACING = 8;

        // the pixel distance from the bottom and top of the ScrollView's scrollable area where auto-scrolling should trigger
        private const int EDGE_SCROLL_BUFFER = 100;

        // the pixel speed that the auto-scrolling should occur
        private const float EDGE_SCROLL_SPEED = 300f;

        public void Start()
        {
            if (droppables == null || droppables.Count == 0)
            {
                Debug.LogError("RGDropZone has no droppable types set");
            }

            if (Content == null)
            {
                Debug.LogError("RGDropZone has no Content set");
            }

            if (ScrollView == null)
            {
                Debug.LogError("RGDropZone has no ScrollView set");
            }

            if (SequenceEditor == null)
            {
                Debug.LogError("RGDropZone has no SequenceEditor set");
            }
            else
            {
                _sequenceEditorScript = SequenceEditor.GetComponent<RGSequenceEditor>();
            }

            if (potentialDropSpotPrefab == null)
            {
                Debug.LogError("RGDropZone has no potentialDropSpotPrefab set");
            }

            if (emptyStatePrefab == null)
            {
                Debug.LogError("RGDropZone has no emptyStatePrefab set");
            }
        }

        /**
         * <summary>
         * When a draggable card is over this drop zone, we will update the potential location that the draggable card
         * could possibly be dropped. If the draggable card started over this drop zone, then it is being reordered. The
         * scrollable drop zone content container will scroll if the user is dragging a card near the bottom or top of it.
         * </summary>
         */
        public void Update()
        {
            if (_currentDraggable == null)
            {
                return;
            }

            // get screen space location of the mouse cursor
            Vector2 mouseScreenSpacePosition = Input.mousePosition;

            // scroll the drop zone up or down when dragging a card near the top or bottom of the drop zone. This
            // will only apply when the drop zone's scroll bar is visible (ie: when the drop zone children overflow)
            var scrollRect = ScrollView.GetComponent<ScrollRect>();
            if (scrollRect.content.rect.height > scrollRect.viewport.rect.height)
            {
                // get the cursor position within the scrollable area
                Vector2 localPoint = scrollRect.viewport.InverseTransformPoint(mouseScreenSpacePosition);

                // adjust the local point to have the Y-axis facing down (ie: y values increase as we go down)
                var resultPosition = new Vector2(localPoint.x, -localPoint.y);

                // used to modify the scrolling speed based on the height of the scrollable area
                var maxScrollDistance = scrollRect.content.GetComponent<RectTransform>().rect.height -
                                        scrollRect.viewport.GetComponent<RectTransform>().rect.height;

                // calc the cursor distance from the bottom of the non-scrollable, viewport area
                var distanceToBottom = scrollRect.viewport.rect.height - resultPosition.y;

                // compute the scrolling speed
                var scrollAmount = Math.Clamp((Time.deltaTime * EDGE_SCROLL_SPEED) / maxScrollDistance, 0, 1);

                // scroll the scrollable area up or down when the cursor is both dragging a card, and near the top or bottom
                // of the scrollable area
                if (resultPosition.y < EDGE_SCROLL_BUFFER)
                {
                    // scroll up
                    scrollRect.verticalNormalizedPosition += scrollAmount;
                }
                else if (distanceToBottom < EDGE_SCROLL_BUFFER)
                {
                    // scroll down
                    scrollRect.verticalNormalizedPosition -= scrollAmount;
                }
            }

            // get the mouse position within the actual content, used for calculating the drop spot of a new or reordering card
            var contentPoint = Content.GetComponent<RectTransform>().InverseTransformPoint(mouseScreenSpacePosition);
            var resultContentPosition = new Vector2(contentPoint.x, -contentPoint.y);

            // update the position of the potential drop spot when the drop index changes
            var dropIndex = ComputeDropIndex(resultContentPosition);
            if (dropIndex >= 0 && dropIndex != _currentDropIndex)
            {
                _currentDropIndex = dropIndex;

                var draggable = _currentDraggable.GetComponent<RGDraggableCard>();
                if (draggable == null)
                {
                    return;
                }

                if (draggable.IsReordering)
                {
                    // only perform reordering if the current draggable has moved from its current index
                    if (_currentDropIndex != _currentDraggable.transform.GetSiblingIndex())
                    {
                        _currentDraggable.transform.SetSiblingIndex(_currentDropIndex);
                        ShiftChildrenForCurrentDrop();
                    }
                }
                else
                {
                    // create and place the potential drop spot if needed
                    if (_potentialDropSpotInstance == null)
                    {
                        _potentialDropSpotInstance = Instantiate(
                            potentialDropSpotPrefab,
                            Content.transform,
                            false);
                    }

                    _potentialDropSpotInstance.transform.SetSiblingIndex(_currentDropIndex);

                    ShiftChildrenForCurrentDrop();
                }
            }
        }

        /**
         * <summary>
         * Reset the state used to track the current draggable
         * </summary>
         */
        public void ResetDraggableTracking()
        {
            _currentDraggable = null;
            _currentDropIndex = -1;
        }

        /**
         * <summary>
         * Add a child at the potential drop location, and hide the empty state if it is showing
         * </summary>
         * <param name="newChild">The new child object to add</param>
         */
        public void AddChild(GameObject newChild)
        {
            SetEmptyState(false);

            // place the new child in the drop index that the potential drop spot is occupying
            var dropIndex = 0;
            if (_potentialDropSpotInstance != null)
            {
                dropIndex = _potentialDropSpotInstance.transform.GetSiblingIndex();
                Destroy(_potentialDropSpotInstance);
            }

            newChild.transform.SetParent(Content.transform, false);
            newChild.transform.SetSiblingIndex(dropIndex);

            _sequenceEditorScript.SetCreateSequenceButtonEnabled(_sequenceEditorScript.NameInput.text.Length > 0);

            ResetDraggableTracking();
        }

        /**
         * <summary>
         * Remove a child from the drop zone. Show the empty state if removing the child results in 0 children
         * </summary>
         * <param name="childToRemove">The child object to remove</param>
         */
        public void RemoveChild(GameObject childToRemove)
        {
            // perform this check before destroying the child, as the childCount won't update until the next frame
            if (Content.transform.childCount - 1 == 0)
            {
                SetEmptyState(true);
                _sequenceEditorScript.SetCreateSequenceButtonEnabled(false);
            }

            childToRemove.transform.SetParent(null);
            Destroy(childToRemove);
        }

        /**
         * <summary>
         * Check if the drop zone contains a specific child object, comparing using the possible child's instance ID
         * </summary>
         * <param name="possibleChild">The possible child object</param>
         */
        public bool Contains(GameObject possibleChild)
        {
            var childIDs = new List<int>(Content.transform.childCount);
            for (int i = 0; i < Content.transform.childCount; ++i)
            {
                childIDs.Add(Content.transform.GetChild(i).GetInstanceID());
            }

            return childIDs.Contains(possibleChild.transform.GetInstanceID());
        }

        /**
         * <summary>
         * If the empty state prefab is showing, we can assume that this drop zone is empty
         * </summary>
         */
        public bool IsEmpty()
        {
            return _emptyStatePrefabInstance != null;
        }

        /**
         * <summary>
         * Show or hide the empty state assigned to this drop zone
         * </summary>
         * <param name="setEmpty">If the empty state should be shown or hidden</param>
         */
        public void SetEmptyState(bool setEmpty)
        {
            if (setEmpty && _emptyStatePrefabInstance == null)
            {
                _emptyStatePrefabInstance = Instantiate(emptyStatePrefab, Content.transform, false);
            }

            if (!setEmpty)
            {
                Destroy(_emptyStatePrefabInstance);
                _emptyStatePrefabInstance = null;
            }
        }

        /**
         * <summary>
         * If a valid member of the `droppables` field is currently being dragged within this drop zone
         * </summary>
         */
        public bool HasValidDroppable()
        {
            return _currentDraggable != null;
        }

        /**
         * <summary>
         * Gets all dropzone children that are draggable cards (ie: not the empty state)
         * </summary>
         */
        public List<RGDraggableCard> GetChildren()
        {
            var children = new List<RGDraggableCard>();
            for (var i = 0; i < Content.transform.childCount; i++)
            {
                var asDraggableCard = Content.transform.GetChild(i).GetComponent<RGDraggableCard>();
                if (asDraggableCard != null)
                {
                    children.Add(asDraggableCard);
                }
            }

            return children;
        }

        /**
         * <summary>
         * Destroy all the children added to this drop zone, then show the empty state prefab
         * </summary>
         */
        public void ClearChildren()
        {
            var childCount = Content.transform.childCount - 1;
            for (var i = childCount; i >= 0; i--)
            {
                Destroy(Content.transform.GetChild(i).gameObject);
            }

            SetEmptyState(true);
        }

        /**
         * <summary>
         * When the cursor enters the drop zone, check if it is of a type that is allowed to be dropped in the drop zone
         * </summary>
         * <param name="eventData">Cursor event data</param>
         */
        public void OnPointerEnter(PointerEventData eventData)
        {
            var target = eventData.pointerDrag;
            if (target != null)
            {
                foreach (GameObject droppable in droppables)
                {
                    if (target.GetType() == droppable.GetType())
                    {
                        _currentDraggable = target;
                        if (_currentDraggable != null)
                        {
                            var draggableCard = _currentDraggable.GetComponent<RGDraggableCard>();
                            if (draggableCard != null)
                            {
                                draggableCard.SetDropZone(this);
                            }
                        }
                        return;
                    }
                }
            }
        }

        /**
         * <summary>
         * When the cursor exits the drop zone: reset the state used to track the current draggable, and let the
         * current draggable update its internal state
         * </summary>
         * <param name="eventData">Cursor event data</param>
         */
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_potentialDropSpotInstance != null)
            {
                Destroy(_potentialDropSpotInstance);
            }

            if (_currentDraggable != null)
            {
                var draggableCard = _currentDraggable.GetComponent<RGDraggableCard>();
                if (draggableCard != null)
                {
                    draggableCard.SetDropZone(null);
                }
            }

            ResetDraggableTracking();
        }

        /**
         * <summary>
         * Compute a child index within this drop zone where a draggable could possibly be dropped
         * </summary>
         * <param name="location">
         * The cursor position within this drop zone (origin is top-left corner with the Y-axis facing down)
         * </param>
         */
        private int ComputeDropIndex(Vector2 location)
        {
            if (_currentDraggable == null)
            {
                // we lack something to drop
                return -1;
            }

            if (Content.transform.childCount == 0)
            {
                // this child is the first
                return 0;
            }

            // get the height of the currently held droppable
            var childHeight = _currentDraggable.GetComponent<RectTransform>()?.rect.height ?? DEFAULT_CHILD_HEIGHT;

            // get the child spacing within this Drop Zone instance
            var childSpacing = Content.GetComponent<VerticalLayoutGroup>()?.spacing ?? DEFAULT_CHILD_SPACING;

            // get the total child height + spacing. Used for detecting the end of the child list
            var totalHeight = (childHeight + childSpacing) * Content.transform.childCount;

            // mouse location is at or below the bottom of list
            if (location.y >= totalHeight)
            {
                return Content.transform.childCount + 1;
            }

            return (int)Math.Round(location.y / (childHeight + childSpacing));
        }

        /**
         * <summary>
         * If a child of this drop zone has been reordered or newly added, we can shift the children after the current
         * drop index to make space
         * </summary>
         */
        private void ShiftChildrenForCurrentDrop()
        {
            // no need to shift children if the current drop index is at the bottom of the child list
            if (_currentDropIndex > Content.transform.childCount - 1)
            {
                return;
            }

            for (var i = _currentDropIndex; i > Content.transform.childCount - 1; i++)
            {
                Content.transform.GetChild(i).SetSiblingIndex(i + 1);
            }
        }
    }
}
