using System.Collections;
using NUnit.Framework;
using RegressionGames.TestFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGCancelSequenceEditButtonTests
    {
        private GameObject _uat;

        private RGCancelSequenceEditButton button;

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


            // create the button we want to test
            _uat = new GameObject();
            button = _uat.AddComponent<RGCancelSequenceEditButton>();

            // create the sequence manager the button references
            var overlay = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };
            var sequenceManager = overlay.gameObject.AddComponent<RGSequenceManager>();
            sequenceManager.sequenceEditor = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };

            button.overlayContainer = overlay;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void OnClick()
        {
            button.OnClick();

            // ensure that the sequence editor is closed on click
            var sequenceManager = button.overlayContainer.GetComponent<RGSequenceManager>();
            Assert.IsFalse(sequenceManager.sequenceEditor.activeSelf);
        }
    }
}
