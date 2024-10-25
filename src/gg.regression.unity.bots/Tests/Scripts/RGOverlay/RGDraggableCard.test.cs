using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.TestFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // get a clean scene
            var botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // destroy any existing overlay before loading new test scene
                Object.Destroy(botManager.gameObject);
            }

            // Wait for the scene
            SceneManager.LoadSceneAsync("EmptyScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("EmptyScene");


            // create the card we want to test
            _uat = new GameObject();
            card = _uat.AddComponent<RGDraggableCard>();
            card.gameObject.AddComponent<RectTransform>();
            card.payload = new Dictionary<string, string>();
            card.draggableCardName = "Card Name";
            card.draggableCardDescription = "Card Description";

            // add placeholder text fields to the card
            var text = RGTestUtils.CreateTMProPlaceholder(_uat.transform);
            card.namePrefab = text;
            card.descriptionPrefab = text;
            card.resourcePathPrefab = text;

            // ensure public prefabs are mocked
            card.restingStatePrefab = new GameObject
            {
                transform =
                {
                    parent = card.transform
                }
            };
            card.restingStatePrefab.AddComponent<RGDraggableCard>();
            card.draggingStatePrefab = new GameObject(){
                transform =
                {
                    parent = card.transform
                }
            };
            var draggedCard = card.draggingStatePrefab.AddComponent<RGDraggedCard>();
            draggedCard.iconPrefab = new GameObject(){
                transform =
                {
                    parent = card.transform
                }
            };
            draggedCard.iconPrefab.AddComponent<Image>();
            draggedCard.nameComponent = RGTestUtils.CreateTMProPlaceholder(_uat.transform);
            draggedCard.resourcePathComponent = RGTestUtils.CreateTMProPlaceholder(_uat.transform);
            card.icon = RGTestUtils.CreateSpritePlaceholder();
            card.iconPrefab = new GameObject(){
                transform =
                {
                    parent = card.transform
                }
            };
            card.iconPrefab.AddComponent<Image>();
            card.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void SetDropZone_IsOverDropZone()
        {
            Assert.IsFalse(card.IsOverDropZone());

            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);
            try
            {
                card.SetDropZone(dropZone.GetComponent<RGDropZone>());

                Assert.IsTrue(card.IsOverDropZone());
            }
            finally
            {
                Object.Destroy(dropZone);
            }
        }

        [Test]
        public void OnBeginDrag()
        {
            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);
            try
            {
                var dragEvent = new PointerEventData(EventSystem.current);
                card.OnBeginDrag(dragEvent);

                // ensure the card shows its dragging state
                var draggedCard = Object.FindObjectOfType<RGDraggableCard>();
                Assert.NotNull(draggedCard);
            }
            finally
            {
                Object.Destroy(dropZone);
            }
        }

        [Test]
        public void OnEndDrag_AddCardToDropZone()
        {
            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);
            try
            {
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
            finally
            {
                Object.Destroy(dropZone);
            }
        }

        [Test]
        public void OnEndDrag_ReorderCard()
        {
            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);
            try
            {
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
            finally
            {
                Object.Destroy(dropZone);
            }
        }

        [Test]
        public void OnEndDrag_DestroyAddedCard()
        {
            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);
            try
            {
                var dropZoneScript = dropZone.GetComponent<RGDropZone>();

                // assume the card begins already inside the drop zone
                dropZoneScript.AddChild(card.gameObject);
                card.IsReordering = true;

                card.OnBeginDrag(genericDragEvent);

                // drag and drop the card outside the drop zone
                var onDragEvent = new PointerEventData(EventSystem.current)
                {
                    position = new Vector2()
                };
                card.OnDrag(onDragEvent);
                card.SetDropZone(null);

                card.OnEndDrag(genericDragEvent);

                // the drop zone should have no children. The only one was removed
                Assert.IsEmpty(dropZoneScript.GetChildren());
            }
            finally
            {
                Object.Destroy(dropZone);
            }
        }
    }
}
