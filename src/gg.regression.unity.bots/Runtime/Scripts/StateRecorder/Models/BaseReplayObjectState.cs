using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    public abstract class BaseReplayObjectState
    {
        public int id;

        public string path;
        public string normalizedPath;
        public string scene;
        public string tag;
        public string layer;

        public List<RigidbodyReplayState> rigidbodies;
        public List<ColliderReplayState> colliders;

        public Bounds screenSpaceBounds;

        public float screenSpaceZOffset;

        public Vector3 position;
        public Quaternion rotation;

        public Bounds? worldSpaceBounds;
    }
}
