using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
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
}
