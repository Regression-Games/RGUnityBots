﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [JsonConverter(typeof(KeyboardInputActionDataJsonConverter))]
    public class KeyboardInputActionData
    {
        // version of this schema, update this if fields change
        public int apiVersion = BotSegment.SDK_API_VERSION_1;

        public double startTime;
        public string action;
        public string binding;
        public double? endTime;

        [NonSerialized]
        public double duration;

        [NonSerialized]
        public double? lastSentUpdateTime;

        [NonSerialized]
        public double lastUpdateTime;

        public bool isPressed => duration > 0 && endTime == null;

        // re-usable and large enough to fit ball sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(5_000));

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"startTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, startTime);
            stringBuilder.Append(",\"action\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, action);
            stringBuilder.Append(",\"binding\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, binding);
            stringBuilder.Append(",\"endTime\":");
            DoubleJsonConverter.WriteToStringBuilderNullable(stringBuilder, endTime);
            stringBuilder.Append(",\"isPressed\":");
            stringBuilder.Append(isPressed ? "true" : "false");
            stringBuilder.Append("}");
        }

        internal string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        public void ReplayReset()
        {
            Replay_StartEndSentFlags[0] = false;
            Replay_StartEndSentFlags[1] = false;
            Replay_OffsetTime = 0;
        }

        // Replay only - used to track if we have sent the start and end events for this entry yet
        [NonSerialized]
        public readonly bool[] Replay_StartEndSentFlags = { false, false };

        // Replay only
        [NonSerialized]
        public double Replay_OffsetTime;

        // Replay only
        public double Replay_StartTime => startTime + Replay_OffsetTime;

        // Replay only
        public double? Replay_EndTime => endTime + Replay_OffsetTime;

        // Replay only - have we finished processing this input
        public bool Replay_IsDone => Replay_StartEndSentFlags[0] && (endTime == null || Replay_StartEndSentFlags[1]);

        // Replay only
        public Key Key => KeyboardInputActionObserver.AllKeyboardKeys[binding.Substring(binding.LastIndexOf('/') + 1)];
    }

    public class KeyboardInputActionDataJsonConverter : JsonConverter<KeyboardInputActionData>
    {
        public override void WriteJson(JsonWriter writer, KeyboardInputActionData value, JsonSerializer serializer)
        {
            writer.WriteRawValue(value.ToJsonString());
        }

        public override bool CanRead => false;

        public override KeyboardInputActionData ReadJson(JsonReader reader, Type objectType, KeyboardInputActionData existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
