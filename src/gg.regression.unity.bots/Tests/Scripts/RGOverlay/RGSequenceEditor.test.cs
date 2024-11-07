using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.TestFramework;
using RegressionGames.StateRecorder.BotSegments.Models;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGSequenceEditorTests
    {
        private GameObject _uat;

        private RGSequenceEditor editor;

        private IList<BotSequenceEntry> segments;

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


            // create the editor we want to test
            _uat = new GameObject();
            editor = _uat.AddComponent<RGSequenceEditor>();

            var dropZone = RGOverlayUtils.CreateNewDropZone(_uat);

            // create the required public fields the editor needs
            var textObject = new GameObject(){
                transform =
                {
                    parent = _uat.transform,
                },
            };
            var text = textObject.AddComponent<TMP_InputField>();
            editor.NameInput = text;
            editor.DescriptionInput = text;
            editor.SearchInput = text;
            editor.titleComponent = TestHelpers.CreateTMProPlaceholder(_uat.transform);
            editor.AvailableSegmentsList = new GameObject(){
                transform =
                {
                    parent = _uat.transform,
                },
            };
            editor.CreateSequenceButton = new GameObject(){
                transform =
                {
                    parent = _uat.transform,
                },
            };
            editor.CreateSequenceButton.AddComponent<Button>();
            editor.SegmentCardPrefab = new GameObject(){
                transform =
                {
                    parent = _uat.transform,
                },
            };
            editor.overrideIndicator = new GameObject() {
                transform =
                {
                    parent = _uat.transform,
                },
            };
            editor.createInstructionText = new GameObject() {
                transform =
                {
                    parent = _uat.transform,
                },
            };
            editor.updateInstructionText = new GameObject() {
                transform =
                {
                    parent = _uat.transform,
                },
            };
            editor.updateOverrideInstructionText = new GameObject() {
                transform =
                {
                    parent = _uat.transform,
                },
            };
            editor.DropZonePrefab = dropZone;
            editor.SegmentListIcon = TestHelpers.CreateSpritePlaceholder();
            editor.SegmentIcon = TestHelpers.CreateSpritePlaceholder();
            editor.SegmentListIcon = TestHelpers.CreateSpritePlaceholder();

            // mock segment entries for the editor to utilize
            segments = new List<BotSequenceEntry>()
            {
                new()
                {
                    type = BotSequenceEntryType.Segment,
                    path = "segment/one",
                    name = "Segment One",
                    description = "Segment One Description"
                },
                new()
                {
                    type = BotSequenceEntryType.Segment,
                    path = "segment/two",
                    name = "Segment Two",
                    description = "Segment Two Description",
                },
                new()
                {
                    type = BotSequenceEntryType.SegmentList,
                    path = "segment/three/list",
                    name = "Segment Three",
                    description = "Segment Three Description",
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }
        
        [Test]
        public void Initialize()
        {
            editor.Initialize(false, null, null, false);

            // ensure that the editor consumes its public fields properly
            Assert.NotNull(editor.NameInput.onValueChanged);
            Assert.NotNull(editor.SearchInput.onValueChanged);
            Assert.IsFalse(editor.overrideIndicator.activeSelf);
        }

        [Test]
        public void InitializeCreate()
        {
            editor.Initialize(false, null, null, false);

            // ensure that the instruction text is correct
            Assert.IsTrue(editor.createInstructionText.activeSelf);
            Assert.IsFalse(editor.updateInstructionText.activeSelf);
            Assert.IsFalse(editor.updateOverrideInstructionText.activeSelf);
        }
        
        [Test]
        public void InitializeEdit()
        {
            var resourcePath = "mock/resource/path/sequence.json";
            try
            {
                editor.Initialize(false, resourcePath, null, false);
            }
            catch { }
            
            // TODO: use a mocking library to mock file access. This test cannot be performed until we can mock
            // BotSequence.LoadSequenceJsonFromPath. The Sequence Editor's SetEditingState depends on it
            
            // ensure that the instruction text is correct
            // Assert.IsFalse(editor.createInstructionText.activeSelf);
            // Assert.IsTrue(editor.updateInstructionText.activeSelf);
            // Assert.IsFalse(editor.updateOverrideInstructionText.activeSelf);
        }
        
        [Test]
        public void InitializeWithOverride()
        {
            editor.Initialize(false, null, null, true);

            // ensure that the instruction text is correct
            Assert.IsFalse(editor.createInstructionText.activeSelf);
            Assert.IsTrue(editor.updateInstructionText.activeSelf);
            Assert.IsTrue(editor.updateOverrideInstructionText.activeSelf);
        }

        [Test]
        public void CreateAvailableSegments()
        {
            editor.CreateAvailableSegments(segments);

            // ensure that the mock segments were instantiated as prefabs properly
            Assert.AreEqual(segments.Count, editor.AvailableSegmentsList.transform.childCount);
        }

        [Test]
        public void ClearAvailableSegments()
        {
            editor.CreateAvailableSegments(segments);
            var numSegmentsCleared = editor.ClearAvailableSegments();

            // ensure that the number of segments removed matches the expected amount
            Assert.AreEqual(segments.Count, numSegmentsCleared);
        }

        [Test]
        public void SaveSequence()
        {
            editor.Initialize(false, null, null, false);

            // set the name and description values to save
            editor.NameInput.text = "Sequence Editor Name";
            editor.DescriptionInput.text = "Sequence Editor Description";

            editor.SaveSequence();

            // ensure that the editor extracts the values from the text inputs properly
            Assert.AreEqual(editor.CurrentSequence.name, editor.NameInput.text);
            Assert.AreEqual(editor.CurrentSequence.description, editor.DescriptionInput.text);
        }

        [Test]
        public void ResetEditor()
        {
            editor.Initialize(false, null, null, false);

            // set the name and description values we want to reset
            editor.NameInput.text = "Sequence Editor Name";
            editor.DescriptionInput.text = "Sequence Editor Description";

            editor.ResetEditor();

            var dropZone = editor.DropZonePrefab.GetComponent<RGDropZone>();
            var button = editor.CreateSequenceButton.GetComponent<Button>();

            // ensure that the name and description text inputs are cleared, the drop zone's children are cleared, and that
            // the create sequence button is disabled
            Assert.IsEmpty(editor.NameInput.text);
            Assert.IsEmpty(editor.DescriptionInput.text);
            Assert.IsEmpty(dropZone.GetChildren());
            Assert.IsFalse(button.interactable);
        }

        [Test]
        public void ReloadAvailableSegments()
        {
            editor.Initialize(false, null, null, false);

            editor.ReloadAvailableSegments();

            Assert.IsEmpty(editor.SearchInput.text);
        }

        [Test]
        public void SetCreateSequenceButtonEnabled_Enabled()
        {
            editor.Initialize(false, null, null, false);

            editor.SetCreateSequenceButtonEnabled(true);

            // ensure that the create sequence button is enabled
            var button = editor.CreateSequenceButton.GetComponent<Button>();
            Assert.IsTrue(button.interactable);
        }

        [Test]
        public void SetCreateSequenceButtonEnabled_Disabled()
        {
            editor.Initialize(false, null, null, false);

            editor.SetCreateSequenceButtonEnabled(false);

            // ensure that the create sequence button is disabled
            var button = editor.CreateSequenceButton.GetComponent<Button>();
            Assert.IsFalse(button.interactable);
        }
    }
}
