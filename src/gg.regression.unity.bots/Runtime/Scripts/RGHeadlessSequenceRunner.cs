using System;
using System.Collections;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.Types;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming
namespace RegressionGames
{
    public static class RGHeadlessSequenceRunner
    {

        internal static readonly int Rc_RunSuccess = 0;
        internal static readonly int Rc_RunFailed = 1;
        internal static readonly int Rc_SequencePathMissing = 2;
        internal static readonly int Rc_OverlayNotInScene = 3;
        internal static readonly int Rc_SequenceLoadFailure = 4;

        internal static readonly string CommandLineArgument = "-rgSequencePath";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            var resourcePath = ParsePathArgument();
            if (resourcePath != null)
            {
                BotSegmentsPlaybackController playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
                if (playbackController == null)
                {
                    RGDebug.LogError($"RGOverlay must be in the first scene loaded for your game to utilize {CommandLineArgument} command line argument");
                    Application.Quit(Rc_OverlayNotInScene);
                }

                playbackController.gameObject.AddComponent<RGHeadlessSequenceRunnerBehaviour>();

                //ensure a keyboard exists for the input playback
                if (IsBatchMode())
                {

                }
            }
        }

        internal static bool IsBatchMode()
        {
            var args = Environment.GetCommandLineArgs();
            var argsLength = args.Length;
            for (var i = 0; i < argsLength; i++)
            {
                var arg = args[i];
                if (arg == "-batchmode")
                {
                    return true;
                }
            }

            return false;
        }

        internal static string ParsePathArgument()
        {
            var args = Environment.GetCommandLineArgs();
            var argsLength = args.Length;
            for (var i = 0; i < argsLength; i++)
            {
                var arg = args[i];
                if (arg == CommandLineArgument)
                {
                    if (i + 1 < argsLength)
                    {
                        return args[i + 1];
                    }
                    // else
                    RGDebug.LogError($"{CommandLineArgument} command line argument requires a path value to be passed after it");
                    Application.Quit(Rc_SequencePathMissing);
                }
            }

            return null;
        }

        /// <summary>
        /// Plays back a bot sequence, and then returns the save location of the recording.
        /// </summary>
        /// <param name="botSequenceInfo">(filePath,resourcePath,BotSequence) tuple of the bot sequence</param>
        /// <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
        /// <param name="timeout">How long in seconds to wait for this sequence to complete, less than or == 0 means wait forever (default=0)</param>
        internal static IEnumerator StartBotSequence((string,string, BotSequence) botSequenceInfo, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            var startTime = Time.unscaledTime;

            botSequenceInfo.Item3.Play();

            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();

            yield return null; // Allow the recording to start playing
            var didTimeout = false;
            while (playbackController.GetState() == PlayState.Playing || playbackController.GetState() == PlayState.Paused)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout )
                {
                    didTimeout = true;
                    break;
                }
                yield return null;
            }
            yield return null;
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip",
                success = !didTimeout
            };
            setPlaybackResult(result);
        }
    }

    public class RGHeadlessSequenceRunnerBehaviour : MonoBehaviour
    {
        private bool _started;

        private readonly Action<PlaybackResult> _result = (result) =>
        {
            if (result.success)
            {
                RGDebug.LogInfo("Regression Games bot sequence playback complete - SUCCESS");
                Application.Quit(RGHeadlessSequenceRunner.Rc_RunSuccess);
            }
            else
            {
                RGDebug.LogError("Regression Games bot sequence playback complete - FAILED");
                Application.Quit(RGHeadlessSequenceRunner.Rc_RunFailed);
            }

        };

        private void Update()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            var sequencePath = RGHeadlessSequenceRunner.ParsePathArgument();
            RGDebug.LogInfo("Regression Games bot sequence playback - loading and starting bot sequence from path: " + sequencePath);

            try
            {
                var botSequenceInfo = BotSequence.LoadSequenceJsonFromPath(sequencePath);
                StartCoroutine(RGHeadlessSequenceRunner.StartBotSequence(botSequenceInfo, _result));
            }
            catch (Exception ex)
            {
                RGDebug.LogException(ex, "Regression Games bot sequence playback - exception loading bot sequence from path: " + sequencePath);
                Application.Quit(RGHeadlessSequenceRunner.Rc_SequenceLoadFailure);
            }
        }



    }
}
