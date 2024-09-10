using System;
using System.Transactions;
using NUnit.Framework;
using RegressionGames;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

[TestFixture]
public class RGDraggableCardTests
{
    private GameObject _uat;
    
    private RGDraggableCard card;
    
    private PointerEventData genericDragEvent = new(EventSystem.current);

    [SetUp]
    public void SetUp()
    {
        _uat = new GameObject();
        card = _uat.AddComponent<RGDraggableCard>();
        card.draggableCardName = "Card Name";
        var textObject = new GameObject();
        card.namePrefab = textObject.AddComponent<TextMeshProUGUI>();
        card.restingStatePrefab = new GameObject();
        card.draggingStatePrefab = new GameObject();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(card.namePrefab);
        Object.Destroy(card.restingStatePrefab);
        Object.Destroy(card.draggingStatePrefab);
        Object.Destroy(card);
        Object.Destroy(_uat);
    }

    [Test]
    public void OnBeginDrag()
    {
        var dragEvent = new PointerEventData(EventSystem.current);
        card.OnBeginDrag(dragEvent);

        var draggedCard = Object.FindObjectOfType<RGDraggableCard>();
        Assert.NotNull(draggedCard);
    }
    
    [Test]
    public void OnEndDrag_AddCardToDropZone()
    {
        card.OnBeginDrag(genericDragEvent);

        var dropZone = new GameObject();
        dropZone.AddComponent<RGDropZone>();
        dropZone.AddComponent<RectTransform>();
        
        var onDragEvent = new PointerEventData(EventSystem.current) { pointerEnter = dropZone };
        card.OnDrag(onDragEvent);

        card.OnEndDrag(genericDragEvent);
        
        Assert.AreEqual(dropZone.transform.childCount, 1);
    }
    
    [Test]
    public void OnEndDrag_ReorderCard()
    {
        card.IsReordering = true;
        
        card.OnBeginDrag(genericDragEvent);

        var dropZone = new GameObject();
        dropZone.AddComponent<RGDropZone>();
        dropZone.AddComponent<RectTransform>();
        
        var onDragEvent = new PointerEventData(EventSystem.current) { pointerEnter = dropZone };
        card.OnDrag(onDragEvent);

        card.OnEndDrag(genericDragEvent);
        
        Assert.IsFalse(card.IsReordering);
    }
    
    [Test]
    public void OnEndDrag_DestroyAddedCard()
    {
        
        var dropZone = new GameObject();
        dropZone.AddComponent<RGDropZone>();
        dropZone.AddComponent<RectTransform>();
        var dropZoneScript = dropZone.GetComponent<RGDropZone>();
        
        card.IsReordering = true;
        card.transform.SetParent(dropZone.transform, false);
        
        Assert.IsTrue(dropZoneScript.Contains(card.gameObject));
        
        card.OnBeginDrag(genericDragEvent);

        var onDragEvent = new PointerEventData(EventSystem.current) { pointerEnter = null };
        card.OnDrag(onDragEvent);

        card.OnEndDrag(genericDragEvent);
        
        var isCardContained = dropZoneScript.Contains(card.gameObject);
        Assert.IsFalse(dropZoneScript.Contains(card.gameObject));
    }
}