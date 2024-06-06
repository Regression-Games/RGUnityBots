using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
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
