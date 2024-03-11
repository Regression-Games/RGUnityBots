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
        // our primary testing project, bossroom, is from 'Unity' so we need to clarify our exclusions
        // (isBossRoom, isUnity)
        private readonly Dictionary<Assembly, (bool,bool)> _unityAssemblies = new();

        public override bool CanConvert(Type objectType)
        {
            var fullName = objectType.FullName;
            // we have added custom serializers for specific unity types
            if (NetworkVariableJsonConverter.Convertable(objectType) || NetworkVariableJsonConverter.NetworkObjectType == objectType)
            {
                return false;
            }

            var assembly = objectType.Assembly;
            if (!_unityAssemblies.TryGetValue(assembly, out var isUnityType))
            {
                var isUnity = fullName.StartsWith("Unity");
                var isBossRoom = isUnity && fullName.StartsWith("Unity.BossRoom");

                isUnityType = (isBossRoom, isUnity);
                _unityAssemblies[assembly] = isUnityType;
            }

            if (!isUnityType.Item1)
            {
                return isUnityType.Item2;
            }

            return false;
        }
    }
}
