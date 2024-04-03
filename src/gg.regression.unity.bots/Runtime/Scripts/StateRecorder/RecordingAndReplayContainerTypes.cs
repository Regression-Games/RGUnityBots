using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using StateRecorder;
using UnityEngine;

namespace RegressionGames.StateRecorder
{

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class FrameStateData : ReplayFrameStateData
    {
        public PerformanceMetricData performance;
        public new List<RecordedGameObjectState> state;

        public string ToJson()
        {
            return "{\n\"tickNumber\":" + tickNumber
                                        + ",\n\"keyFrame\":[" + string.Join(",",keyFrame.Select(a=>a.ToJson()))
                                        + "],\n\"time\":" + time
                                        + ",\n\"timeScale\":" + timeScale
                                        + ",\n\"screenSize\":" + VectorIntJsonConverter.ToJsonString(screenSize)
                                        + ",\n\"performance\":" + performance.ToJson()
                                        + ",\n\"state\":[\n" + string.Join(",\n", state.Select(a=>a.ToJson()))
                                        + "\n],\n\"inputs\":" + inputs.ToJson()
                                        + "\n}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData
    {
        public List<KeyboardInputActionData> keyboard;
        public List<MouseInputActionData> mouse;

        public string ToJson()
        {
            return "{\n\"keyboard\":[\n" + string.Join(",\n", keyboard.Select(a=>a.ToJson()))
                   + "\n],\n\"mouse\":[\n" + string.Join(",\n", mouse.Select(a=>a.ToJson()))
                   + "\n]"
                   + "\n}";
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
            return "{\"previousTickTime\":" + previousTickTime
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
            return "{\"frameTime\":" + frameTime
                                     + ",\"renderTime\":" + renderTime
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

        public List<RigidbodyState> rigidbodies;
        public List<ColliderState> colliders;

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
    public class RecordedGameObjectState : BaseReplayObjectState
    {
        public List<BehaviourState> behaviours;

        public string ToJson()
        {
            return "{\n\"id\":" + id
                                + ",\n\"path\":" + JsonConvert.ToString(path)
                                + ",\n\"scene\":" + JsonConvert.ToString(scene)
                                + ",\n\"tag\":" + JsonConvert.ToString(tag)
                                + ",\n\"layer\":" + JsonConvert.ToString(layer)
                                + ",\n\"rendererCount\":" + rendererCount
                                + ",\n\"screenSpaceBounds\":" + BoundsJsonConverter.ToJsonString(screenSpaceBounds)
                                + ",\n\"worldSpaceBounds\":" + BoundsJsonConverter.ToJsonString(worldSpaceBounds)
                                + ",\n\"position\":" + VectorJsonConverter.ToJsonStringVector3(position)
                                + ",\n\"rotation\":" + QuaternionJsonConverter.ToJsonString(rotation)
                                + ",\n\"rigidbodies\":[\n" + string.Join(",\n", rigidbodies.Select(a=>a.ToJson()))
                                + "\n],\n\"colliders\":[\n" + string.Join(",\n", colliders.Select(a=>a.ToJson()))
                                + "\n],\n\"behaviours\":[\n" + string.Join(",\n", behaviours.Select(a=>a.ToJson()))
                                + "\n]\n}";
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
            var stateJson = "{}";
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
            }

            return "{\"name\":" + JsonConvert.ToString(name)
                                     + ",\"path\":" + JsonConvert.ToString(path)
                                     // have to use JsonConvert to serialize here as Behaviours are the wild wild west of contents
                                     + ",\"state\":" + stateJson
                                     + "}";
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderState
    {
        public string path;
        public Bounds bounds;
        public bool isTrigger;

        public string ToJson()
        {
            return "{\"path\":" + JsonConvert.ToString(path)
                                + ",\"bounds\":" + BoundsJsonConverter.ToJsonString(bounds)
                                + ",\"isTrigger\":" + (isTrigger ? "true" : "false")
                                + "}";
        }
    }


    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyState
    {
        public string path;

        public Vector3 position;

        // for 3D this is rotation on Z axis
        public Quaternion rotation;
        public Vector3 velocity;
        public float mass;
        public float drag;
        public float angularDrag;
        public bool useGravity;
        public bool isKinematic;

        public string ToJson()
        {
            return "{\"path\":" + JsonConvert.ToString(path)
                                + ",\"position\":" + VectorJsonConverter.ToJsonStringVector3(position)
                                + ",\"rotation\":" + QuaternionJsonConverter.ToJsonString(rotation)
                                + ",\"velocity\":" + VectorJsonConverter.ToJsonStringVector3(velocity)
                                + ",\"mass\":" + mass
                                + ",\"drag\":" + drag
                                + ",\"angularDrag\":" + angularDrag
                                + ",\"useGravity\":" + (useGravity ? "true" : "false")
                                + ",\"isKinematic\":" + (isKinematic ? "true" : "false")
                                + "}";
        }
    }
}
