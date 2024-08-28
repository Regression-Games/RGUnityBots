using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RGDropZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public List<GameObject> droppables; 

    public int childHeight;

    public int childSpacing;

    public GameObject potentialDropSpotPrefab;

    public GameObject emptyStatePrefab;

    private GameObject potentialDropSpotInstance;

    private GameObject _currentDroppable;

    private int _currentDropIndex;

    private const int DEFAULT_CHILD_HEIGHT = 30;

    private const int DEFAULT_CHILD_SPACING = 8;

    public void Start()
    {
        _currentDropIndex = -1;
    }

    public void Update()
    {
        if (_currentDroppable != null)
        {
            Vector2 mouseScreenSpacePosition = Input.mousePosition;
            var rectTransform = GetComponent<RectTransform>();

            // get screenspace location of the mouse cursor, within the bounds of this drop zone
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                mouseScreenSpacePosition,
                null, 
                out localPoint
            ); 

            // adjust the local point to have the origin in the top-left corner
            Vector2 resultPosition = new Vector2(
                localPoint.x + rectTransform.rect.size.x * rectTransform.pivot.x, 
                -localPoint.y + rectTransform.rect.size.y * rectTransform.pivot.y
            );

            // update the position of the potential drop spot when the drop index changes
            var dropIndex = ComputeDropIndex(resultPosition);
            if (dropIndex >= 0 && dropIndex != _currentDropIndex)
            {
                _currentDropIndex = dropIndex;

                // create the drop spot indicator if it does not exist, and shift all of the
                // existing children down to make room
                // if (potentialDropSpotInstance == null)
                // {
                    var draggable = _currentDroppable.GetComponent<RGDraggableCard>();
                    if (draggable != null && draggable.IsReordering)
                    {
                        ShiftChildrenForReorder(_currentDroppable.transform.GetSiblingIndex());
                    }
                    else
                    {
                        if (potentialDropSpotInstance == null)
                        {
                            potentialDropSpotInstance = Instantiate(
                                potentialDropSpotPrefab,
                                transform,
                                false);
                        }

                        potentialDropSpotInstance.transform.SetSiblingIndex(_currentDropIndex);
                        
                        ShiftChildrenForNewChild();
                        
                    }
                // }
            }
        }
    }

    public void FinishReordering()
    {
        _currentDroppable = null;
        _currentDropIndex = -1;
    }

    public void AddChild(GameObject newChild)
    {
        if (emptyStatePrefab != null)
        {
            Destroy(emptyStatePrefab);
        }

        var dropIndex = 0;
        if (potentialDropSpotInstance != null)
        {
            dropIndex = potentialDropSpotInstance.transform.GetSiblingIndex();
            Destroy(potentialDropSpotInstance);
        }
        
        newChild.transform.SetParent(transform, false);
        newChild.transform.SetSiblingIndex(dropIndex);
        _currentDroppable = null;
    }

    public void RemoveChild(GameObject childToRemove)
    {
        Destroy(childToRemove);
        
        if (transform.childCount == 0)
        {
            var emptyState = Instantiate(emptyStatePrefab, new Vector3(), Quaternion.identity);
            emptyState.transform.SetParent(transform, false);
        }
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
        var draggableCard = _currentDroppable?.GetComponent<RGDraggableCard>();
        if (draggableCard != null)
        {
            draggableCard.OnExitDropZone();
        }
        
        if (potentialDropSpotInstance != null)
        {
            Destroy(potentialDropSpotInstance);
        }
        
        _currentDroppable = null;
        _currentDropIndex = -1;
    }

    private int ComputeDropIndex(Vector2 location)
    {
        if (_currentDroppable == null)
        {
            // somehow we lack something to drop
            return -1;
        }
        
        if (transform.childCount == 0)
        {
            return 0;
        }

        // get the height of the currently held droppable
        var childHeight = _currentDroppable.GetComponent<RectTransform>()?.rect.height ?? DEFAULT_CHILD_HEIGHT;
        
        // get the child spacing within this Drop Zone instance
        var childSpacing = GetComponent<VerticalLayoutGroup>()?.spacing ?? DEFAULT_CHILD_SPACING;

        var totalHeight = (childHeight + childSpacing) * transform.childCount;

        // mouse is beyond the bottom of list
        if (location.y >= totalHeight)
        {
            return transform.childCount + 1;
        }

        return (int)Math.Round(location.y / (childHeight + childSpacing));
    }

    private void ShiftChildrenForReorder(int OriginalIndex)
    {
        if (_currentDropIndex == OriginalIndex)
        {
            return;
        }
        
        _currentDroppable.transform.SetSiblingIndex(_currentDropIndex);
        
        // Shift existing children down
        var childTransforms = new Transform[transform.childCount];
        for (var i = 0; i < transform.childCount; i++)
        {
            childTransforms[i] = transform.GetChild(i);
        }
        
        for (var i = _currentDropIndex; i > transform.childCount - 1; i++)
        {
            childTransforms[i].SetSiblingIndex(i + 1);
        }
    }

    private void ShiftChildrenForNewChild()
    {
        if (_currentDropIndex > transform.childCount - 1)
        {
            return;
        }
        
        // Adjust sibling indices to insert the new child at the specified index
        var childTransforms = new Transform[transform.childCount];
        for (var i = 0; i < transform.childCount; i++)
        {
            childTransforms[i] = transform.GetChild(i);
        }
        
        for (var i = _currentDropIndex; i > transform.childCount - 1; i++)
        {
            childTransforms[i].SetSiblingIndex(i + 1);
        }
    }
}