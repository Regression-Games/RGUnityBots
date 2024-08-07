using System;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RegressionGames.ActionManager.JsonConverters
{
    public class RGActionParamFuncJsonConverter : JsonConverter
    {
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1_000));
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IRGActionParamFunc func = (IRGActionParamFunc)value;
            _stringBuilder.Value.Clear();
            func.WriteToStringBuilder(_stringBuilder.Value);
            writer.WriteRawValue(_stringBuilder.Value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            var deserializeMethod = objectType.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
            return deserializeMethod.Invoke(null, new object[] { obj });
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(IRGActionParamFunc).IsAssignableFrom(objectType);
        }
    }
}