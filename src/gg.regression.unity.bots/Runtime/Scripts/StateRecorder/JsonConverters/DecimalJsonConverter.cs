﻿using System;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class DecimalJsonConverter: JsonConverter
    {
        public static string ToJsonString(decimal f)
        {
            var val = (int)f;
            var remainder = (int)((f % 1) * 10_000_000);
            // write to fixed precision of 7 decimal places
            return remainder > 0 ? val +"." + remainder : val.ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var f = (decimal)value;
            writer.WriteRawValue(ToJsonString(f));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal) || objectType == typeof(Decimal);
        }
    }
}
