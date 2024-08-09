using System;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class CVWithinRectJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(CVWithinRect).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Null)
            {
                JObject jObject = JObject.Load(reader);
                // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
                CVWithinRect actionModel = new();
                actionModel.screenSize = jObject.GetValue("screenSize").ToObject<Vector2Int>(serializer);
                actionModel.rect = jObject.GetValue("rect").ToObject<RectInt>(serializer);
                return actionModel;
            }

            return null;
        }

        public static void WriteToStringBuilderNullable(StringBuilder stringBuilder, [CanBeNull] CVWithinRect rect)
        {
            if (rect == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                stringBuilder.Append("{\"screenSize\":");
                VectorIntJsonConverter.WriteToStringBuilder(stringBuilder, rect.screenSize);
                stringBuilder.Append(",\"rect\":");
                RectIntJsonConverter.WriteToStringBuilderNullable(stringBuilder, rect.rect);
                stringBuilder.Append("}");
            }
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
