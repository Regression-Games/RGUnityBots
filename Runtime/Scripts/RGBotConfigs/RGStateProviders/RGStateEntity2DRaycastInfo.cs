using System;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    [Serializable]
    public class RGStateEntity2DRaycastInfo
    {
        public Vector2 from;
        public Vector2 direction;
        public float angle => Vector2.SignedAngle(Vector2.zero, direction);
        public Collider2D hitCollider = null;
        public Vector2? hitPoint = null;
        public float? hitRange => hitPoint != null ? Vector2.Distance(from, (Vector2)hitPoint) : null;
        public string hitObjectType = null;
        public int? hitObjectId = null;
    }
}
