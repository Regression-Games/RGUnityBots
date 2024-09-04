using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public class CVMouseActionDetails
    {
        public int apiVersion = SdkApiVersion.VERSION_10;

        //main 5 buttons
        public bool leftButton;
        public bool middleButton;
        public bool rightButton;
        public bool forwardButton;
        public bool backButton;

        // scroll wheel
        public Vector2 scroll;

        /**
         * Minimum duration for this action before moving to the next action.  This can be utilized to ensure that a button is held for a minimum amount of time before moving to the next action.  If <= 0, then the action is instant (1 frame).
         */
        public float duration = 0f;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
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
            stringBuilder.Append(",\"duration\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, duration);
            stringBuilder.Append("}");
        }

        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1000));

        public override string ToString()
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value);
            return _stringBuilder.Value.ToString();
        }
    }
}
