using System.Collections;
using NUnit.Framework;
using RegressionGames.TestFramework;
using RegressionGames.Types;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RegressionGames.Tests.GameScene
{
    [TestFixture]
    public class GameSceneTests : InputTestFixture
    {
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
            yield return RGTestUtils.StartBotSequence(sequencePath, result => sequenceResult = result);

            // Print out the recording path for viewing later
            RGDebug.LogInfo("Played back the bot sequence - saved to " + sequenceResult.saveLocation);
            Assert.IsNotNull(sequenceResult.saveLocation);

        }
    }
}
