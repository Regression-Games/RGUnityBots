using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Animations.Rigging;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class AnimationRigJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(1_000);

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Rig val)
        {
            stringBuilder.Append("{\"weight\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, val.weight);
            stringBuilder.Append(",\"effectors\":[\r\n");
            var list = val.effectors.ToList();
            var listCount = list.Count;
            for (var i = 0; i < listCount; i++)
            {
                var rigEffectorData = list[i];
                stringBuilder.Append("{\"style\":");
                AnimationStyleJsonConverter.WriteToStringBuilder(stringBuilder, rigEffectorData.style);
                stringBuilder.Append(",\"visible\":");
                stringBuilder.Append(rigEffectorData.visible ? "true" : "false");
                stringBuilder.Append("}");
                if (i + 1 < listCount)
                {
                    stringBuilder.Append(",\r\n");
                }
            }
            stringBuilder.Append("]}");
        }

        private static string ToJsonString(Rig value)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, value);
            return _stringBuilder.ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                // raw is way faster than using the libraries
                writer.WriteRawValue(ToJsonString((Rig)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Rig);
        }
    }
}
