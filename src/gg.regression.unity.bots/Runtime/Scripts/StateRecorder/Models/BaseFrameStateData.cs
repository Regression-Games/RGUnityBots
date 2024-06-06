using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
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
}
