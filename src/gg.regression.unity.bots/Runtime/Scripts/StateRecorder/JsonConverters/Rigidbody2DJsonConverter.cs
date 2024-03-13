using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class Rigidbody2DJsonConverter : Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                var val = (Rigidbody2D)value;
                // raw is way faster than using the libraries
                writer.WriteRawValue("{\"position\":" + val.position
                                                      + ",\"rotation\":" + val.rotation
                                                      + ",\"velocity\":" + val.velocity
                                                      + ",\"mass\":" + val.mass
                                                      + ",\"drag\":" + val.drag
                                                      + ",\"angularDrag\":" + val.angularDrag
                                                      + ",\"gravityScale\":" + val.gravityScale
                                                      + ",\"isKinematic\":" + val.isKinematic.ToString().ToLower() + "}");
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Rigidbody2D);
        }
    }
}
