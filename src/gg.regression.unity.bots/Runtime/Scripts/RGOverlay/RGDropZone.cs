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

    private GameObject currentDroppable;

    private GameObject potentialDropSpot;

    private IList<GameObject> children;

    public void Start()
    {
        children = new List<GameObject>();
    }

    public void Update()
    {
        if (currentDroppable != null)
        {
            Vector2 mouseScreenSpacePosition = Input.mousePosition;
            var rectTransform = this.GetComponent<RectTransform>();

            // get screenspace location of the mouse cursor, within the bounds of this drop zone
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                mouseScreenSpacePosition,
                null, 
                out localPoint
            ); 

            // adjust the local point to have the origin at the top-left corner
            Vector2 resultPosition = new Vector2(
                localPoint.x + rectTransform.rect.size.x * rectTransform.pivot.x, 
                -localPoint.y + rectTransform.rect.size.y * rectTransform.pivot.y
            );

            var dropIndex = ComputeDropIndex(resultPosition);
            if (dropIndex >= 0)
            {
                Debug.Log($"Drop index: {dropIndex}");
                if (potentialDropSpot != null)
                {
                    Destroy(potentialDropSpot);
                }

                if (potentialDropSpotPrefab != null)
                {
                    var dropSpot = Instantiate(potentialDropSpotPrefab);

                    var dropSpotTransform = dropSpot.transform;
                    dropSpot.transform.SetParent(this.transform, false);

                    // Adjust sibling indices to insert the new child at the specified index
                    Transform[] childs = new Transform[children.Count];
                    for (int i = 0; i < children.Count; i++)
                    {
                        childs[i] = this.transform.GetChild(i);
                    }

                    // Shift existing children to the right
                    for (int i = children.Count - 1; i >= dropIndex; i--)
                    {
                        childs[i].SetSiblingIndex(i + 1);
                    }

                    dropSpot.transform.SetSiblingIndex(dropIndex);

                    potentialDropSpot = dropSpot;
                }
            }
        }
    }

    public void AddChild(GameObject newChild)
    {
        if (emptyStatePrefab != null)
        {
            Destroy(emptyStatePrefab);
        }

        if (potentialDropSpot != null)
        {
            Destroy(potentialDropSpot);
        }

        children.Add(newChild);
        newChild.transform.SetParent(this.transform, false);

        currentDroppable = null;
    }

    public void RemoveChild(GameObject childToRemove)
    {
        Destroy(childToRemove);
        children.Remove(childToRemove);

        if (children.Count == 0)
        {
            var emptyState = Instantiate(emptyStatePrefab, new Vector3(), Quaternion.identity);
            emptyState.transform.SetParent(this.transform, false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var target = eventData.pointerDrag;
        if (target != null)
        {
            // Debug.Log("Has a target");
            foreach (GameObject droppable in droppables)
            {
                if (target.GetType() == droppable.GetType())
                {
                    // Debug.Log($"A valid droppable has arrived: {target.name}");
                    currentDroppable = target;
                    return;
                }
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var target = eventData.pointerDrag;
        if (target != null)
        {
            foreach (GameObject droppable in droppables)
            {
                if (target.GetType() == droppable.GetType())
                {
                    // Debug.Log($"A valid droppable has exited: {target.name}");
                    currentDroppable = null;
                    return;
                }
            }

            currentDroppable = null;
        }
    }

    private int ComputeDropIndex(Vector2 location)
    {
        if (currentDroppable == null)
        {
            // somehow we lack something to drop
            return -1;
        }
        
        if (children.Count == 0)
        {
            // Debug.Log("No kids");
            return 0;
        }

        var childHeight = currentDroppable.GetComponent<RectTransform>().rect.height;
        var childSpacing = this.GetComponent<VerticalLayoutGroup>().spacing;
        var totalHeight = (childHeight + childSpacing) * children.Count;

        // at bottom of list
        if (location.y >= totalHeight)
        {
            // Debug.Log("At bottom of list");
            return children.Count + 1;
        }

        for (var i = children.Count; i > 0; --i)
        {
            var height = (childHeight * i) + (childSpacing * i);
            if (location.y >= height)
            {
                // Debug.Log($"At the index: {i}");
                return i;
            }
        }

        return 0;

        // Debug.Log("No case found");
        // return -1;
    }
}