using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntityPlatformer2D
    {
        public Vector3 spriteSize = Vector3.one;
        public Vector2[] platformPositions = Array.Empty<Vector2>();
    }
}
