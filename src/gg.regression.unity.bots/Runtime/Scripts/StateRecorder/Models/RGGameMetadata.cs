using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [JsonConverter(typeof(RGGameMetadataJsonConverter))]
    public class RGGameMetadata
    {
        public int apiVersion = SdkApiVersion.VERSION_21;

        public int ApiVersion()
        {
            return apiVersion;
        }

        public string unityVersion;
        public RuntimePlatform runtimePlatform;
        public bool isEditor;
        public string productName;
        public string companyName;
        public string gameVersion;
        public string gameBuildGUID;
        public SystemLanguage systemLanguage;
        public List<string> allConfiguredRenderPipelines;
        public bool usingLegacyInputManager;

        public static RGGameMetadata GetMetadata()
        {
            return new RGGameMetadata()
            {
                unityVersion = Application.unityVersion,
                runtimePlatform = Application.platform,
                isEditor = Application.isEditor,
                productName = Application.productName,
                companyName = Application.companyName,
                gameVersion = Application.version,
                gameBuildGUID = Application.buildGUID,
                systemLanguage = Application.systemLanguage,
                allConfiguredRenderPipelines = GraphicsSettings.allConfiguredRenderPipelines.Select(a=> a.GetType().FullName).Distinct().ToList(),
#if ENABLE_LEGACY_INPUT_MANAGER
                usingLegacyInputManager = true
#else
                usingLegacyInputManager = false
#endif
            };

        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"unityVersion\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, unityVersion);
            stringBuilder.Append(",\n\"runtimePlatform\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Enum.GetName(typeof(RuntimePlatform),runtimePlatform));
            stringBuilder.Append(",\n\"isEditor\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, isEditor);
            stringBuilder.Append(",\n\"productName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, productName);
            stringBuilder.Append(",\n\"companyName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, companyName);
            stringBuilder.Append(",\n\"gameVersion\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, gameVersion);
            stringBuilder.Append(",\n\"gameBuildGUID\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, gameBuildGUID);
            stringBuilder.Append(",\n\"systemLanguage\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Enum.GetName(typeof(SystemLanguage),systemLanguage));

            stringBuilder.Append(",\n\"allConfiguredRenderPipelines\":[");
            var allConfiguredRenderPipelinesCount = allConfiguredRenderPipelines.Count;
            for (var i = 0; i < allConfiguredRenderPipelinesCount; i++)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, allConfiguredRenderPipelines[i]);
                if (i + 1 < allConfiguredRenderPipelinesCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]");

            stringBuilder.Append(",\n\"usingLegacyInputManager\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, usingLegacyInputManager);

            stringBuilder.Append("\n}");
        }

    }
}
