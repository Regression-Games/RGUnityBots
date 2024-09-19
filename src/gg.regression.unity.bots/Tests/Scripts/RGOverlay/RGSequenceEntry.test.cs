using NUnit.Framework;
using RegressionGames.TestFramework;
using UnityEngine;
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

        [SetUp]
        public void SetUp()
        {
            // create the entry we want to test
            _uat = new GameObject();
            entry = _uat.AddComponent<RGSequenceEntry>();
            entry.sequenceName = "Sequence Entry Name";
            entry.description = "Sequence Entry Description";
            entry.playButton = new GameObject().AddComponent<Button>();
            entry.editButton = new GameObject().AddComponent<Button>();
            entry.deleteButton = new GameObject().AddComponent<Button>();
            entry.nameComponent = RGTestUtils.CreateTMProPlaceholder();
            entry.descriptionComponent = RGTestUtils.CreateTMProPlaceholder();

            // mocks for the Sequence Editor + Manager
            manager = _uat.AddComponent<RGSequenceManager>();
            editor = manager.gameObject.AddComponent<RGSequenceEditor>();
            editor.titleComponent = RGTestUtils.CreateTMProPlaceholder();
            manager.sequenceEditor = editor.gameObject;
            var deleteDialog = new GameObject();
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
            Assert.AreEqual(entry.description, entry.descriptionComponent.text);
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
        public void OnEdit()
        {
            entry.OnEdit();

            // ensure that the sequence editor is activated
            Assert.IsTrue(editor.gameObject.activeSelf);
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
