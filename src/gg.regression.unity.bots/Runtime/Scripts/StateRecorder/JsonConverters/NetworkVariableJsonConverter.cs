using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class NetworkVariableJsonConverter : Newtonsoft.Json.JsonConverter
    {
        // only works if they include this type in their runtime
        public static readonly Type NetworkVariableType = Type.GetType("Unity.Netcode.NetworkVariable`1, Unity.Netcode.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", throwOnError: false);
        public static readonly Type NetworkObjectType = Type.GetType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", throwOnError: false);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var fieldValue = value.GetType().GetProperty("Value")?.GetValue(value);
                writer.WriteRawValue("{\"Value\":" + JsonConvert.ToString(fieldValue) + "}");
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public static bool Convertable(Type objectType)
        {
            // only works if they include the network code in their runtime
            if (NetworkVariableType == null)
            {
                return false;
            }

            // handle nullable
            if (objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                objectType = Nullable.GetUnderlyingType(objectType);
            }

            if (objectType.IsGenericType && objectType.GetGenericTypeDefinition() == NetworkVariableType)
            {
                return true;
            }

            return false;
        }

        public override bool CanConvert(Type objectType)
        {
            return Convertable(objectType);
        }
    }
}
