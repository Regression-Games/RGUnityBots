using System;
using System.Collections;

using NUnit.Framework;
using RegressionGames.TestFramework;
using RegressionGames.Types;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RegressionGames.Tests.GameScene
{
    [TestFixture]
    public class GameSceneTests
    {
        private int oldIndex = -1;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            SavePreviousViewAspect();
            //force this to match our recording resolution
            Screen.SetResolution(1920, 1080, false);

            // set to full HD (1920x1080)
            SetEditorAspectRatio(3);
        }

        [UnityTest]
        public IEnumerator TestGameObjectsSequence()
        {
            // Define which bot sequence to use
            string sequencePath = "BotSequences/GameScenePlaybackTest.json";

            // Wait for the scene
            SceneManager.LoadSceneAsync("GameObjectTestScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("GameObjectTestScene");

            // Start the sequence
            PlaybackResult sequenceResult = null;
            yield return RGTestUtils.StartBotSequence(sequencePath, result => sequenceResult = result, timeout: 30);

            if (!sequenceResult.success)
            {
                RGDebug.LogWarning("Sequence playback timed out");
            }
            Assert.IsTrue(sequenceResult.success);


            // Print out the recording path for viewing later
            RGDebug.LogInfo("Played back the bot sequence - saved to " + sequenceResult.saveLocation);
            Assert.IsNotNull(sequenceResult.saveLocation);

        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (oldIndex > -1)
            {
                SetEditorAspectRatio(oldIndex);
            }
        }

        private void SetEditorAspectRatio(int index)
        {
#if UNITY_EDITOR
                Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                EditorWindow window = EditorWindow.GetWindow(gameView);
                PropertyInfo selectedSizeIndex = gameView.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                selectedSizeIndex.SetValue(window, index, null);
#endif
        }

        private void SavePreviousViewAspect()
        {
#if UNITY_EDITOR
            Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow window = EditorWindow.GetWindow(gameView);
            PropertyInfo selectedSizeIndex = gameView.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            oldIndex = (int) selectedSizeIndex.GetValue(window);
#endif
        }
    }
}
