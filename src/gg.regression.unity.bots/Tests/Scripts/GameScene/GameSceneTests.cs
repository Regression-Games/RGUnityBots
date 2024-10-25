using System;
using System.Collections;

using NUnit.Framework;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.Types;
using RegressionGames.TestFramework;
using RegressionGames.Types;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace RegressionGames.Tests.Z_RunMeLast_GameScene
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

            MouseEventSender.InitializeVirtualMouse();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // destroy any existing overlay before loading new test scene
                Object.Destroy(botManager.gameObject);
            }

            // Wait for the scene
            SceneManager.LoadSceneAsync("GameObjectTestScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("GameObjectTestScene");

            botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // make sure the overlay has RGExcludeFromState removed
                var component = botManager.gameObject.GetComponent<RGExcludeFromState>();
                if (component != null)
                {
                    Object.Destroy(component);
                }
            }
        }

        [UnityTest]
        public IEnumerator TestGameObjectsSequence()
        {
            // Define which bot sequence to use
            string sequencePath = "BotSequences/GameScenePlaybackTest.json";

            // Start the sequence
            PlaybackResult sequenceResult = null;
            // give it up to 1 minute to finish before reporting time out failure - reality, it finishes in about 10 seconds on the windows laptops
            yield return RGTestUtils.StartBotSequence(sequencePath, result => sequenceResult = result, timeout: 60);

            if (!sequenceResult.success)
            {
                RGDebug.LogWarning("Sequence playback timed out");
            }
            Assert.IsTrue(sequenceResult.success);


            // Print out the recording path for viewing later
            RGDebug.LogInfo("Played back the bot sequence - saved to " + sequenceResult.saveLocation);
            Assert.IsNotNull(sequenceResult.saveLocation);

        }

        [TearDown]
        public void TearDown()
        {

            // just get some component on the top level RGOverlayCanvas
            var botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // remove our altered overlay
                Object.Destroy(botManager.gameObject);
            }

            SceneManager.UnloadSceneAsync("GameObjectTestScene");
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
                MethodInfo selectSize = gameView.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public);
                selectSize.Invoke(window, new object[]{index, null});
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
