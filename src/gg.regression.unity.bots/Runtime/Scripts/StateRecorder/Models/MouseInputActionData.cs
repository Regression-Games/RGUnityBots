using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [JsonConverter(typeof(MouseInputActionDataJsonConverter))]
    public class MouseInputActionData
    {
        // version of this schema, update this if fields change
        public int apiVersion = SdkApiVersion.VERSION_1;

        public double startTime;

        public Vector2Int screenSize;

        public Vector2Int position;

        public Vector3? worldPosition;

        // non-fractional pixel accuracy
        //main 5 buttons
        public bool leftButton;
        public bool middleButton;
        public bool rightButton;
        public bool forwardButton;
        public bool backButton;

        // scroll wheel
        public Vector2 scroll;

        public string[] clickedObjectNormalizedPaths;

        public bool IsButtonClicked => leftButton || middleButton || rightButton || forwardButton || backButton || Math.Abs(scroll.y) > 0.1f || Math.Abs(scroll.x) > 0.1f;

        public bool PositionsEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return (previous.position.x) == (this.position.x)
                       && (previous.position.y) == (this.position.y);
            }

            return false;
        }

        public bool ButtonStatesEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return previous.leftButton == this.leftButton
                       && previous.middleButton == this.middleButton
                       && previous.rightButton == this.rightButton
                       && previous.forwardButton == this.forwardButton
                       && previous.backButton == this.backButton
                       && Math.Abs(previous.scroll.y - this.scroll.y) < 0.1f
                       && Math.Abs(previous.scroll.x - this.scroll.x) < 0.1f;
            }

            return false;
        }

        public void ReplayReset()
        {
            Replay_IsDone = false;
            Replay_OffsetTime = 0;
        }

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(5_000));

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"startTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, startTime);
            stringBuilder.Append(",\"screenSize\":");
            Vector2IntJsonConverter.WriteToStringBuilder(stringBuilder, screenSize);
            stringBuilder.Append(",\"position\":");
            Vector2IntJsonConverter.WriteToStringBuilder(stringBuilder, position);
            stringBuilder.Append(",\"worldPosition\":");
            Vector3JsonConverter.WriteToStringBuilderNullable(stringBuilder, worldPosition);
            stringBuilder.Append(",\"leftButton\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder,leftButton);
            stringBuilder.Append(",\"middleButton\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder,middleButton);
            stringBuilder.Append(",\"rightButton\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder,rightButton);
            stringBuilder.Append(",\"forwardButton\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder,forwardButton);
            stringBuilder.Append(",\"backButton\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder,backButton);
            stringBuilder.Append(",\"scroll\":");
            Vector2JsonConverter.WriteToStringBuilder(stringBuilder, scroll);
            stringBuilder.Append(",\"clickedObjectNormalizedPaths\":[");
            var clickedObjectPathsLength = clickedObjectNormalizedPaths.Length;
            for (var i = 0; i < clickedObjectPathsLength; i++)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, clickedObjectNormalizedPaths[i]);
                if (i + 1 < clickedObjectPathsLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]}");
        }

        internal string ToJsonString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }

        //gives the position relative to the current screen size
        public Vector2Int NormalizedPosition => new()
        {
            x = (int)(position.x * (Screen.width / (float)screenSize.x)),
            y = (int)(position.y * (Screen.height / (float)screenSize.y))
        };

        //Replay Only
        [NonSerialized]
        public bool Replay_IsDone;

        //Replay Only
        [NonSerialized]
        public double Replay_OffsetTime;

        // Replay only - used for logging
        [NonSerialized]
        public int Replay_SegmentNumber;

        // Replay only
        public double Replay_StartTime => startTime + Replay_OffsetTime;

    }

    public class MouseInputActionDataJsonConverter : JsonConverter<MouseInputActionData>
    {
        public override void WriteJson(JsonWriter writer, MouseInputActionData value, JsonSerializer serializer)
        {
            writer.WriteRawValue(value.ToJsonString());
        }

        public override bool CanRead => false;

        public override MouseInputActionData ReadJson(JsonReader reader, Type objectType, MouseInputActionData existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
