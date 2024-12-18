using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;
using StateRecorder.BotSegments.Models;
using StateRecorder.BotSegments.Models.SegmentValidations;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public class SegmentValidationJsonConverter: JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(SegmentValidation).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            SegmentValidation validation = new();
            validation.type = jObject["type"].ToObject<SegmentValidationType>(serializer);
            if (jObject.ContainsKey("apiVersion"))
            {
                validation.apiVersion = jObject["apiVersion"].ToObject<int>(serializer);
            }
            IRGSegmentValidationData data = null;
            switch (validation.type)
            {
                case SegmentValidationType.Script:
                    data = jObject["data"].ToObject<ScriptSegmentValidationData>(serializer);
                    break;
                default:
                    throw new JsonSerializationException($"Unsupported SegmentValidation type: '{validation.type}'");
            }

            validation.data = data;
            return validation;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}