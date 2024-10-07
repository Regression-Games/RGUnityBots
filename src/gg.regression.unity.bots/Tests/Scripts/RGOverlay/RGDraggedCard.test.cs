using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.TestFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGDraggedCardTests
    {
        private GameObject _uat;

        private RGDraggedCard card;

        [SetUp]
        public void SetUp()
        {
            // create the card we want to test
            _uat = new GameObject();
            card = _uat.AddComponent<RGDraggedCard>();
            card.transform.SetParent(_uat.transform, false);
            card.draggedCardName = "Dragged Card";
            card.draggedCardResourcePath = "my/resource/path";
            card.payload = new Dictionary<string, string>();
            card.iconPrefab = new GameObject();
            card.nameComponent = RGTestUtils.CreateTMProPlaceholder();
            card.resourcePathComponent = RGTestUtils.CreateTMProPlaceholder();
            card.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(card);
            Object.Destroy(_uat);
        }

        [Test]
        public void Initialize()
        {
            Assert.AreEqual(card.nameComponent.text, card.draggedCardName);
        }
    }
}
