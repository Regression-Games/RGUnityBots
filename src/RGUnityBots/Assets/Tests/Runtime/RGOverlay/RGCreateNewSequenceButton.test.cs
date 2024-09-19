using NUnit.Framework;
using RegressionGames;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Runtime.RGOverlay
{
    [TestFixture]
    public class RGCreateNewSequenceButtonTests
    {
        private GameObject _uat;

        private RGCreateNewSequenceButton button;

        [SetUp]
        public void SetUp()
        {
            // create the button we want to test
            _uat = new GameObject();
            button = _uat.AddComponent<RGCreateNewSequenceButton>();
            button.transform.SetParent(_uat.transform, false);
        
            // create the sequence manager the button references
            var overlay = new GameObject();
            var sequenceManager = overlay.gameObject.AddComponent<RGSequenceManager>();
            sequenceManager.sequenceEditor = new GameObject();
            sequenceManager.sequencesPanel = new GameObject();

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
        }
    }
}