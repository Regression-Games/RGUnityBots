using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
namespace RegressionGames.StateRecorder.JsonConverters
{
    public class NetworkVariableJsonConverter : Newtonsoft.Json.JsonConverter, IStringBuilderConverter
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(500));


        // only works if they include this type in their runtime
        public static readonly Type NetworkVariableType = Type.GetType("Unity.Netcode.NetworkVariable`1, Unity.Netcode.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", throwOnError: false);
        // network behaviours already expose the networkobject data.. exposing it again is duplication in the json
        //public static readonly Type NetworkObjectType = Type.GetType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", throwOnError: false);

        public void WriteToStringBuilder(StringBuilder stringBuilder, object value)
        {
            var fieldValue = value.GetType().GetProperty("Value")?.GetValue(value);
            stringBuilder.Append("{\"Value\":");
            JsonUtils.WriteObjectStateToStringBuilder(stringBuilder, fieldValue);
            stringBuilder.Append("}");
        }

        public string ToJsonString(object val)
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
                writer.WriteRawValue(ToJsonString(value));
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
