﻿using System;
using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions
{
    [Serializable]
    public class InputPlaybackActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_1;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.InputPlayback;
        /**
         * <summary>Used to sync up with input event times on replay to playback at proper timings.  This is the the time of the prior key frame so that we can compute the time delay to play each input once we get to this key frame.</summary>
         */
        public double startTime;
        public InputData inputData;

        private bool _isStopped;

        private float pauseTime = -1f;

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                RGDebug.LogInfo($"({segmentNumber}) - Bot Segment - Processing InputPlaybackActionData");

                var now = Time.unscaledTime;
                var currentInputTimePoint = now - startTime;

                if (inputData.keyboard != null)
                {
                    foreach (var keyboardInputActionData in inputData.keyboard)
                    {
                        keyboardInputActionData.Replay_OffsetTime = currentInputTimePoint;
                    }
                }

                if (inputData.mouse != null)
                {
                    foreach (var mouseInputActionData in inputData.mouse)
                    {
                        mouseInputActionData.Replay_OffsetTime = currentInputTimePoint;
                    }
                }
            }
        }

        public void PauseAction(int segmentNumber)
        {
            pauseTime = Time.unscaledTime;
        }

        public void UnPauseAction(int segmentNumber)
        {
            // reset the times to get the right spacing of inputs for any that haven't finished yet
            if (pauseTime > 0)
            {
                var now = Time.unscaledTime;
                var delta = now - pauseTime;

                if (inputData.keyboard != null)
                {
                    foreach (var keyboardInputActionData in inputData.keyboard)
                    {
                        if (!keyboardInputActionData.Replay_IsDone)
                        {
                            keyboardInputActionData.Replay_OffsetTime += delta;
                        }
                    }
                }

                if (inputData.mouse != null)
                {
                    foreach (var mouseInputActionData in inputData.mouse)
                    {
                        if (!mouseInputActionData.Replay_IsDone)
                        {
                            mouseInputActionData.Replay_OffsetTime += delta;
                        }
                    }
                }
            }

            pauseTime = -1f;
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            var result = false;

            if (!_isStopped)
            {
                var currentTime = Time.unscaledTime;

                var allInputs = inputData.AllInputsSortedByTime();

                HashSet<Key> keysSeenThisUpdate = new();
                bool stopKeyboard = false;

                foreach (object input in allInputs)
                {
                    if (!stopKeyboard && input is KeyboardInputActionData replayKeyboardInputEntry)
                    {
                        // make sure not to send 2 events for the same key in the same frame Update
                        if (keysSeenThisUpdate.Contains(replayKeyboardInputEntry.Key))
                        {
                            stopKeyboard = true;
                            continue;
                        }
                        // if we don't have one of the times, mark that event send as already 'done' so we don't send it
                        if (!replayKeyboardInputEntry.Replay_StartTime.HasValue)
                        {
                            replayKeyboardInputEntry.Replay_StartEndSentFlags[0] = true;
                        }

                        if (!replayKeyboardInputEntry.Replay_EndTime.HasValue)
                        {
                            replayKeyboardInputEntry.Replay_StartEndSentFlags[1] = true;
                        }

                        // cannot send start and end on same input update for the same key.. that is why these are reversed for safety
                        if (replayKeyboardInputEntry.Replay_StartEndSentFlags[0] && !replayKeyboardInputEntry.Replay_StartEndSentFlags[1] && currentTime >= replayKeyboardInputEntry.Replay_EndTime)
                        {
                            // send end event
                            result = true;
                            KeyboardEventSender.SendKeyEvent(segmentNumber, replayKeyboardInputEntry.Key, KeyState.Up);
                            keysSeenThisUpdate.Add(replayKeyboardInputEntry.Key);
                            replayKeyboardInputEntry.Replay_StartEndSentFlags[1] = true;
                        }

                        if (!replayKeyboardInputEntry.Replay_StartEndSentFlags[0] && currentTime >= replayKeyboardInputEntry.Replay_StartTime)
                        {
                            // send start event
                            result = true;
                            KeyboardEventSender.SendKeyEvent(segmentNumber, replayKeyboardInputEntry.Key, KeyState.Down);
                            keysSeenThisUpdate.Add(replayKeyboardInputEntry.Key);
                            replayKeyboardInputEntry.Replay_StartEndSentFlags[0] = true;
                        }
                    }

                    if (input is MouseInputActionData replayMouseInputEntry)
                    {
                        {
                            if (!replayMouseInputEntry.Replay_IsDone && currentTime >= replayMouseInputEntry.Replay_StartTime)
                            {
                                // send event
                                result = true;

                                //TODO (REG-2237) : Replace with this finding the object and sending the raw position mouse event
                                MouseEventSender.SendMouseEvent(segmentNumber, replayMouseInputEntry, null, null, currentTransforms, currentEntities);
                                replayMouseInputEntry.Replay_IsDone = true;
                            }
                        }
                    }

                }
            }

            error = null;
            return result;
        }

        public void AbortAction(int segmentNumber)
        {
            _isStopped = true;
        }

        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // for input playback, we finish the action queue even if criteria match before hand (no-op)
        }

        public bool IsCompleted()
        {
            if (!_isStopped)
            {
                if (inputData.keyboard != null)
                {
                    foreach (var keyboardInputActionData in inputData.keyboard)
                    {
                        if (!keyboardInputActionData.Replay_IsDone)
                        {
                            return false;
                        }
                    }
                }

                if (inputData.mouse != null)
                {
                    foreach (var mouseInputActionData in inputData.mouse)
                    {
                        if (!mouseInputActionData.Replay_IsDone)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void ReplayReset()
        {
            _isStopped = false;
            inputData.ReplayReset();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"startTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, startTime);
            stringBuilder.Append(",\n\"inputData\":");
            inputData.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }

        public int EffectiveApiVersion()
        {
            return Math.Max(apiVersion, inputData.EffectiveApiVersion);
        }
    }
}
