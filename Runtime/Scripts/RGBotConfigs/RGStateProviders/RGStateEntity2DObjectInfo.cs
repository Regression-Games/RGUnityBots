using System;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    [Serializable]
    public class RGStateEntity2DObjectInfo
    {
        public int id;
        public Collider hitCollider = null;
        public Vector2? position = null;
    }
}
