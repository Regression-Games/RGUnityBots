using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RecordedGameObjectState
    {

        public int id;

        public int? parentId;

        public string path;
        public string normalizedPath;

        [NonSerialized] // used internally for performance, but serialized as the name
        public Scene scene;

        public string tag;
        public string layer;

        public Bounds? screenSpaceBounds;

        public float? screenSpaceZOffset;

        [NonSerialized]
        // keep reference to this instead of updating its fields every tick
        public Transform transform;

        public Bounds? worldSpaceBounds;

        public IList<RigidbodyRecordState> rigidbodies;
        public IList<ColliderRecordState> colliders;
        public IList<BehaviourState> behaviours;
        public IList<ECSComponentState> ecsComponents;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"id\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, id);
            stringBuilder.Append(",\n\"parentId\":");
            IntJsonConverter.WriteToStringBuilderNullable(stringBuilder, parentId);
            stringBuilder.Append(",\n\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\n\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\n\"scene\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, scene.name);
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
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, transform.position);
            stringBuilder.Append(",\n\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilder(stringBuilder, transform.rotation);
            stringBuilder.Append(",\n\"rigidbodies\":[\n");
            var rigidbodiesCount = rigidbodies.Count;
            for (var i = 0; i < rigidbodiesCount; i++)
            {
                rigidbodies[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < rigidbodiesCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"colliders\":[\n");
            var collidersCount = colliders.Count;
            for (var i = 0; i < collidersCount; i++)
            {
                colliders[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < collidersCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"behaviours\":[\n");
            var behavioursCount = behaviours.Count;
            for (var i = 0; i < behavioursCount; i++)
            {
                behaviours[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < behavioursCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"ecsComponents\":[\n");
            var ecsComponentsCount = ecsComponents.Count;
            for (var i = 0; i < ecsComponentsCount; i++)
            {
                ecsComponents[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < ecsComponentsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            
            stringBuilder.Append("\n]\n}");
        }

    }
}
