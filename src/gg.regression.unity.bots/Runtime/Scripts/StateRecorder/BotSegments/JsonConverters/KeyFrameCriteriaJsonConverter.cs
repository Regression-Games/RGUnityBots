using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class KeyFrameCriteriaJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(KeyFrameCriteria).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            KeyFrameCriteria criteria = new();
            criteria.transient = jObject["transient"].ToObject<bool>();
            criteria.type = jObject["type"].ToObject<KeyFrameCriteriaType>();
            IKeyFrameCriteriaData data = null;
            switch (criteria.type)
            {
                case KeyFrameCriteriaType.NormalizedPath:
                    data = jObject["data"].ToObject<PathKeyFrameCriteriaData>(serializer);
                    break;

                default:
                    throw new JsonSerializationException($"Unsupported KeyFrameCriteria type: '{criteria.type}'");
            }

            criteria.data = data;
            return criteria;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
