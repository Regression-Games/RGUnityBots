using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StateRecorder.BotSegments.Models.SegmentValidations;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public class ScriptSegmentValidationDataJsonConverter: JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(ScriptSegmentValidationData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug lines without this
            ScriptSegmentValidationData data = new();
            data.classFullName = jObject.GetValue("classFullName").ToObject<string>(serializer);
            if (jObject.ContainsKey("apiVersion"))
            {
                data.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
            }
            if (jObject.ContainsKey("timeout"))
            {
                data.timeout = jObject.GetValue("timeout").ToObject<float>(serializer);
            }
            return data;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}