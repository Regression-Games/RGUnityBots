using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.RemoteOrchestration.Models;

namespace RegressionGames.RemoteOrchestration.JsonConverters
{
    public class WorkAssignmentJsonConverter: Newtonsoft.Json.JsonConverter
    {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanWrite is false. The type will skip the converter.");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug code without it
            WorkAssignment result = new WorkAssignment();
            result.id = jObject["id"].ToObject<long>();
            result.resourcePath = jObject["resourcePath"].ToObject<string>();
            if (jObject.TryGetValue("startTime", out var st))
            {
                var stString = st.ToObject<string>();
                try
                {
                    DateTime startTime = DateTime.Parse(stString);
                    result.startTime = startTime;
                }
                catch (Exception ex)
                {
                    RGDebug.LogException(ex, "Exception parsing 'startTime' for new WorkAssignment, starting immediately...");
                }
            }

            if (jObject.TryGetValue("timeout", out var timeout))
            {
                result.timeout = timeout.ToObject<int>();
            }

            if (jObject.TryGetValue("status", out var status))
            {
                // the only status that the server can override us to is CANCELLED, all
                // others are client driven
                var waStatus = Enum.Parse<WorkAssignmentStatus>(status.ToObject<string>());
                result.status = waStatus;
            }
            return result;
        }

        public override bool CanWrite => false;

        public override bool CanRead => true;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(WorkAssignment);
        }
    }
}
