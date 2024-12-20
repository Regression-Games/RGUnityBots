using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.TestFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGSequenceManagerTests
    {
        private GameObject _uat;

        private RGSequenceManager manager;

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


            // create the manager we want to test
            _uat = new GameObject();
            manager = _uat.AddComponent<RGSequenceManager>();
            manager.sequencesPanel = new GameObject();
            manager.segmentsPanel = new GameObject();
            manager.sequenceCardPrefab = new GameObject();
            manager.segmentCardPrefab = new GameObject();
            manager.sequenceEditor = new GameObject();
            manager.sequenceEditor.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void Start()
        {
            manager.Start();

            Assert.IsNotNull(manager.sequencesPanel);
            Assert.IsNotNull(manager.segmentsPanel);
            Assert.IsTrue(manager.sequencesPanel.activeSelf);
            Assert.IsFalse(manager.segmentsPanel.activeSelf);
        }

    [Test]
        public void ShowEditSequenceDialog()
        {
            manager.ShowEditSequenceDialog(false, null, null);

            // ensure the sequence editor is visible
            Assert.IsTrue(manager.sequenceEditor.activeSelf);
        }

        [Test]
        public void HideEditSequenceDialog()
        {
            manager.HideEditSequenceDialog();

            // ensure the sequence editor is hidden
            Assert.IsFalse(manager.sequenceEditor.activeSelf);
        }

        [Test]
        public void InstantiateSequences()
        {
            // add a list of bot sequences to the manager for instantiation
            const int numSequences = 5;
            var sequences = new Dictionary<string, (string, BotSequence)>();
            for (var i = 0; i < numSequences; ++i)
            {
                sequences.Add($"/placeholder/path/{i}", (null, new BotSequence()));
            }

            manager.InstantiateSequences(sequences);

            // ensure that the sequences panel is populated with the correct number of prefabs
            Assert.AreEqual(numSequences, manager.sequencesPanel.transform.childCount);
        }
    }
}
