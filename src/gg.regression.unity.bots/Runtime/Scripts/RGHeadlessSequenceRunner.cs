using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.InputSystem;
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
        internal static readonly int Rc_SequenceTimeoutNeedsPath = 5;
        internal static readonly int Rc_SequenceTimeoutMissing = 6;
        internal static readonly int Rc_SequenceTimeoutNotInt = 7;
        internal static readonly int Rc_SequencePathNotRelative = 8;

        internal static readonly string SequencePathArgument = "-rgsequencepath";
        internal static readonly string SequenceTimeoutArgument = "-rgsequencetimeout";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void FirstLoadChecks()
        {
            // do this here to fail fast on bad args
            ParsePathArgument();
            ParseTimeoutArgument();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            var resourcePath = ParsePathArgument();
            if (resourcePath != null)
            {
                BotSegmentsPlaybackController playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
                if (playbackController == null)
                {
                    RGDebug.LogError($"RGOverlay must be in the first scene loaded for your game to utilize {SequencePathArgument} command line argument");
                    Application.Quit(Rc_OverlayNotInScene);
                }

                playbackController.gameObject.AddComponent<RGHeadlessSequenceRunnerBehaviour>();

                //ensure a keyboard exists for the input playback
                if (Application.isBatchMode)
                {
                    var currentKeyboard = Keyboard.current;
                    // this should always be null as -batchmode has no keyboard.. but if a game already has their own virtual keyboard for some reason, we don't need to make another
                    if (currentKeyboard == null)
                    {
                        var virtualKeyboard = InputSystem.devices.FirstOrDefault(a => a.name == "RGVirtualKeyboard");

                        if (virtualKeyboard == null)
                        {
                            virtualKeyboard = InputSystem.AddDevice<Keyboard>("RGVirtualKeyboard");
                        }

                        if (virtualKeyboard != null)
                        {
                            if (!virtualKeyboard.enabled)
                            {
                                InputSystem.EnableDevice(virtualKeyboard);
                            }

                            if (!virtualKeyboard.canRunInBackground)
                            {
                                // Forcibly allow the virtual keyboard to send events while the application is backgrounded
                                // Note that if the user continues creating keyboard events while outside the application, this could still interfere
                                // with the game if it is reading keyboard input via the Input System.
                                var deviceFlagsField = virtualKeyboard.GetType().GetField("m_DeviceFlags", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (deviceFlagsField != null)
                                {
                                    int canRunInBackground = 1 << 11;
                                    int canRunInBackgroundHasBeenQueried = 1 << 12;
                                    var deviceFlags = (int)deviceFlagsField.GetValue(virtualKeyboard);
                                    deviceFlags |= canRunInBackground;
                                    deviceFlags |= canRunInBackgroundHasBeenQueried;
                                    deviceFlagsField.SetValue(virtualKeyboard, deviceFlags);
                                }
                                else
                                {
                                    RGDebug.LogWarning("Unable to set device canRunInBackground flags for virtual Keyboard");
                                }
                            }
                        }
                    }
                }
            }
        }

        internal static string ParsePathArgument()
        {
            var args = Environment.GetCommandLineArgs();
            var argsLength = args.Length;
            for (var i = 0; i < argsLength; i++)
            {
                var arg = args[i];
                if (arg == SequencePathArgument)
                {
                    if (i + 1 < argsLength)
                    {

                        var path = args[i + 1];

                        // Sequences that are run via -rgsequencepath must be relative, as we only allows sequences
                        // included in the Resource or persistent path directories.
                        if (Path.IsPathRooted(path))
                        {
                            RGDebug.LogError($"{SequencePathArgument} command line argument requires a relative path");
                            Application.Quit(Rc_SequencePathNotRelative);
                            return null;
                        }

                        return path;
                    }
                    // else
                    RGDebug.LogError($"{SequencePathArgument} command line argument requires a path value to be passed after it");
                    Application.Quit(Rc_SequencePathMissing);
                }
            }

            return null;
        }

        internal static int ParseTimeoutArgument()
        {
            var args = Environment.GetCommandLineArgs();
            var argsLength = args.Length;
            for (var i = 0; i < argsLength; i++)
            {
                var arg = args[i];
                if (arg == SequenceTimeoutArgument)
                {
                    var pathArgument = ParsePathArgument();
                    if (pathArgument == null)
                    {
                        RGDebug.LogError($"{SequenceTimeoutArgument} command line argument requires {SequencePathArgument} to also be specified");
                        Application.Quit(Rc_SequenceTimeoutNeedsPath);
                    }

                    if (i + 1 < argsLength)
                    {
                        var nextArg = args[i + 1];

                        if (!int.TryParse(nextArg, out var timeout))
                        {
                            RGDebug.LogError($"{SequenceTimeoutArgument} command line argument requires an integer timeout value to be passed after it");
                            Application.Quit(Rc_SequenceTimeoutNotInt);
                        }

                        return timeout;
                    }
                    // else
                    RGDebug.LogError($"{SequenceTimeoutArgument} command line argument requires an integer timeout value to be passed after it");
                    Application.Quit(Rc_SequenceTimeoutMissing);
                }
            }

            return 0;
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

            var replayToolbar = Object.FindObjectOfType<ReplayToolbarManager>();
            if (replayToolbar != null)
            {
                replayToolbar.SetInUseButtonStates();
            }

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
            botSequenceInfo.Item3.Stop();
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
            var sequenceTimeout = RGHeadlessSequenceRunner.ParseTimeoutArgument();
            RGDebug.LogInfo("Regression Games bot sequence playback - loading and starting bot sequence from path: " + sequencePath + " , with timeout: " + (sequenceTimeout<=0 ? "NONE":sequenceTimeout));

            try
            {
                var botSequenceInfo = BotSequence.LoadSequenceJsonFromPath(sequencePath);
                StartCoroutine(RGHeadlessSequenceRunner.StartBotSequence(botSequenceInfo, _result, sequenceTimeout));
            }
            catch (Exception ex)
            {
                RGDebug.LogException(ex, "Regression Games bot sequence playback - exception loading bot sequence from path: " + sequencePath);
                Application.Quit(RGHeadlessSequenceRunner.Rc_SequenceLoadFailure);
            }
        }



    }
}
