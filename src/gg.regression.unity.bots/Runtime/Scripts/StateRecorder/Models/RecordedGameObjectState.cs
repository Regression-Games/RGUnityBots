using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RecordedGameObjectState
    {
        // version of this schema, update this if fields change
        public int apiVersion = SdkApiVersion.VERSION_4;

        public int EffectiveApiVersion => Math.Max(apiVersion, componentDataProviders.DefaultIfEmpty().Max(a => a?.ApiVersion() ?? 0));

        public long id;

        public long? parentId;

        public string path;
        public string normalizedPath;

        public string scene;

        public string tag;
        public string layer;

        public ObjectType type;

        public Bounds? screenSpaceBounds;

        public float? screenSpaceZOffset;

        public Vector3? position;
        public Quaternion? rotation;

        public Bounds? worldSpaceBounds;

        // List of component data provider impls for this object.. allows transform and entity support
        [NonSerialized]
        public List<IComponentDataProvider> componentDataProviders;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"id\":");
            LongJsonConverter.WriteToStringBuilder(stringBuilder, id);
            stringBuilder.Append(",\n\"parentId\":");
            LongJsonConverter.WriteToStringBuilderNullable(stringBuilder, parentId);
            stringBuilder.Append(",\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\n\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\n\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\n\"scene\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, scene);
            stringBuilder.Append(",\n\"tag\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, tag);
            stringBuilder.Append(",\n\"layer\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, layer);
            stringBuilder.Append(",\n\"screenSpaceBounds\":");
            BoundsJsonConverter.WriteToStringBuilderNullable(stringBuilder, screenSpaceBounds);
            stringBuilder.Append(",\n\"screenSpaceZOffset\":");
            FloatJsonConverter.WriteToStringBuilderNullable(stringBuilder, screenSpaceZOffset);
            stringBuilder.Append(",\n\"worldSpaceBounds\":");
            BoundsJsonConverter.WriteToStringBuilderNullable(stringBuilder, worldSpaceBounds);
            stringBuilder.Append(",\n\"position\":");
            VectorJsonConverter.WriteToStringBuilderVector3Nullable(stringBuilder, position);
            stringBuilder.Append(",\n\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilderNullable(stringBuilder, rotation);
            // TODO: Someday remove these no longer used fields
            stringBuilder.Append(",\n\"rigidbodies\":[],\n\"colliders\":[],\n\"behaviours\":[]");
            stringBuilder.Append(",\n\"components\":[\n");
            var componentDataProvidersCount = componentDataProviders.Count;
            for (var i = 0; i < componentDataProvidersCount; i++)
            {
                var cdp = componentDataProviders[i];
                cdp.WriteToStringBuilder(stringBuilder);
                if (i + 1 < componentDataProvidersCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]\n}");
        }

    }
}
