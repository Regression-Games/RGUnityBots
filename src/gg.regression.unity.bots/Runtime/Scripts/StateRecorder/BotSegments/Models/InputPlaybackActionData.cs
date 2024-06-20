using System;
using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    [Serializable]
    public class InputPlaybackActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = BotSegment.SDK_API_VERSION_1;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.InputPlayback;
        /**
         * <summary>Used to sync up with input event times on replay to playback at proper timings.  This is the the time of the prior key frame so that we can compute the time delay to play each input once we get to this key frame.</summary>
         */
        public double startTime;
        public InputData inputData;

        public void StartAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms)
        {
            RGDebug.LogInfo($"({segmentNumber}) - Bot Segment - Processing InputPlaybackActionData");

            var now = Time.unscaledTime;
            var currentInputTimePoint = now - startTime;

            foreach (var keyboardInputActionData in inputData.keyboard)
            {
                keyboardInputActionData.Replay_OffsetTime = currentInputTimePoint;
            }

            foreach (var mouseInputActionData in inputData.mouse)
            {
                mouseInputActionData.Replay_OffsetTime = currentInputTimePoint;
            }
        }

        public bool ProcessAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms, out string error)
        {
            var result = false;
            var currentTime = Time.unscaledTime;
            foreach (var replayKeyboardInputEntry in inputData.keyboard)
            {
                if (!replayKeyboardInputEntry.Replay_StartEndSentFlags[0] && currentTime >= replayKeyboardInputEntry.Replay_StartTime)
                {
                    // send start event
                    result = true;
                    KeyboardEventSender.SendKeyEvent(segmentNumber, replayKeyboardInputEntry, KeyState.Down);
                    replayKeyboardInputEntry.Replay_StartEndSentFlags[0] = true;
                }

                if (!replayKeyboardInputEntry.Replay_StartEndSentFlags[1] && currentTime >= replayKeyboardInputEntry.Replay_EndTime)
                {
                    // send end event
                    result = true;
                    KeyboardEventSender.SendKeyEvent(segmentNumber, replayKeyboardInputEntry, KeyState.Up);
                    replayKeyboardInputEntry.Replay_StartEndSentFlags[1] = true;
                }
            }

            foreach (var replayMouseInputEntry in inputData.mouse)
            {
                if (!replayMouseInputEntry.Replay_IsDone && currentTime >= replayMouseInputEntry.Replay_StartTime)
                {
                    //Need the statuses for the mouse to click correctly when things move a bit or resolution changes
                    var uiTransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame();
                    var gameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame();

                    // send event
                    result = true;
                    MouseEventSender.SendMouseEvent(segmentNumber, replayMouseInputEntry, uiTransforms.Item1, gameObjectTransforms.Item1, uiTransforms.Item2, gameObjectTransforms.Item2);
                    replayMouseInputEntry.Replay_IsDone = true;
                }
            }

            error = null;
            return result;
        }

        public bool? IsCompleted()
        {
            foreach (var keyboardInputActionData in inputData.keyboard)
            {
                if (!keyboardInputActionData.Replay_IsDone)
                {
                    return false;
                }
            }

            foreach (var mouseInputActionData in inputData.mouse)
            {
                if (!mouseInputActionData.Replay_IsDone)
                {
                    return false;
                }
            }
            return true;
        }

        public void ReplayReset()
        {
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
