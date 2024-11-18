using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.ActionManager;
using RegressionGames.GenericBots.Experimental;
using RegressionGames.StateRecorder.BotSegments.Models.BotActions;

namespace RegressionGames.StateRecorder.BotSegments.JsonConverters
{
    public sealed class QLearningActionDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(QLearningBotActionData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug lines without this
            QLearningBotActionData data = new();
            if (jObject.ContainsKey("apiVersion"))
            {
                data.apiVersion = jObject.GetValue("apiVersion").ToObject<int>();
            }
            data.actionInterval = jObject.GetValue("actionInterval").ToObject<float>(serializer);
            data.modelFilePath = jObject.GetValue("modelFilePath").ToObject<string>(serializer);
            data.actionSettings = jObject.GetValue("actionSettings").ToObject<RGActionManagerSettings>(serializer);
            data.rewardTypeRatios = jObject.GetValue("rewardTypeRatios").ToObject<Dictionary<RewardType, int>>();
            if (data.rewardTypeRatios.Count == 0)
            {
                throw new Exception("QLearningBotActionData.rewardTypeRatios MUST contain at least one RewardType");
            }

            // training options
            if (jObject.ContainsKey("training"))
            {
                data.training = jObject.GetValue("training").ToObject<bool>(serializer);
            }
            if (jObject.ContainsKey("trainingTimeScale"))
            {
                data.trainingTimeScale = jObject.GetValue("trainingTimeScale").ToObject<float>(serializer);
            }
            return data;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
