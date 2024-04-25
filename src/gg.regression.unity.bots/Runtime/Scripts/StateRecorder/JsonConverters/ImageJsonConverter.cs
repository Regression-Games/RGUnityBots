using System;
using System.Text;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class ImageJsonConverter : Newtonsoft.Json.JsonConverter
    {

        // re-usable and large enough to fit images of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(500);

        public static void WriteToStringBuilder(StringBuilder stringBuilder, Image val)
        {
            if (val == null)
            {
                stringBuilder.Append("null");
                return;
            }

            stringBuilder.Append("{\"sourceImage\":");
            stringBuilder.Append((val.sprite == null ? "null":JsonUtils.EscapeJsonString(val.sprite.name)));
            stringBuilder.Append(",\"color\":");
            ColorJsonConverter.WriteToStringBuilder(stringBuilder, val.color);
            stringBuilder.Append(",\"material\":");
            stringBuilder.Append((val.material == null ? "null":JsonUtils.EscapeJsonString(val.material.name)));
            stringBuilder.Append(",\"raycastTarget\":");
            stringBuilder.Append((val.raycastTarget ? "true" : "false"));
            stringBuilder.Append(",\"preserveAspect\":");
            stringBuilder.Append((val.preserveAspect ? "true" : "false"));
            stringBuilder.Append("}");
        }

        private static string ToJsonString(Image val)
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, val);
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
                writer.WriteRawValue(ToJsonString((Image)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Image);
        }
    }
}
