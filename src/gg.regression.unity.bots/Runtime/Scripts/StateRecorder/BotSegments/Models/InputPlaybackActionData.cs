using System;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;

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

        public bool IsCompleted()
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
