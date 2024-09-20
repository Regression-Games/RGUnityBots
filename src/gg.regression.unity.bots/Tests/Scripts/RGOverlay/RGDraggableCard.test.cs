using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.TestFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
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
            var draggedCard = card.draggingStatePrefab.AddComponent<RGDraggedCard>();
            draggedCard.iconPrefab = new GameObject();
            draggedCard.iconPrefab.AddComponent<Image>();
            draggedCard.namePrefab = RGTestUtils.CreateTMProPlaceholder();
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
            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);
            var dropZoneScript = dropZone.GetComponent<RGDropZone>();

            // assume the card begins outside the drop zone
            card.IsReordering = false;

            card.OnBeginDrag(genericDragEvent);

            // place the card over the potential drop zone
            var onDragEvent = new PointerEventData(EventSystem.current)
            {
                pointerEnter = dropZone.gameObject
            };
            card.OnDrag(onDragEvent);

            // drop the card into the drop zone. The drop zone should no longer be empty
            card.OnEndDrag(genericDragEvent);

            Assert.IsFalse(dropZoneScript.IsEmpty());
        }

        [Test]
        public void OnEndDrag_ReorderCard()
        {
            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);

            // assume the card begins already inside the drop zone
            card.IsReordering = true;

            card.OnBeginDrag(genericDragEvent);

            // drag and drop the card at another location within the drop zone
            var onDragEvent = new PointerEventData(EventSystem.current)
            {
                pointerEnter = dropZone.gameObject
            };
            card.OnDrag(onDragEvent);
            card.SetDropZone(dropZone.GetComponent<RGDropZone>());

            card.OnEndDrag(genericDragEvent);

            // the card should complete its reordering
            Assert.IsFalse(card.IsReordering);
        }

        [Test]
        public void OnEndDrag_DestroyAddedCard()
        {
            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);
            var dropZoneScript = dropZone.GetComponent<RGDropZone>();

            // assume the card begins already inside the drop zone
            dropZoneScript.AddChild(card.gameObject);
            card.IsReordering = true;

            card.OnBeginDrag(genericDragEvent);

            // drag and drop the card outside the drop zone
            var onDragEvent = new PointerEventData(EventSystem.current)
            {
                pointerEnter = null
            };
            card.OnDrag(onDragEvent);

            card.OnEndDrag(genericDragEvent);

            // the drop zone should have no children. The only one was removed
            Assert.IsEmpty(dropZoneScript.GetChildren());
        }
    }
}
