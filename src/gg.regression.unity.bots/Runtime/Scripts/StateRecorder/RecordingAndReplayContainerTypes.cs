using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using StateRecorder;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames.StateRecorder
{

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RecordingFrameStateData : BaseFrameStateData
    {
        /**
         * <summary>Reference to the original recording this was created from during replay, possibly null</summary>
         */
        public string referenceSessionId = null;

        public PerformanceMetricData performance;
        public new IEnumerable<RecordedGameObjectState> state;

        // re-usable and large enough to fit all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(10_000_000);

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"sessionId\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, sessionId);
            stringBuilder.Append(",\n\"referenceSessionId\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, referenceSessionId);
            stringBuilder.Append(",\n\"tickNumber\":");
            LongJsonConverter.WriteToStringBuilder(stringBuilder, tickNumber);
            stringBuilder.Append(",\n\"keyFrame\":[");
            var keyFrameLength = keyFrame.Length;
            for (var i = 0; i < keyFrameLength; i++)
            {
                stringBuilder.Append("\"").Append(keyFrame[i].ToString()).Append("\"");
                if (i + 1 < keyFrameLength)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"time\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, time);
            stringBuilder.Append(",\n\"timeScale\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, timeScale);
            stringBuilder.Append(",\n\"screenSize\":");
            VectorIntJsonConverter.WriteToStringBuilder(stringBuilder, screenSize);
            stringBuilder.Append(",\n\"performance\":");
            performance.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\n\"pixelHash\":\"");
            stringBuilder.Append(pixelHash);
            stringBuilder.Append("\",\n\"state\":[\n");
            var counter = 0;
            var stateCount = state.Count();
            foreach( var stateEntry in state)
            {
                stateEntry.WriteToStringBuilder(stringBuilder);
                if (++counter < stateCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"inputs\":");
            inputs.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }

        public string ToJsonString()
        {
            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder);
            return _stringBuilder.ToString();
        }

    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData
    {
        public List<KeyboardInputActionData> keyboard;
        public List<MouseInputActionData> mouse;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"keyboard\":[\n");
            var keyboardCount = keyboard.Count;
            for (var i = 0; i < keyboardCount; i++)
            {
                keyboard[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < keyboardCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"mouse\":[\n");
            var mouseCount = mouse.Count;
            for (var i = 0; i < mouseCount; i++)
            {
                mouse[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < mouseCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]\n}");
        }

    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerformanceMetricData
    {
        public double previousTickTime;
        public int framesSincePreviousTick;
        public int fps;
        public long? cpuTime;
        public long? memory;
        public EngineStatsData engineStats;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"previousTickTime\":");
            DoubleJsonConverter.WriteToStringBuilder(stringBuilder, previousTickTime);
            stringBuilder.Append(",\"framesSincePreviousTick\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, framesSincePreviousTick);
            stringBuilder.Append(",\"fps\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, fps);
            if (cpuTime.HasValue)
            {
                stringBuilder.Append(",\"cpuTime\":");
                LongJsonConverter.WriteToStringBuilder(stringBuilder, cpuTime.Value);
            }
            if (memory.HasValue)
            {
                stringBuilder.Append(",\"memory\":");
                LongJsonConverter.WriteToStringBuilder(stringBuilder, memory.Value);
            }
            stringBuilder.Append(",\"engineStats\":");
            engineStats.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class EngineStatsData
    {
        public float frameTime;
        public float renderTime;

        public int triangles;
        public int vertices;

        public int setPassCalls;

        public int drawCalls;
        public int dynamicBatchedDrawCalls;
        public int staticBatchedDrawCalls;
        public int instancedBatchedDrawCalls;

        public int batches;
        public int dynamicBatches;
        public int staticBatches;
        public int instancedBatches;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"frameTime\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, frameTime);
            stringBuilder.Append(",\"renderTime\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, renderTime);
            stringBuilder.Append(",\"triangles\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, triangles);
            stringBuilder.Append(",\"vertices\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, vertices);
            stringBuilder.Append(",\"setPassCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, setPassCalls);
            stringBuilder.Append(",\"drawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, drawCalls);
            stringBuilder.Append(",\"dynamicBatchedDrawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, dynamicBatchedDrawCalls);
            stringBuilder.Append(",\"staticBatchedDrawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, staticBatchedDrawCalls);
            stringBuilder.Append(",\"instancedBatchedDrawCalls\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, instancedBatchedDrawCalls);
            stringBuilder.Append(",\"batches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, batches);
            stringBuilder.Append(",\"dynamicBatches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, dynamicBatches);
            stringBuilder.Append(",\"staticBatches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, staticBatches);
            stringBuilder.Append(",\"instancedBatches\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, instancedBatches);
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // replay doesn't need to deserialize everything we record
    public class BaseFrameStateData
    {
        /**
         * <summary>UUID of the session</summary>
         */
        public string sessionId;
        public long tickNumber;
        public KeyFrameType[] keyFrame;
        public double time;
        public float timeScale;
        public Vector2Int screenSize;
        public string pixelHash;
        public List<ReplayGameObjectState> state;
        public InputData inputs;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // replay doesn't need to deserialize everything we record
    public class ReplayFrameStateData :BaseFrameStateData
    {
        /**
         * <summary>UUID of the session</summary>
         */
        public long recordingSessionId;
    }

    public abstract class BaseReplayObjectState
    {
        public int id;

        public string path;
        public string normalizedPath;
        public string scene;
        public string tag;
        public string layer;

        public int rendererCount;

        public List<RigidbodyReplayState> rigidbodies;
        public List<ColliderReplayState> colliders;

        public Bounds screenSpaceBounds;

        public float screenSpaceZOffset;

        public Vector3 position;
        public Quaternion rotation;

        public Bounds? worldSpaceBounds;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // replay doesn't need to deserialize everything we record
    public class ReplayGameObjectState : BaseReplayObjectState
    {
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RecordedGameObjectState
    {

        public int id;

        public string path;
        public string normalizedPath;

        [NonSerialized] // used internally for performance, but serialized as the name
        public Scene scene;

        public string tag;
        public string layer;

        public int rendererCount;

        public Bounds screenSpaceBounds;

        public float screenSpaceZOffset;

        [NonSerialized]
        // keep reference to this instead of updating its fields every tick
        public Transform transform;

        public Bounds? worldSpaceBounds;

        public List<RigidbodyRecordState> rigidbodies;
        public List<ColliderRecordState> colliders;
        public List<BehaviourState> behaviours;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"id\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, id);
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
            stringBuilder.Append(",\n\"rendererCount\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, rendererCount);
            stringBuilder.Append(",\n\"screenSpaceBounds\":");
            BoundsJsonConverter.WriteToStringBuilder(stringBuilder, screenSpaceBounds);
            stringBuilder.Append(",\n\"screenSpaceZOffset\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, screenSpaceZOffset);
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
            stringBuilder.Append("\n]\n}");
        }

    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class BehaviourState
    {
        public string name;
        public string path;
        public string normalizedPath;
        public Behaviour state;

        public override string ToString()
        {
            return name;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\"state\":");
            JsonUtils.WriteBehaviourStateToStringBuilder(stringBuilder, state);
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderRecordState
    {
        public string path;
        public string normalizedPath;
        public Collider collider;

        public virtual void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\"is2D\":false");
            stringBuilder.Append(",\"bounds\":");
            BoundsJsonConverter.WriteToStringBuilder(stringBuilder, collider.bounds);
            stringBuilder.Append(",\"isTrigger\":");
            stringBuilder.Append((collider.isTrigger ? "true" : "false"));
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Collider2DRecordState : ColliderRecordState
    {
        public new Collider2D collider;

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\"is2D\":true");
            stringBuilder.Append(",\"bounds\":");
            BoundsJsonConverter.WriteToStringBuilder(stringBuilder, collider.bounds);
            stringBuilder.Append(",\"isTrigger\":");
            stringBuilder.Append((collider.isTrigger ? "true" : "false"));
            stringBuilder.Append("}");
        }

    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderReplayState
    {
        public string path;
        public string normalizedPath;
        public bool is2D;
        public Bounds bounds;
        public bool isTrigger;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyRecordState
    {

        public string path;
        public string normalizedPath;

        // keep a ref to this instead of updating fields every tick
        public Rigidbody rigidbody;

        public virtual void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\"is2D\":false");
            stringBuilder.Append(",\"position\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.position);
            stringBuilder.Append(",\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.rotation);
            stringBuilder.Append(",\"velocity\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.velocity);
            stringBuilder.Append(",\"mass\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.mass);
            stringBuilder.Append(",\"drag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.drag);
            stringBuilder.Append(",\"angularDrag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.angularDrag);
            stringBuilder.Append(",\"useGravity\":");
            stringBuilder.Append((rigidbody.useGravity ? "true" : "false"));
            stringBuilder.Append(",\"isKinematic\":");
            stringBuilder.Append((rigidbody.isKinematic ? "true" : "false"));
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Rigidbody2DRecordState: RigidbodyRecordState
    {

        // keep a ref to this instead of updating fields every tick
        public new Rigidbody2D rigidbody;

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"path\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, path);
            stringBuilder.Append(",\"normalizedPath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, normalizedPath);
            stringBuilder.Append(",\"is2D\":true");
            stringBuilder.Append(",\"position\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.position);
            stringBuilder.Append(",\"rotation\":");
            QuaternionJsonConverter.WriteToStringBuilder(stringBuilder, Quaternion.Euler(0, 0, rigidbody.rotation));
            stringBuilder.Append(",\"velocity\":");
            VectorJsonConverter.WriteToStringBuilderVector3(stringBuilder, rigidbody.velocity);
            stringBuilder.Append(",\"mass\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.mass);
            stringBuilder.Append(",\"drag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.drag);
            stringBuilder.Append(",\"angularDrag\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.angularDrag);
            stringBuilder.Append(",\"gravityScale\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, rigidbody.gravityScale);
            stringBuilder.Append(",\"isKinematic\":");
            stringBuilder.Append((rigidbody.isKinematic ? "true" : "false"));
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyReplayState
    {
        public string path;
        public string normalizedPath;

        public Vector3 position;

        public bool is2D;

        // for 3D this is rotation on Z axis
        public Quaternion rotation;
        public Vector3 velocity;
        public float mass;
        public float drag;
        public float angularDrag;
        public bool useGravity;
        public float gravityScale;
        public bool isKinematic;
    }
}
