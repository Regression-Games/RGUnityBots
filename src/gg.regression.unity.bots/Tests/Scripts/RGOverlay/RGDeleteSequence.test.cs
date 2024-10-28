using System;
using System.Collections;
using NUnit.Framework;
using RegressionGames.TestFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGDeleteSequenceTests
    {
        private GameObject _uat;

        private RGDeleteSequence deleter;

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

            // create the delete script we want to test
            _uat = new GameObject();
            deleter = _uat.AddComponent<RGDeleteSequence>();
            deleter.sequenceNamePrefab = RGTestUtils.CreateTMProPlaceholder(_uat.transform);
            deleter.confirmButton = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            }.AddComponent<Button>();
            deleter.cancelButton = new GameObject(){
                transform =
                {
                    parent = _uat.transform
                }
            }.AddComponent<Button>();
            deleter.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void Start()
        {
            Assert.NotNull(deleter.confirmButton);
            Assert.NotNull(deleter.cancelButton);
        }

        [Test]
        public void Initialize()
        {
            var entry = new GameObject(){
                transform =
                {
                    parent = _uat.transform,
                },
            }.AddComponent<RGSequenceEntry>();
            entry.sequenceName = "Great Sequence";
            entry.resourcePath = "/sequence/path";
            entry.filePath = "/sequence/path-to-delete";

            deleter.Initialize(entry);

            // ensure that we transfer the sequence entry's name and path to the script's display fields
            Assert.AreEqual(entry.sequenceName, deleter.sequenceNamePrefab.text);
            Assert.AreEqual(entry.resourcePath, deleter.resourcePath);
            Assert.AreEqual(entry.filePath, deleter.filePath);
        }

        [Test]
        public void OnDelete_HasSequencePath()
        {
            deleter.resourcePath = "/sequence/path";
            deleter.filePath = "/sequence/path-to-delete";

            deleter.OnDelete();

            // ensure that we reset the script's display fields after deleting
            Assert.IsEmpty(deleter.resourcePath);
            Assert.IsEmpty(deleter.filePath);
            Assert.IsEmpty(deleter.sequenceNamePrefab.text);
        }

        [Test]
        public void OnDelete_NoSequencePath()
        {

            deleter.resourcePath = null;
            deleter.filePath = null;

            // ensure that a missing sequence path throws an exception
            Assert.Throws<Exception>(() => deleter.OnDelete());
        }

        [Test]
        public void OnCancel()
        {
            // ensure that we can cancel the deletion and reset the scripts display fields
            Assert.DoesNotThrow(() => deleter.OnCancel());
            Assert.IsEmpty(deleter.resourcePath);
            Assert.IsEmpty(deleter.filePath);
            Assert.IsEmpty(deleter.sequenceNamePrefab.text);
        }
    }
}
