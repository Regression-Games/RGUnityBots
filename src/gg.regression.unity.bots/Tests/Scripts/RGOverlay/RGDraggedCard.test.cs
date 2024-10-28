using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.TestFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGDraggedCardTests
    {
        private GameObject _uat;

        private RGDraggedCard card;

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
            card = _uat.AddComponent<RGDraggedCard>();
            card.draggedCardName = "Dragged Card";
            card.draggedCardResourcePath = "my/resource/path";
            card.payload = new Dictionary<string, string>();
            card.iconPrefab = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };
            card.nameComponent = RGTestUtils.CreateTMProPlaceholder(_uat.transform);
            card.resourcePathComponent = RGTestUtils.CreateTMProPlaceholder(_uat.transform);
            card.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void Initialize()
        {
            Assert.AreEqual(card.nameComponent.text, card.draggedCardName);
        }
    }
}
