using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public class FrameStateData : ReplayFrameStateData
    {

        // re-usable and hopefully large enough to fit objects of all sizes, it not, it will resize
        private static readonly StringBuilder _stringBuilder = new StringBuilder(1_000_000);

        public PerformanceMetricData performance;
        public new List<RecordedGameObjectState> state;

        public string ToJson()
        {
            _stringBuilder.Clear();
            _stringBuilder.Append("{\n\"tickNumber\":");
            _stringBuilder.Append(tickNumber);
            _stringBuilder.Append(",\n\"keyFrame\":[");
            var keyFrameLength = keyFrame.Length;
            for (var i = 0; i < keyFrameLength; i++)
            {
                _stringBuilder.Append("\"").Append(keyFrame[i].ToString()).Append("\"");
                if (i + 1 < keyFrameLength)
                {
                    _stringBuilder.Append(",");
                }
            }
            _stringBuilder.Append("],\n\"time\":");
            _stringBuilder.Append(DoubleJsonConverter.ToJsonString(time));
            _stringBuilder.Append(",\n\"timeScale\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(timeScale));
            _stringBuilder.Append(",\n\"screenSize\":");
            _stringBuilder.Append(VectorIntJsonConverter.ToJsonString(screenSize));
            _stringBuilder.Append(",\n\"performance\":");
            _stringBuilder.Append(performance.ToJson());
            _stringBuilder.Append(",\n\"pixelHash\":\"");
            _stringBuilder.Append(pixelHash);
            _stringBuilder.Append("\",\n\"state\":[\n");
            var stateCount = state.Count;
            for (var i = 0; i < stateCount; i++)
            {
                _stringBuilder.Append(state[i].ToJson());
                if (i + 1 < stateCount)
                {
                    _stringBuilder.Append(",\n");
                }
            }
            _stringBuilder.Append("\n],\n\"inputs\":");
            _stringBuilder.Append(inputs.ToJson());
            _stringBuilder.Append("\n}");
            return _stringBuilder.ToString();
        }

    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData
    {
        // re-usable and hopefully large enough to fit objects of all sizes, if not, it will resize
        private static readonly StringBuilder _stringBuilder = new StringBuilder(100_000);

        public List<KeyboardInputActionData> keyboard;
        public List<MouseInputActionData> mouse;

        public string ToJson()
        {
            _stringBuilder.Clear();
            _stringBuilder.Append("{\n\"keyboard\":[\n");
            var keyboardCount = keyboard.Count;
            for (var i = 0; i < keyboardCount; i++)
            {
                _stringBuilder.Append(keyboard[i].ToJson());
                if (i + 1 < keyboardCount)
                {
                    _stringBuilder.Append(",\n");
                }
            }
            _stringBuilder.Append("\n],\n\"mouse\":[\n");
            var mouseCount = mouse.Count;
            for (var i = 0; i < mouseCount; i++)
            {
                _stringBuilder.Append(mouse[i].ToJson());
                if (i + 1 < mouseCount)
                {
                    _stringBuilder.Append(",\n");
                }
            }
            _stringBuilder.Append("\n]\n}");
            return _stringBuilder.ToString();
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerformanceMetricData
    {
        public double previousTickTime;
        public int framesSincePreviousTick;
        public int fps;
        public EngineStatsData engineStats;

        public string ToJson()
        {
            return "{\"previousTickTime\":" + DoubleJsonConverter.ToJsonString(previousTickTime)
                                            + ",\"framesSincePreviousTick\":" + framesSincePreviousTick
                                            + ",\"fps\":" + fps
                                            + ",\"engineStats\":" + engineStats.ToJson()
                                            + "}";
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

        public string ToJson()
        {
            return "{\"frameTime\":" + FloatJsonConverter.ToJsonString(frameTime)
                                     + ",\"renderTime\":" + FloatJsonConverter.ToJsonString(renderTime)
                                     + ",\"triangles\":" + triangles
                                     + ",\"vertices\":" + vertices
                                     + ",\"setPassCalls\":" + setPassCalls
                                     + ",\"drawCalls\":" + drawCalls
                                     + ",\"dynamicBatchedDrawCalls\":" + dynamicBatchedDrawCalls
                                     + ",\"staticBatchedDrawCalls\":" + staticBatchedDrawCalls
                                     + ",\"instancedBatchedDrawCalls\":" + instancedBatchedDrawCalls
                                     + ",\"batches\":" + batches
                                     + ",\"dynamicBatches\":" + dynamicBatches
                                     + ",\"staticBatches\":" + staticBatches
                                     + ",\"instancedBatches\":" + instancedBatches
                                     + "}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // replay doesn't need to deserialize everything we record
    public class ReplayFrameStateData
    {
        public long tickNumber;
        public KeyFrameType[] keyFrame;
        public double time;
        public float timeScale;
        public Vector2Int screenSize;
        public string pixelHash;
        public List<ReplayGameObjectState> state;
        public InputData inputs;
    }

    public abstract class BaseReplayObjectState
    {
        public int id;

        public string path;
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
        // re-usable and large enough to fit objects of all sizes
        private static readonly StringBuilder _stringBuilder = new StringBuilder(100_000);

        public int id;

        public string path;
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

        public string ToJson()
        {
            _stringBuilder.Clear();
            _stringBuilder.Append("{\n\"id\":");
            _stringBuilder.Append(id);
            _stringBuilder.Append(",\n\"path\":");
            _stringBuilder.Append(JsonUtils.EscapeJsonString(path));
            _stringBuilder.Append(",\n\"scene\":");
            _stringBuilder.Append(JsonUtils.EscapeJsonString(scene.name));
            _stringBuilder.Append(",\n\"tag\":");
            _stringBuilder.Append(JsonUtils.EscapeJsonString(tag));
            _stringBuilder.Append(",\n\"layer\":");
            _stringBuilder.Append(JsonUtils.EscapeJsonString(layer));
            _stringBuilder.Append(",\n\"rendererCount\":");
            _stringBuilder.Append(rendererCount);
            _stringBuilder.Append(",\n\"screenSpaceBounds\":");
            _stringBuilder.Append(BoundsJsonConverter.ToJsonString(screenSpaceBounds));
            _stringBuilder.Append(",\n\"screenSpaceZOffset\":");
            _stringBuilder.Append(FloatJsonConverter.ToJsonString(screenSpaceZOffset));
            _stringBuilder.Append(",\n\"worldSpaceBounds\":");
            _stringBuilder.Append(BoundsJsonConverter.ToJsonString(worldSpaceBounds));
            _stringBuilder.Append(",\n\"position\":");
            _stringBuilder.Append(VectorJsonConverter.ToJsonStringVector3(transform.position));
            _stringBuilder.Append(",\n\"rotation\":");
            _stringBuilder.Append(QuaternionJsonConverter.ToJsonString(transform.rotation));
            _stringBuilder.Append(",\n\"rigidbodies\":[\n");
            var rigidbodiesCount = rigidbodies.Count;
            for (var i = 0; i < rigidbodiesCount; i++)
            {
                _stringBuilder.Append(rigidbodies[i].ToJson());
                if (i + 1 < rigidbodiesCount)
                {
                    _stringBuilder.Append(",\n");
                }
            }
            _stringBuilder.Append("\n],\n\"colliders\":[\n");
            var collidersCount = colliders.Count;
            for (var i = 0; i < collidersCount; i++)
            {
                _stringBuilder.Append(colliders[i].ToJson());
                if (i + 1 < collidersCount)
                {
                    _stringBuilder.Append(",\n");
                }
            }
            _stringBuilder.Append("\n],\n\"behaviours\":[\n");
            var behavioursCount = behaviours.Count;
            for (var i = 0; i < behavioursCount; i++)
            {
                _stringBuilder.Append(behaviours[i].ToJson());
                if (i + 1 < behavioursCount)
                {
                    _stringBuilder.Append(",\n");
                }
            }
            _stringBuilder.Append("\n]\n}");
            return _stringBuilder.ToString();
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class BehaviourState
    {
        public string name;
        public string path;
        public Behaviour state;

        public override string ToString()
        {
            return name;
        }

        public string ToJson()
        {
            string stateJson;
            try
            {
                stateJson = JsonConvert.SerializeObject(state, Formatting.None, ScreenRecorder.JsonSerializerSettings);
                if (string.IsNullOrEmpty(stateJson))
                {
                    // shouldn't happen... but keeps us running if it does
                    stateJson = "{\"EXCEPTION\":\"Could not convert Behaviour to JSON\"}";
                }
            }
            catch (Exception ex)
            {
                RGDebug.LogException(ex, "Error converting behaviour to JSON - " + state.name);
                stateJson = "{}";
            }

            return "{\"name\":" + JsonUtils.EscapeJsonString(name)
                                     + ",\"path\":" + JsonUtils.EscapeJsonString(path)
                                     // have to use JsonConvert to serialize here as Behaviours are the wild wild west of contents
                                     + ",\"state\":" + stateJson
                                     + "}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderRecordState
    {
        public string path;
        public Collider collider;

        public virtual string ToJson()
        {
            return "{\"path\":" + JsonUtils.EscapeJsonString(path)
                                + ",\"is2D\":false"
                                + ",\"bounds\":" + BoundsJsonConverter.ToJsonString(collider.bounds)
                                + ",\"isTrigger\":" + (collider.isTrigger ? "true" : "false")
                                + "}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Collider2DRecordState : ColliderRecordState
    {
        public new Collider2D collider;

        public override string ToJson()
        {
            return "{\"path\":" + JsonUtils.EscapeJsonString(path)
                                + ",\"is2D\":true"
                                + ",\"bounds\":" + BoundsJsonConverter.ToJsonString(collider.bounds)
                                + ",\"isTrigger\":" + (collider.isTrigger ? "true" : "false")
                                + "}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderReplayState
    {
        public string path;
        public bool is2D;
        public Bounds bounds;
        public bool isTrigger;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyRecordState
    {

        public string path;

        // keep a ref to this instead of updating fields every tick
        public Rigidbody rigidbody;

        public virtual string ToJson()
        {
            return "{\"path\":" + JsonUtils.EscapeJsonString(path)
                                + ",\"is2D\":false"
                                + ",\"position\":" + VectorJsonConverter.ToJsonStringVector3(rigidbody.position)
                                + ",\"rotation\":" + QuaternionJsonConverter.ToJsonString(rigidbody.rotation)
                                + ",\"velocity\":" + VectorJsonConverter.ToJsonStringVector3(rigidbody.velocity)
                                + ",\"mass\":" + FloatJsonConverter.ToJsonString(rigidbody.mass)
                                + ",\"drag\":" + FloatJsonConverter.ToJsonString(rigidbody.drag)
                                + ",\"angularDrag\":" + FloatJsonConverter.ToJsonString(rigidbody.angularDrag)
                                + ",\"useGravity\":" + (rigidbody.useGravity ? "true" : "false")
                                + ",\"isKinematic\":" + (rigidbody.isKinematic ? "true" : "false")
                                + "}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Rigidbody2DRecordState: RigidbodyRecordState
    {

        // keep a ref to this instead of updating fields every tick
        public new Rigidbody2D rigidbody;

        public override string ToJson()
        {
            return "{\"path\":" + JsonUtils.EscapeJsonString(path)
                                + ",\"is2D\":true"
                                + ",\"position\":" + VectorJsonConverter.ToJsonStringVector3(rigidbody.position)
                                // rotation around Z
                                + ",\"rotation\":" + QuaternionJsonConverter.ToJsonString(Quaternion.Euler(0, 0, rigidbody.rotation))
                                + ",\"velocity\":" + VectorJsonConverter.ToJsonStringVector3(rigidbody.velocity)
                                + ",\"mass\":" + FloatJsonConverter.ToJsonString(rigidbody.mass)
                                + ",\"drag\":" + FloatJsonConverter.ToJsonString(rigidbody.drag)
                                + ",\"angularDrag\":" + FloatJsonConverter.ToJsonString(rigidbody.angularDrag)
                                + ",\"gravityScale\":" + FloatJsonConverter.ToJsonString(rigidbody.gravityScale)
                                + ",\"isKinematic\":" + (rigidbody.isKinematic ? "true" : "false")
                                + "}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyReplayState
    {
        public string path;

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
