using System;
using RGBotConfigs.RGStateProviders;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{

    [Serializable]
    public class RgStateHandlerPlatformer2DPlayer : MonoBehaviour
    {
        [NonSerialized]
        [Tooltip("Draw debug gizmos for player locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

        [HideInInspector]
        public Vector2 position = Vector2.zero;

        [Tooltip("The current height that the player can jump.  (Updated automatically when using a RGStatePlatformer2DPlayerStatsProvider)")]
        [Min(0f)]
        public float jumpHeight;

        [Tooltip(
            "The max height that the player can jump.  (Updated automatically when using a RGStatePlatformer2DPlayerStatsProvider)")]
        public float maxJumpHeight;

        [Tooltip("The current horizontal velocity of the player.  (Updated automatically when using a RGStatePlatformer2DPlayerStatsProvider)")]
        public float velocity;

        [Tooltip("The current max horizontal velocity of the player.  (Updated automatically when using a RGStatePlatformer2DPlayerStatsProvider)")]
        public float maxVelocity;

        [Tooltip("The current max safe fall height of the player.  From this height the player will take zero damage. <0 means infinite. >=0 is treated as the actual value  (Updated automatically when using a RGStatePlatformer2DPlayerStatsProvider)")]
        public float safeFallHeight = -1f;

        [Tooltip(
            "The current max non fatal fall height of the player.  From this height the player may take damage, but will not die from the fall. <0 means infinite. >=0 is treated as the actual value  (Updated automatically when using a RGStatePlatformer2DPlayerStatsProvider)")]
        public float nonFatalFallHeight = -1f;

        [Tooltip(
            "The gravity value (negative) to use in fall and jump calculations.  (Updated automatically when using a RGStatePlatformer2DPlayerStatsProvider)")]
        public float gravity = -9.81f;

        [NonSerialized]
        private RGStatePlatformer2DPlayerStatsProvider _statsProvider;

        private void OnEnable()
        {
            _statsProvider = gameObject.GetComponent<RGStatePlatformer2DPlayerStatsProvider>();
            UpdateState();
        }

        private void Update()
        {
            UpdateState();
        }

        public void UpdateState()
        {
            if (_statsProvider != null)
            {
                jumpHeight = _statsProvider.JumpHeight();
                maxJumpHeight = _statsProvider.MaxJumpHeight();
                velocity = _statsProvider.Velocity();
                maxVelocity = _statsProvider.MaxVelocity();
                safeFallHeight = _statsProvider.SafeFallHeight();
                nonFatalFallHeight = _statsProvider.NonFatalFallHeight();
                gravity = _statsProvider.Gravity();
            }
            position = GetTruePosition();
        }

        // ReSharper disable once InconsistentNaming
        private Vector2 GetTruePosition()
        {
            var theCollider = gameObject.GetComponent<BoxCollider2D>();
            var theBounds = theCollider.bounds;
            // bottom of the feet centered horizontally
            return new Vector2(theBounds.center.x, theBounds.min.y);
        }

        private void OnDrawGizmos()
        {
            if (renderDebugGizmos)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere((Vector2)position, 0.125f);
            }
        }

    }

}
