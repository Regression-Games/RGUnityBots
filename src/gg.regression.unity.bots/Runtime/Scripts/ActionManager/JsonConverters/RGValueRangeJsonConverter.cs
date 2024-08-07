using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RegressionGames.ActionManager.JsonConverters
{
    public class RGValueRangeJsonConverter : JsonConverter
    {
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1_000));

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IRGValueRange valueRange = (IRGValueRange)value;
            _stringBuilder.Value.Clear();
            valueRange.WriteToStringBuilder(_stringBuilder.Value);
            writer.WriteRawValue(_stringBuilder.Value.ToString());
        }
        
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            RGValueRangeType rangeType = Enum.Parse<RGValueRangeType>(obj["type"].ToString());
            switch (rangeType)
            {
                case RGValueRangeType.RANGE_VOID:
                    return new RGVoidRange(obj);
                case RGValueRangeType.RANGE_BOOL:
                    return new RGBoolRange(obj);
                case RGValueRangeType.RANGE_INT:
                    return new RGIntRange(obj);
                case RGValueRangeType.RANGE_VECTOR2_INT:
                    return new RGVector2IntRange(obj);
                case RGValueRangeType.RANGE_VECTOR3_INT:
                    return new RGVector3IntRange(obj);
                case RGValueRangeType.RANGE_FLOAT:
                    return new RGFloatRange(obj);
                case RGValueRangeType.RANGE_VECTOR2:
                    return new RGVector2Range(obj);
                default:
                    throw new JsonSerializationException("invalid range type " + rangeType);
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(IRGValueRange).IsAssignableFrom(objectType);
        }
    }
}