using System;
using System.Collections;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RegressionGames
{
    /// <summary>
    /// Utilities for running tests within the Unity Test Runner using Regression Games features.
    /// </summary>
    public class RGTestUtils
    {

        /// <summary>
        /// Waits until a specific scene has been loaded, and asserts that it has been loaded
        /// </summary>
        /// <param name="sceneName">The name of the scene to wait for</param>
        /// <param name="timeout">The maximum time to wait for the scene to load (in seconds). Defaults to 5.</param>
        public static IEnumerator WaitForScene(string sceneName, int timeout = 5)
        {
            var startTime = Time.unscaledTime;
            bool loaded = false;
            while (!loaded)
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded)
                {
                    var timeElapsed = Time.time - startTime;
                    if (timeElapsed > timeout)
                    {
                        break;
                    }
                }
                yield return null;
            }
            yield return null;
            Assert.IsTrue(loaded, $"Scene {sceneName} failed to load within {timeout} seconds");
        }

        /// <summary>
        /// Wait for the specified condition to become true with a timeout.
        /// This is similar to WaitUntil(), but also includes a failing assertion if the timeout expires.
        /// </summary>
        /// <param name="condition">The condition that should become true</param>
        /// <param name="timeout">Maximum time to wait for the condition to become true (in seconds)</param>
        public static IEnumerator WaitUntil(Func<bool> condition, int timeout = 5)
        {
            var startTime = Time.unscaledTime;
            bool result;
            while (!(result = condition()) && Time.unscaledTime - startTime < timeout)
                yield return null;
            Assert.IsTrue(result, $"Condition did not become true within {timeout} seconds");
        }

        /// <summary>
        /// Plays back an existing recording, and then returns the save location of the recording.
        /// </summary>
        /// <param name="recordingPath">The path to the recording to play back (the full data.zip file path)</param>
        /// <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
        public static IEnumerator StartPlaybackFromZipFile(string recordingPath, Action<PlaybackResult> setPlaybackResult)
        {
            RGDebug.LogInfo("Loading and starting playback recording from " + recordingPath);
            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
            var botSegments = BotSegmentZipParser.ParseBotSegmentZipFromSystemPath(recordingPath, out var sessionId);
            var replayData = new BotSegmentsPlaybackContainer(botSegments, sessionId);
            playbackController.SetDataContainer(replayData);
            playbackController.Play();
            yield return null; // Allow the recording to start playing
            while (playbackController.IsPlaying())
            {
                yield return null;
            }
            yield return null;
            RGDebug.LogInfo("Playback complete!");
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip"
            };
            setPlaybackResult(result);
        }

        /// <summary>
        /// Plays back a bot sequence, and then returns the save location of the recording.
        /// </summary>
        /// <param name="sequencePath">The relative path to the bot sequence</param>
        /// <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
        public static IEnumerator StartBotSequence(string sequencePath, Action<PlaybackResult> setPlaybackResult)
        {
            RGDebug.LogInfo("Loading and starting bot sequence from " + sequencePath);

            var botSequence = BotSequence.LoadSequenceJsonFromPath(sequencePath);

            botSequence.Play();

            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();

            yield return null; // Allow the recording to start playing
            while (playbackController.IsPlaying())
            {
                yield return null;
            }
            yield return null;
            RGDebug.LogInfo("Playback complete!");
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip"
            };
            setPlaybackResult(result);
        }

    }
}
