using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class UnityObjectFallbackJsonConverter : Newtonsoft.Json.JsonConverter
    {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        // optimize to avoid string comparisons on every object
        private readonly Dictionary<Assembly, bool> _unityAssemblies = new();

        public override bool CanConvert(Type objectType)
        {
            var assembly = objectType.Assembly;
            if (_unityAssemblies.TryGetValue(assembly, out var isUnityType))
            {
                return isUnityType;
            }

            isUnityType = assembly.FullName.StartsWith("Unity");
            _unityAssemblies[assembly] = isUnityType;

            return isUnityType;
        }
    }
}
