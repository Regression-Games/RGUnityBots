using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class RectIntJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<RectInt>
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new (() => new(1000));

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, RectInt? f)
        {
            if (!f.HasValue)
            {
                stringBuilder.Append("null");
                return;
            }
            WriteToStringBuilder(stringBuilder, f.Value);
        }

        void ITypedStringBuilderConverter<RectInt>.WriteToStringBuilder(StringBuilder stringBuilder, RectInt val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<RectInt>.ToJsonString(RectInt val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, RectInt value)
        {
            stringBuilder.Append("{\"x\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, value.x);
            stringBuilder.Append(",\"y\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, value.y);
            stringBuilder.Append(",\"width\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, value.width);
            stringBuilder.Append(",\"height\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, value.height);
            stringBuilder.Append("}");
        }

        public static string ToJsonString(RectInt val)
        {
            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value, val);
            return _stringBuilder.Value.ToString();
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
                writer.WriteRawValue(ToJsonString((RectInt)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug without using this
            RectInt rect = new();
            rect.x = jObject["x"].ToObject<int>();
            rect.y = jObject["y"].ToObject<int>();
            rect.width = jObject["width"].ToObject<int>();
            rect.height = jObject["height"].ToObject<int>();
            return rect;
        }

        public override bool CanRead => true;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RectInt) || objectType == typeof(RectInt?);
        }
    }
}
