using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class BoundsJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public static string ToJsonString(Bounds? val)
        {
            if (val != null)
            {
                var value = val.Value;
                var center = value.center;
                var extents = value.extents;
                return "{\"center\":{\"x\":"
                       + center.x
                       + ",\"y\":" + center.y
                       + ",\"z\":" + center.z
                       + "},\"extents\":{\"x\":"
                       + extents.x
                       + ",\"y\":" + extents.y
                       + ",\"z\":" + extents.z
                       + "}}";
            }

            return "null";
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
                writer.WriteRawValue(ToJsonString((Bounds)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Bounds) || objectType == typeof(Bounds?);
        }
    }
}
