using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RegressionGames.TestFramework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGSequenceEntryTests
    {
        private GameObject _uat;

        private RGSequenceEntry entry;

        private RGSequenceManager manager;

        private RGSequenceEditor editor;

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


            // create the entry we want to test
            _uat = new GameObject();
            entry = _uat.AddComponent<RGSequenceEntry>();
            entry.isOverride = false;
            entry.sequenceName = "Sequence Entry Name";
            entry.description = "Sequence Entry Description";
            entry.playButton = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            }.AddComponent<Button>();
            entry.editButton = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            }.AddComponent<Button>();
            entry.copyButton = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            }.AddComponent<Button>();
            entry.deleteButton = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            }.AddComponent<Button>();
            entry.overrideIndicator = new GameObject()
            {
                transform =
                {
                    parent = _uat.transform
                }
            };
            entry.recordingDot = new GameObject()
            {
                transform =
                {
                    parent = _uat.transform
                }
            };
            entry.nameComponent = TestHelpers.CreateTMProPlaceholder(_uat.transform);

            // mocks for the Sequence Editor + Manager
            manager = _uat.AddComponent<RGSequenceManager>();
            editor = manager.gameObject.AddComponent<RGSequenceEditor>();
            editor.AvailableSegmentsList = new GameObject()
            {
                transform =
                {
                    parent = _uat.transform
                }
            };
            editor.SegmentCardPrefab = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };
            editor.DropZonePrefab = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };
            editor.overrideIndicator = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };
            var dropZonePrefab = editor.DropZonePrefab.AddComponent<RGDropZone>();

            dropZonePrefab.Content = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };
            dropZonePrefab.emptyStatePrefab = new GameObject(){
                transform =
                {
                    parent = _uat.transform
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
            manager.sequenceEditor = editor.gameObject;
            var deleteDialog = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            };
            deleteDialog.AddComponent<RGDeleteSequence>();
            manager.deleteSequenceDialog = deleteDialog;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void Start()
        {
            entry.Start();

            // ensure that the entry consumes its public fields properly
            Assert.AreEqual(entry.sequenceName, entry.nameComponent.text);
            Assert.IsFalse(entry.overrideIndicator.activeSelf);
        }
        
        [Test]
        public void StartWithOverride()
        {
            entry.isOverride = true;
            entry.Start();
            
            // ensure that the override indicator is activated
            Assert.IsTrue(entry.overrideIndicator.activeSelf);
        }

        [Test]
        public void OnPlay()
        {
            // mock the action that the play button uses
            var playActionCalled = false;
            entry.playAction = () => { playActionCalled = true; };

            entry.OnPlay();

            // ensure that the play button's mock action was utilized
            Assert.IsTrue(playActionCalled);
        }

        [Test]
        public void OnCopy()
        {
            entry.OnCopy();

            // ensure that the sequence editor is activated
            Assert.IsTrue(editor.gameObject.activeSelf);

            // ensure it says copy
            Assert.IsTrue(editor.titleComponent.text.Contains("Copy Sequence"));
        }

        [Test]
        public void OnEdit()
        {
            entry.OnEdit();

            // ensure that the sequence editor is activated
            Assert.IsTrue(editor.gameObject.activeSelf);

            // ensure it says edit
            // TODO: This requires a ton more setup for a future time where it needs to really successfully load an existing sequence
            //Assert.IsTrue(editor.titleComponent.text.Contains("Edit Sequence"));
        }
        
        [Test]
        public void OnEditWithOverride()
        {
            entry.isOverride = true;
            entry.OnEdit();
            
            // ensure that the override indicator is activated
            Assert.IsTrue(editor.overrideIndicator.activeSelf);
        }

        [Test]
        public void OnDelete()
        {
            entry.OnDelete();

            // assert that the Delete Sequence dialog is shown
            Assert.IsTrue(manager.deleteSequenceDialog.activeSelf);
        }
    }
}
