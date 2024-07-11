using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using Unity.Entities;

namespace RegressionGames.StateRecorder.ECS
{
    public class EntityComponentDataProvider : IComponentDataProvider
    {

        public int apiVersion = SdkApiVersion.VERSION_4;

        public int ApiVersion()
        {
            return apiVersion;
        }

        private static readonly Dictionary<Type, FieldInfo[]> FieldInfoCache = new();

        private readonly IComponentData _componentData;

        public EntityComponentDataProvider(IComponentData componentData)
        {
            this._componentData = componentData;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            Type structType = _componentData.GetType();
            if (!FieldInfoCache.TryGetValue(structType, out var fieldInfos))
            {
                fieldInfos = structType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                FieldInfoCache[structType] = fieldInfos;
            }

            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, structType.Name);
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"state\":{");

            var fieldInfosLength = fieldInfos.Length;
            for (var i = 0; i < fieldInfosLength; i++)
            {
                var fieldInfo = fieldInfos[i];
                StringJsonConverter.WriteToStringBuilder(stringBuilder, fieldInfo.Name);
                stringBuilder.Append(":");
                JsonUtils.WriteObjectStateToStringBuilder(stringBuilder, fieldInfo.GetValue(_componentData));
                if (i + 1 < fieldInfosLength)
                {
                    stringBuilder.Append(",");
                }
            }

            stringBuilder.Append("}}");
        }
    }
}
