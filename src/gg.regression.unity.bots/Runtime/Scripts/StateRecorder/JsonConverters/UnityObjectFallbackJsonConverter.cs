using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

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
                writer.WriteRawValue("{}");
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
        private static readonly Dictionary<Assembly, (bool, bool)> _unityAssemblies = new();

        public static bool Convertable(Type objectType)
        {
            if (InGameObjectFinder.GetInstance().collectStateFromBehaviours == false &&
                objectType == typeof(Behaviour))
            {
                // in this case, any Behaviour should just be empty unless it has a custom converter
                return true;
            }
            var fullName = objectType.FullName;
            // we have added custom serializers for specific unity types
            if (NetworkVariableJsonConverter.Convertable(objectType))
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

            // let the default object serializer do its thing
            return false;
        }

        public override bool CanConvert(Type objectType)
        {
            return Convertable(objectType);
        }
    }
}
