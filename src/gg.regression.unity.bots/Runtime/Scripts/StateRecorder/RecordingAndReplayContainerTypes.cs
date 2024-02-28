using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace RegressionGames.StateRecorder
{

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class FrameStateData : ReplayFrameStateData
    {
        public PerformanceMetricData performance;
        public new List<RecordedGameObjectState> state;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData
    {
        public List<KeyboardInputActionData> keyboard;
        public List<MouseInputActionData> mouse;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerformanceMetricData
    {
        public float previousTickTime;
        public int framesSincePreviousTick;
        public int fps;
        public EngineStatsData engineStats;
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
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // replay doesn't need to deserialize everything we reocrd
    public class ReplayFrameStateData
    {
        public long tickNumber;
        public bool keyFrame;
        public float time;
        public float timeScale;
        public int[] screenSize;
        public List<ReplayGameObjectState> state;
        public InputData inputs;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // replay doesn't need to deserialize everything we reocrd
    public class ReplayGameObjectState
    {
        public int id;

        public string path;
        public string scene;
        public string tag;
        public string layer;

        public Bounds screenSpaceBounds;

        public Vector3 position;
        public Quaternion rotation;

        public Bounds? worldSpaceBounds;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RecordedGameObjectState : ReplayGameObjectState
    {
        public List<RigidbodyState> rigidbodies;
        public List<ColliderState> colliders;
        public List<BehaviourState> behaviours;
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
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderState
    {
        public string path;
        public Bounds bounds;
        public bool isTrigger;
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
    }
}
