using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture]
public class RGDraggableCardTests
{
    private GameObject _uat;

    private RGDraggableCard card;

    private readonly PointerEventData genericDragEvent = new(EventSystem.current);

    [SetUp]
    public void SetUp()
    {
        // create the card we want to test
        _uat = new GameObject();
        card = _uat.AddComponent<RGDraggableCard>();
        card.transform.SetParent(_uat.transform, false);
        card.gameObject.AddComponent<RectTransform>();
        card.payload = new Dictionary<string, string>();
        card.draggableCardName = "Card Name";
        card.draggableCardDescription = "Card Description";

        // add placeholder text fields to the card
        var text = RGTestUtils.CreateTMProPlaceholder();
        card.namePrefab = text;
        card.descriptionPrefab = text;

        // ensure public prefabs are mocked
        card.restingStatePrefab = new GameObject();
        card.restingStatePrefab.AddComponent<RGDraggableCard>();
        card.draggingStatePrefab = new GameObject();
        card.icon = RGTestUtils.CreateSpritePlaceholder();
        card.iconPrefab = new GameObject();
        card.iconPrefab.AddComponent<Image>();
        card.Start();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_uat);
    }

    [Test]
    public void OnBeginDrag()
    {
        var dragEvent = new PointerEventData(EventSystem.current);
        card.OnBeginDrag(dragEvent);

        // ensure the card shows its dragging state
        var draggedCard = Object.FindObjectOfType<RGDraggableCard>();
        Assert.NotNull(draggedCard);
    }

    [Test]
    public void OnEndDrag_AddCardToDropZone()
    {
        var dropZone = CreateNewDropZone();
        var dropZoneScript = dropZone.GetComponent<RGDropZone>();
        
        card.OnBeginDrag(genericDragEvent);

        // place the card over the potential drop zone
        var onDragEvent = new PointerEventData(EventSystem.current)
        {
            pointerEnter = dropZone
        };
        card.OnDrag(onDragEvent);

        // drop the card into the drop zone. It should be added as a new child
        card.OnEndDrag(genericDragEvent);
        
        Assert.IsNotEmpty(dropZoneScript.GetChildren());
    }

    [Test]
    public void OnEndDrag_ReorderCard()
    {
        var dropZone = CreateNewDropZone();
        var dropZoneScript = dropZone.GetComponent<RGDropZone>();
        
        // the card begins already inside the drop zone
        card.IsReordering = true;
        card.transform.SetParent(dropZone.transform, false);
        
        card.OnBeginDrag(genericDragEvent);

        // drag and drop the card at another location within the drop zone
        var onDragEvent = new PointerEventData(EventSystem.current) { pointerEnter = dropZone.gameObject };
        card.OnDrag(onDragEvent);

        card.OnEndDrag(genericDragEvent);

        // the card should complete its reordering
        Assert.IsFalse(card.IsReordering);
    }

    [Test]
    public void OnEndDrag_DestroyAddedCard()
    {
        var dropZone = CreateNewDropZone();
        var dropZoneScript = dropZone.GetComponent<RGDropZone>();

        // the card begins already inside the drop zone
        card.IsReordering = true;
        card.transform.SetParent(dropZone.transform, false);

        card.OnBeginDrag(genericDragEvent);

        // drag and drop the card outside the drop zone
        var onDragEvent = new PointerEventData(EventSystem.current) { pointerEnter = null };
        card.OnDrag(onDragEvent);

        card.OnEndDrag(genericDragEvent);

        // the drop zone should have no children. Its only one was removed
        Assert.IsEmpty(dropZoneScript.GetChildren());
    }

    /**
     * <summary>
     * Create and initialize a new drop zone that accepts any Game Object as a child
     * </summary>
     */
    private GameObject CreateNewDropZone()
    {
        var sequenceEditor = new GameObject();
        var sequenceEditorScript = sequenceEditor.AddComponent<RGSequenceEditor>();
        var dzTextObject = new GameObject();
        var dzText = dzTextObject.AddComponent<TMP_InputField>();
        sequenceEditorScript.NameInput = dzText;
        
        var dropZone = new GameObject();
        dropZone.gameObject.AddComponent<RectTransform>();
        var dzScript = dropZone.AddComponent<RGDropZone>();
        dzScript.potentialDropSpotPrefab = new GameObject();
        dzScript.emptyStatePrefab = new GameObject();
        dzScript.SequenceEditor = sequenceEditor;
        dzScript.droppables = new List<GameObject>() { card.gameObject };
        dzScript.Start();
        return dropZone;
    }
}