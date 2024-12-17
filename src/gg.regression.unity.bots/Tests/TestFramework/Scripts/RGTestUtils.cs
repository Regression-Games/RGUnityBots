using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using RegressionGames.Types;
using StateRecorder.BotSegments;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ReSharper disable UnusedMember.Global - These are helper methods for customer projects, don't remove them
// ReSharper disable MemberCanBePrivate.Global - These are helper methods for customer projects, don't make them private
namespace RegressionGames.TestFramework
{
    /**
     * <summary>
     * Utilities for running tests within the Unity Test Runner using Regression Games features.
     * </summary>
     */
    public class RGTestUtils
    {
        /**
         * <summary>
         * Waits until a specific scene has been loaded, and asserts that it has been loaded
         * </summary>
         * <param name="sceneName">The name of the scene to wait for</param>
         * <param name="timeout">The maximum time to wait for the scene to load (in seconds). Defaults to 5.</param>
         */
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

        /**
         * <summary>
         * Wait for the specified condition to become true with a timeout.
         * This is similar to WaitUntil(), but also includes a failing assertion if the timeout expires.
         * </summary>
         * <param name="condition">The condition that should become true</param>
         * <param name="timeout">Maximum time to wait for the condition to become true (in seconds)</param>
         * <param name="message">Message to display upon failure (optional)</param>
         */
        public static IEnumerator WaitUntil(Func<bool> condition, int timeout = 5, string message = null)
        {
            var startTime = Time.unscaledTime;
            bool result;
            while (!(result = condition()) && Time.unscaledTime - startTime < timeout)
                yield return null;
            Assert.IsTrue(result, message ?? $"Condition did not become true within {timeout} seconds");
        }

        /**
         * <summary>
         * Plays back an existing recording, and then returns the save location of the recording.
         * </summary>
         * <param name="recordingPath">The path to the recording to play back (the full data.zip file path)</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this segment to complete, &lt;=0 means wait forever (default)</param>
         */
        public static IEnumerator StartPlaybackFromZipFile(string recordingPath, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Loading and starting playback recording from " + recordingPath);
            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
            var statusManager = Object.FindObjectOfType<BotSegmentPlaybackStatusManager>();
            var botSegments = BotSegmentZipParser.ParseBotSegmentZipFromSystemPath(recordingPath, out var sessionId);
            var replayData = new BotSegmentsPlaybackContainer(botSegments, sessionId);
            playbackController.SetDataContainer(replayData);
            playbackController.Play();

            yield return null; // Allow the recording to start playing
            var didTimeout = false;

            var startTime = Time.unscaledTime;
            while (playbackController.GetState() == PlayState.Playing || playbackController.GetState() == PlayState.Paused)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout)
                {
                    didTimeout = true;
                    break;
                }

                yield return null;
            }

