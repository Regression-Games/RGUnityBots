using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames
{
    public class RGDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public List<GameObject> droppables;

        public GameObject potentialDropSpotPrefab;

        public GameObject emptyStatePrefab;

        private GameObject _emptyStatePrefabInstance;

        private GameObject _potentialDropSpotInstance;

        private GameObject _currentDroppable;

        private int _currentDropIndex;

        private const int DEFAULT_CHILD_HEIGHT = 30;

        private const int DEFAULT_CHILD_SPACING = 8;

        public void Start()
        {
            _currentDropIndex = -1;

            if (emptyStatePrefab != null)
            {
                _emptyStatePrefabInstance = Instantiate(emptyStatePrefab, this.transform, false);
            }
        }

        public void Update()
        {
            if (_currentDroppable == null)
            {
                return;
            }

            // get screenspace location of the mouse cursor, within the bounds of this drop zone
            Vector2 mouseScreenSpacePosition = Input.mousePosition;
            var rectTransform = this.GetComponent<RectTransform>();
            Vector2 localPoint = rectTransform.InverseTransformPoint(mouseScreenSpacePosition);

            // adjust the local point to have the origin in the top-left corner
            var resultPosition = new Vector2(
                localPoint.x + rectTransform.rect.size.x * rectTransform.pivot.x,
                -localPoint.y + rectTransform.rect.size.y * rectTransform.pivot.y
            );

            // update the position of the potential drop spot when the drop index changes
            var dropIndex = ComputeDropIndex(resultPosition);
            if (dropIndex >= 0 && dropIndex != _currentDropIndex)
            {
                _currentDropIndex = dropIndex;

                // create the drop spot indicator if it does not exist, and shift existing children down to make room
                var draggable = _currentDroppable.GetComponent<RGDraggableCard>();
                if (draggable == null)
                {
                    return;
                }

                if (draggable.IsReordering)
                {
                    if (_currentDropIndex != _currentDroppable.transform.GetSiblingIndex())
                    {
                        _currentDroppable.transform.SetSiblingIndex(_currentDropIndex);
                        ShiftChildrenForCurrentDrop();
                    }
                }
                else
                {
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

        public void AddChild(GameObject newChild)
        {
            if (_emptyStatePrefabInstance != null)
            {
                Destroy(_emptyStatePrefabInstance);
            }

            var dropIndex = 0;
            if (_potentialDropSpotInstance != null)
            {
                dropIndex = _potentialDropSpotInstance.transform.GetSiblingIndex();
                Destroy(_potentialDropSpotInstance);
            }

            newChild.transform.SetParent(transform, false);
            newChild.transform.SetSiblingIndex(dropIndex);

            _currentDroppable = null;
            _currentDropIndex = -1;
        }

        public void RemoveChild(GameObject childToRemove)
        {
            if (this.transform.childCount - 1 == 0)
            {
                _emptyStatePrefabInstance = Instantiate(emptyStatePrefab, this.transform, false);
            }

            Destroy(childToRemove);
        }

        public bool Contains(GameObject possibleChild)
        {
            var childIDs = new List<int>(this.transform.childCount);
            for (int i = 0; i < this.transform.childCount; ++i)
            {
                childIDs.Add(this.transform.GetChild(i).GetInstanceID());
            }

            return childIDs.Contains(possibleChild.transform.GetInstanceID());
        }

        public void CompleteReordering()
        {
            _currentDroppable = null;
            _currentDropIndex = -1;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var target = eventData.pointerDrag;
            if (target != null)
            {
                foreach (GameObject droppable in droppables)
                {
                    if (target.GetType() == droppable.GetType())
                    {
                        _currentDroppable = target;
                        return;
                    }
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Destroy(_potentialDropSpotInstance);

            var draggableCard = _currentDroppable?.GetComponent<RGDraggableCard>();
            if (draggableCard != null)
            {
                draggableCard.OnExitDropZone();
            }

            _currentDroppable = null;
            _currentDropIndex = -1;
        }

        private int ComputeDropIndex(Vector2 location)
        {
            if (_currentDroppable == null)
            {
                // we lack something to drop
                return -1;
            }

            if (this.transform.childCount == 0)
            {
                return 0;
            }

            // get the height of the currently held droppable
            var childHeight = _currentDroppable.GetComponent<RectTransform>()?.rect.height ?? DEFAULT_CHILD_HEIGHT;

            // get the child spacing within this Drop Zone instance
            var childSpacing = this.GetComponent<VerticalLayoutGroup>()?.spacing ?? DEFAULT_CHILD_SPACING;

            var totalHeight = (childHeight + childSpacing) * this.transform.childCount;

            // mouse location is at the bottom of list
            if (location.y >= totalHeight)
            {
                return this.transform.childCount + 1;
            }

            return (int)Math.Round(location.y / (childHeight + childSpacing));
        }

        private void ShiftChildrenForCurrentDrop()
        {
            if (_currentDropIndex > this.transform.childCount - 1)
            {
                return;
            }

            var childTransforms = new Transform[this.transform.childCount];
            for (var i = 0; i < this.transform.childCount; i++)
            {
                childTransforms[i] = this.transform.GetChild(i);
            }

            for (var i = _currentDropIndex; i > this.transform.childCount - 1; i++)
            {
                childTransforms[i].SetSiblingIndex(i + 1);
            }
        }
    }
}