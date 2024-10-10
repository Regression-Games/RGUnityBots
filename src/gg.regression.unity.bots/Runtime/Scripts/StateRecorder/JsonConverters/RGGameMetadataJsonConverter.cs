using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.BotSegments.Models.AIService;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class RGGameMetadataJsonConverter : JsonConverter, ITypedStringBuilderConverter<RGGameMetadata>
    {

        // re-usable and large enough to fit all sizes
        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(1000));

        void ITypedStringBuilderConverter<RGGameMetadata>.WriteToStringBuilder(StringBuilder stringBuilder, RGGameMetadata val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<RGGameMetadata>.ToJsonString(RGGameMetadata val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, RGGameMetadata val)
        {
            val.WriteToStringBuilder(stringBuilder);
        }

        private static string ToJsonString(RGGameMetadata val)
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
                // raw is way faster than using the libraries
                writer.WriteRawValue(ToJsonString((RGGameMetadata)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            // ReSharper disable once UseObjectOrCollectionInitializer - easier to debug when on separate lines
            RGGameMetadata gameMetadata = new();
            gameMetadata.apiVersion = jObject.GetValue("apiVersion").ToObject<int>(serializer);
            gameMetadata.unityVersion = jObject.GetValue("unityVersion")?.ToObject<string>(serializer);
            gameMetadata.runtimePlatform = jObject.GetValue("runtimePlatform")?.ToObject<RuntimePlatform>(serializer) ?? RuntimePlatform.WindowsEditor;
            gameMetadata.isEditor = jObject.GetValue("isEditor")?.ToObject<bool>(serializer) ?? false;
            gameMetadata.productName = jObject.GetValue("productName")?.ToObject<string>(serializer);
            gameMetadata.companyName = jObject.GetValue("companyName")?.ToObject<string>(serializer);
            gameMetadata.gameVersion = jObject.GetValue("gameVersion")?.ToObject<string>(serializer);
            gameMetadata.gameBuildGUID = jObject.GetValue("gameBuildGUID")?.ToObject<string>(serializer);
            gameMetadata.systemLanguage = jObject.GetValue("systemLanguage")?.ToObject<SystemLanguage>(serializer) ?? SystemLanguage.English;
            gameMetadata.allConfiguredRenderPipelines = jObject.GetValue("allConfiguredRenderPipelines")?.ToObject<List<string>>(serializer);
            gameMetadata.usingLegacyInputManager = jObject.GetValue("usingLegacyInputManager")?.ToObject<bool>(serializer) ?? false;
            return gameMetadata;
        }

        public override bool CanRead => true;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RGGameMetadata);
        }
    }
}
