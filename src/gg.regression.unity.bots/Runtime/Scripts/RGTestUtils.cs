using System;
using System.Collections;
using RegressionGames.StateRecorder;
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
            var startTime = Time.time;
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
            Assert.IsTrue(loaded);
        }
        
        
        /// <summary>
        /// Plays back an existing recording, and then returns the save location of the recording.
        /// </summary>
        /// <param name="recordingPath">The path to the recording to play back (the full data.zip file path)</param>
        /// <param name="setSaveLocation">A callback that will be called with the save location of the recording</param>
        public static IEnumerator StartPlaybackFromFile(string recordingPath, Action<string> setSaveLocation)
        {
            RGDebug.LogInfo("Loading and starting playback recording from " + recordingPath);
            var playbackController = Object.FindObjectOfType<ReplayDataPlaybackController>();
            var replayData = new ReplayDataContainer(recordingPath);
            playbackController.SetDataContainer(replayData);
            playbackController.Play();
            yield return null; // Allow the recording to start playing
            while (playbackController.IsPlaying())
            {
                yield return null;
            }
            yield return null;
            RGDebug.LogInfo("Playback complete!");
            setSaveLocation(playbackController.SaveLocation() + ".zip");
        }
        
    }
}