            yield return null;
            RGDebug.LogInfo("Playback complete! - " + (didTimeout ? "TIMEOUT" : "SUCCESS"));
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip",
                success = !didTimeout,
                statusMessage = didTimeout?statusManager.LastError():null
            };
            setPlaybackResult(result);
            playbackController.Stop();
        }

        /**
         * <summary>
         * Plays back an existing recording, and then returns the save location of the recording.
         * </summary>
         * <param name="recordingPath">The path to the recording to play back.  Directory of numeric json files.</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this segment to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator StartPlaybackFromDirectory(string recordingPath, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Loading and starting playback recording from " + recordingPath);
            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
            var statusManager = Object.FindObjectOfType<BotSegmentPlaybackStatusManager>();
            var botSegments = BotSegmentDirectoryParser.ParseBotSegmentSystemDirectory(recordingPath, out var sessionId);
            var replayData = new BotSegmentsPlaybackContainer(botSegments, sessionId);
            playbackController.SetDataContainer(replayData);
            playbackController.Play();

            yield return null; // Allow the recording to start playing
            var didTimeout = false;

            var startTime = Time.unscaledTime;
            while (playbackController.GetState() == PlayState.Playing || playbackController.GetState() == PlayState.Paused)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout)
                {
                    didTimeout = true;
                    break;
                }

                yield return null;
            }

            yield return null;
            RGDebug.LogInfo("Playback complete! - " + (didTimeout ? "TIMEOUT" : "SUCCESS"));
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip",
                success = !didTimeout,
                statusMessage = didTimeout?statusManager.LastError():null
            };
            setPlaybackResult(result);
            playbackController.Stop();
        }

        /**
         * <summary>
         * Runs a bot action and waits for it to complete, then returns success or failure
         * </summary>
         * <param name="botCriteria">The List of KeyFrameCriteria to wait for</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this check to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator WaitForBotCriteria(List<KeyFrameCriteria> botCriteria, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Waiting for match of bot criteria: " + string.Join(",", botCriteria.ToArray().Select(a=>a.ToString())));

            // persist the starting state as the last successful key frame so that counts are reasonable and doesn't show everything as added on every evaluation
            KeyFrameEvaluator.Evaluator.PersistPriorFrameStatus();

            // wait a frame before starting evaluation
            yield return null;

            var matched = botCriteria.Count == 0;

            var didTimeout = false;

            var startTime = Time.unscaledTime;

            while (!matched)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout)
                {
                    didTimeout = true;
                    break;
                }

                matched = KeyFrameEvaluator.Evaluator.Matched(
                    true,
                    0,
                    true,
                    botCriteria
                    );

                if (!matched)
                {
                    // wait a frame before checking again each time
                    yield return null;
                }
            }

            RGDebug.LogInfo("Wait for match of bot criteria complete! - " + (didTimeout ? "TIMEOUT" : "SUCCESS"));
            var result = new PlaybackResult
            {
                saveLocation = null, // not applicable for this
                success = !didTimeout,
                statusMessage = didTimeout ? KeyFrameEvaluator.Evaluator.GetUnmatchedCriteria() : null
            };
            setPlaybackResult(result);
            KeyFrameEvaluator.Evaluator.Reset();

        }

        /**
         * <summary>
         * Runs a bot action and waits for it to complete, then returns success or failure
         * </summary>
         * <param name="botAction">The bot action</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this action to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator PerformBotAction(BotAction botAction, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Starting bot action of type: " + botAction.type);

            var objectFinders = Object.FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);

            Dictionary<long, ObjectStatus> transformStatuses = null;
            Dictionary<long, ObjectStatus> entityStatuses = null;

            foreach (var objectFinder in objectFinders)
            {
                if (objectFinder is TransformObjectFinder)
                {
                    transformStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                }
                else
                {
                    entityStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                }
            }

            transformStatuses ??= new Dictionary<long, ObjectStatus>();
            entityStatuses ??= new Dictionary<long, ObjectStatus>();

            botAction.StartAction(0, transformStatuses, entityStatuses);
            // process the action the first time
            botAction.ProcessAction(0, transformStatuses, entityStatuses, out var lastError);

            var didTimeout = false;

            var startTime = Time.unscaledTime;
            while (botAction?.IsCompleted != true)
            {
                // wait a frame before calling process again each time
                yield return null;
                if (timeout > 0 && Time.unscaledTime > startTime + timeout)
                {
                    didTimeout = true;
                    break;
                }

                foreach (var objectFinder in objectFinders)
                {
                    if (objectFinder is TransformObjectFinder)
                    {
                        transformStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                    }
                    else
                    {
                        entityStatuses = objectFinder.GetObjectStatusForCurrentFrame().Item2;
                    }
                }

                transformStatuses ??= new Dictionary<long, ObjectStatus>();
                entityStatuses ??= new Dictionary<long, ObjectStatus>();

                // process the action
                botAction.ProcessAction(0, transformStatuses, entityStatuses, out lastError);
            }

            yield return null;
            RGDebug.LogInfo("Bot action complete! - " + (didTimeout ? "TIMEOUT" : (string.IsNullOrEmpty(lastError)?"SUCCESS":"ERROR")));
            var result = new PlaybackResult
            {
                saveLocation = null, // not applicable for this
                success = !didTimeout && string.IsNullOrEmpty(lastError),
                statusMessage = lastError
            };
            setPlaybackResult(result);
            botAction.AbortAction(0);

        }

        /**
         * <summary>
         * Plays back a bot segment, and then returns the save location of the recording.
         * </summary>
         * <param name="botSegment">The bot segment</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this segment to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator StartBotSegment(BotSegment botSegment, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Starting bot segment from path: " + botSegment.resourcePath);

            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
            var statusManager = Object.FindObjectOfType<BotSegmentPlaybackStatusManager>();

            playbackController.SetDataContainer(new BotSegmentsPlaybackContainer(new[] { botSegment }));

            playbackController.Play();

            yield return null; // Allow the recording to start playing
            var didTimeout = false;

            var startTime = Time.unscaledTime;
            while (playbackController.GetState() == PlayState.Playing || playbackController.GetState() == PlayState.Paused)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout)
                {
                    didTimeout = true;
                    break;
                }

                yield return null;
            }

            yield return null;
            RGDebug.LogInfo("Playback complete! - " + (didTimeout ? "TIMEOUT" : "SUCCESS"));
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip",
                success = !didTimeout,
                statusMessage = didTimeout?statusManager.LastError():null
            };
            setPlaybackResult(result);
            playbackController.Stop();
        }

        /**
         * <summary>
         * Plays back a bot segment list, and then returns the save location of the recording.
         * </summary>
         * <param name="botSegmentList">The bot segment list</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this segment to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator StartBotSegmentList(BotSegmentList botSegmentList, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Starting bot segment list from path: " + botSegmentList.resourcePath);

            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
            var statusManager = Object.FindObjectOfType<BotSegmentPlaybackStatusManager>();

            playbackController.SetDataContainer(new BotSegmentsPlaybackContainer(botSegmentList.segments));

            playbackController.Play();

            yield return null; // Allow the recording to start playing
            var didTimeout = false;

            var startTime = Time.unscaledTime;
            while (playbackController.GetState() == PlayState.Playing || playbackController.GetState() == PlayState.Paused)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout)
                {
                    didTimeout = true;
                    break;
                }

                yield return null;
            }

            yield return null;
            RGDebug.LogInfo("Playback complete! - " + (didTimeout ? "TIMEOUT" : "SUCCESS"));
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip",
                success = !didTimeout,
                statusMessage = didTimeout?statusManager.LastError():null
            };
            setPlaybackResult(result);
            playbackController.Stop();
        }

        /**
         * <summary>
         * Plays back a bot segment or segment list, and then returns the save location of the recording.
         * </summary>
         * <param name="segmentPath">The relative path to the bot segment or segment list</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this segment to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator StartBotSegmentOrSegmentList(string segmentPath, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Loading bot segment or segment list from path: " + segmentPath);

            var botSegmentInfo = BotSequence.LoadBotSegmentOrBotSegmentListFromPath(segmentPath);

            if (botSegmentInfo.Item3 is BotSegment botSegment)
            {
                yield return StartBotSegment(botSegment, setPlaybackResult, timeout);
            }
            else if (botSegmentInfo.Item3 is BotSegmentList botSegmentList)
            {
                yield return StartBotSegmentList(botSegmentList, setPlaybackResult, timeout);
            }
            else
            {
                yield return null;
            }
        }

        /**
         * <summary>
         * Plays back a bot sequence, and then returns the save location of the recording.
         * </summary>
         * <param name="botSequence">The bot sequence</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this sequence to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator StartBotSequence(BotSequence botSequence, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Starting bot sequence from path: " + botSequence.resourcePath);
            botSequence.Play();

            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
            var statusManager = Object.FindObjectOfType<BotSegmentPlaybackStatusManager>();

            yield return null; // Allow the recording to start playing
            var didTimeout = false;

            var startTime = Time.unscaledTime;
            while (playbackController.GetState() == PlayState.Playing || playbackController.GetState() == PlayState.Paused)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout)
                {
                    didTimeout = true;
                    break;
                }

                yield return null;
            }

            yield return null;
            RGDebug.LogInfo("Playback complete! - " + (didTimeout ? "TIMEOUT" : "SUCCESS"));
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip",
                success = !didTimeout,
                statusMessage = didTimeout?statusManager.LastError():null
            };
            setPlaybackResult(result);
            botSequence.Stop();
        }

        /**
         * <summary>
         * Plays back a bot sequence, and then returns the save location of the recording.
         * </summary>
         * <param name="sequencePath">The relative path to the bot sequence</param>
         * <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
         * <param name="timeout">How long in seconds to wait for this sequence to complete, &lt;= 0 means wait forever (default)</param>
         */
        public static IEnumerator StartBotSequence(string sequencePath, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            RGDebug.LogInfo("Loading bot sequence from path: " + sequencePath);

            var botSequenceInfo = BotSequence.LoadSequenceJsonFromPath(sequencePath);

            yield return StartBotSequence(botSequenceInfo.Item3, setPlaybackResult, timeout);
        }

    }
}
