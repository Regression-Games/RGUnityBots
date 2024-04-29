using System.Collections;
using RegressionGames.StateRecorder;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace RegressionGames
{
    public class RGTestUtils
    {
        
        /**
         * Wait until a specific scene has been loaded
         */
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

        public static void StartRecording()
        {
            RGDebug.LogInfo("Recording in-depth data for this test run");
            ScreenRecorder.GetInstance()?.StartRecording();
        }

        public static void StopRecording()
        {
            ScreenRecorder.GetInstance()?.StopRecording();
        }

        /*
         * Plays back an existing recording 
         */
        public static IEnumerator StartPlayback(string recordingPath)
        {
            RGDebug.LogInfo("Loading and starting playback recording from " + recordingPath);
            var playbackController = GameObject.FindObjectOfType<ReplayDataPlaybackController>();
            var replayData = new ReplayDataContainer(recordingPath);
            playbackController.SetDataContainer(replayData);
            playbackController.Play();
            while (playbackController.ReplayCompletedSuccessfully() != true)
            {
                yield return null;
            }
        }
        
    }
}