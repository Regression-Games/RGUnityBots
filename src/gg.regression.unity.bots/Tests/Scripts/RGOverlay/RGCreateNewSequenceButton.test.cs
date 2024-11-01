using System.Collections;
using NUnit.Framework;
using RegressionGames.TestFramework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGCreateNewSequenceButtonTests
    {
        private GameObject _uat;

        private RGCreateNewSequenceButton button;

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
            button = _uat.AddComponent<RGCreateNewSequenceButton>();

            // create the sequence manager the button references
            var overlay = new GameObject
            {
                transform =
                {
                    parent = _uat.transform
                }
            };
            var sequenceManager = overlay.gameObject.AddComponent<RGSequenceManager>();
            sequenceManager.sequencesPanel = new GameObject()            {
                transform =
                {
                    parent = overlay.transform
                }
            };

            var editor = sequenceManager.gameObject.AddComponent<RGSequenceEditor>();
            editor.AvailableSegmentsList = new GameObject()            {
                transform =
                {
                    parent = sequenceManager.transform
                }
            };
            editor.SegmentCardPrefab = new GameObject(){
                transform =
                {
                    parent = sequenceManager.transform
                }
            };
            editor.DropZonePrefab = new GameObject(){
                transform =
                {
                    parent = sequenceManager.transform
                }
            };
            var dropZonePrefab = editor.DropZonePrefab.AddComponent<RGDropZone>();

            dropZonePrefab.Content = new GameObject(){
                transform =
                {
                    parent = sequenceManager.transform
                }
            };
            dropZonePrefab.emptyStatePrefab = new GameObject(){
                transform =
                {
                    parent = sequenceManager.transform
                }
            };
            editor.titleComponent = TestHelpers.CreateTMProPlaceholder(_uat.transform);
            var ni = new GameObject
            {
                transform =
                {
                    parent = editor.transform
                }
            };
            editor.NameInput = ni.AddComponent<TMP_InputField>();
            var di = new GameObject
            {
                transform =
                {
                    parent = editor.transform
                }
            };
            editor.DescriptionInput =  di.AddComponent<TMP_InputField>();
            var si = new GameObject
            {
                transform =
                {
                    parent = editor.transform
                }
            };
            editor.SearchInput =  si.AddComponent<TMP_InputField>();
            sequenceManager.sequenceEditor = editor.gameObject;

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
            Assert.IsTrue(sequenceManager.sequenceEditor.activeSelf);

            // ensure it says create
            Assert.IsTrue(sequenceManager.sequenceEditor.GetComponent<RGSequenceEditor>().titleComponent.text.Contains("Create Sequence"));
        }
    }
}
