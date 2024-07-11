using System;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RegressionGames.ActionManager.JsonConverters
{
    public class RGGameActionJsonConverter : JsonConverter
    {
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(4_000));
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            RGGameAction action = (RGGameAction)value;
            _stringBuilder.Value.Clear();
            action.WriteToStringBuilder(_stringBuilder.Value);
            writer.WriteRawValue(_stringBuilder.Value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            Type actionType = Type.GetType(obj["actionTypeName"].ToString(), true);
            var constructor = actionType.GetConstructor(new[] { typeof(JObject) });
            if (constructor == null)
            {
                throw new Exception($"Missing deserialization constructor for {actionType.FullName}");
            }
            return (RGGameAction)constructor.Invoke(new object[] { obj });
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(RGGameAction).IsAssignableFrom(objectType);
        }
    }
}