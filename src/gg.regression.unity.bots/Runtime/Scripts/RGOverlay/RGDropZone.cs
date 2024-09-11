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
        
        public GameObject potentialDropSpotPrefab;

        public GameObject emptyStatePrefab;

        private RGSequenceEditor _sequenceEditorScript;
        
        private GameObject _emptyStatePrefabInstance;

        private GameObject _potentialDropSpotInstance;

        private GameObject _currentDraggable;

        private int _currentDropIndex = -1;

        private const int DEFAULT_CHILD_HEIGHT = 30;

        private const int DEFAULT_CHILD_SPACING = 8;

        public void Start()
        {
            if (droppables.Count == 0)
            {
                Debug.LogError("RGDropZone has no droppable types set");
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
         * could possibly be dropped. If the draggable card started over this drop zone, then it is being reordered
         * </summary>
         */
        public void Update()
        {
            if (_currentDraggable == null)
            {
                return;
            }

            // get screenspace location of the mouse cursor, within the bounds of this drop zone
            Vector2 mouseScreenSpacePosition = Input.mousePosition;
            var rectTransform = GetComponent<RectTransform>();
            Vector2 localPoint = rectTransform.InverseTransformPoint(mouseScreenSpacePosition);

            // adjust the local point to have the origin in the top-left corner (Y-axis facing down)
            var resultPosition = new Vector2(
                localPoint.x + rectTransform.rect.size.x * rectTransform.pivot.x,
                -localPoint.y + rectTransform.rect.size.y * rectTransform.pivot.y
            );

            // update the position of the potential drop spot when the drop index changes
            var dropIndex = ComputeDropIndex(resultPosition);
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
                            transform,
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
            Destroy(_emptyStatePrefabInstance);

            // place the new child in the drop index that the potential drop spot is occupying
            var dropIndex = 0;
            if (_potentialDropSpotInstance != null)
            {
                dropIndex = _potentialDropSpotInstance.transform.GetSiblingIndex();
                Destroy(_potentialDropSpotInstance);
            }

            newChild.transform.SetParent(transform, false);
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
            if (transform.childCount - 1 == 0)
            {
                _emptyStatePrefabInstance = Instantiate(emptyStatePrefab, transform, false);
                _sequenceEditorScript.SetCreateSequenceButtonEnabled(false);
            }
            
            childToRemove.transform.parent = null;
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
            var childIDs = new List<int>(transform.childCount);
            for (int i = 0; i < transform.childCount; ++i)
            {
                childIDs.Add(transform.GetChild(i).GetInstanceID());
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
            for (var i = 0; i < transform.childCount; i++)
            {
                var asDraggableCard = transform.GetChild(i).GetComponent<RGDraggableCard>();
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
            var childCount = transform.childCount - 1;
            for (var i = childCount; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            
            if (_emptyStatePrefabInstance == null)
            {
                _emptyStatePrefabInstance = Instantiate(emptyStatePrefab, transform, false);
            }
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
                        return;
                    }
                }
            }
        }

        /**
         * <summary>
         * When the cursor exists the drop zone: reset the state used to track the current draggable, and let the
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

            var draggableCard = _currentDraggable?.GetComponent<RGDraggableCard>();
            if (draggableCard != null)
            {
                draggableCard.OnExitDropZone();
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

            if (transform.childCount == 0)
            {
                // this child is the first
                return 0;
            }

            // get the height of the currently held droppable
            var childHeight = _currentDraggable.GetComponent<RectTransform>()?.rect.height ?? DEFAULT_CHILD_HEIGHT;

            // get the child spacing within this Drop Zone instance
            var childSpacing = GetComponent<VerticalLayoutGroup>()?.spacing ?? DEFAULT_CHILD_SPACING;

            // get the total child height + spacing. Used for detecting the end of the child list
            var totalHeight = (childHeight + childSpacing) * transform.childCount;

            // mouse location is at or below the bottom of list
            if (location.y >= totalHeight)
            {
                return transform.childCount + 1;
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
            if (_currentDropIndex > transform.childCount - 1)
            {
                return;
            }

            for (var i = _currentDropIndex; i > transform.childCount - 1; i++)
            {
                transform.GetChild(i).SetSiblingIndex(i + 1);
            }
        }
    }
}