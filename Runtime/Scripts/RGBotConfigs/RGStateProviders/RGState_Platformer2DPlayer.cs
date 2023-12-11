using System;
using System.Collections.Generic;
using RegressionGames.RGBotConfigs;
using RegressionGames.StateActionTypes;
using RGBotConfigs.RGStateProviders;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntity_Platformer2DPlayer : RGStateEntity<RGState_Platformer2DPlayer>
    {
        public float jumpHeight => (float)this.GetValueOrDefault("jumpHeight", 0);
        public float maxJumpHeight => (float)this.GetValueOrDefault("maxJumpHeight", 0);
        public float velocity => (float)this.GetValueOrDefault("velocity", 0f);
        public float maxVelocity => (float)this.GetValueOrDefault("maxVelocity", 0f);
        public float safeFallHeight => (float)this.GetValueOrDefault("safeFallHeight", -1f);
        public float nonFatalFallHeight => (float)this.GetValueOrDefault("nonFatalFallHeight", -1f);
        public bool isOnGround => (bool)this.GetValueOrDefault("isOnGround", false);
        public float gravity => (float)this.GetValueOrDefault("gravity", -9.81f);
    }
    
    [Serializable]
    public class RGState_Platformer2DPlayer : RGState
    {
        [Tooltip("Draw debug gizmos for player locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

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

        private Vector2? _truePosition = null;

        [NonSerialized]
        private RGStatePlatformer2DPlayerStatsProvider _statsProvider;

        private void Start()
        {
            _statsProvider = gameObject.GetComponent<RGStatePlatformer2DPlayerStatsProvider>();
        }

        protected override Dictionary<string, object> GetState()
        {
            if (_statsProvider is not null)
            {
                maxJumpHeight = _statsProvider.MaxJumpHeight();
                velocity = _statsProvider.Velocity();
                maxVelocity = _statsProvider.MaxVelocity();
                safeFallHeight = _statsProvider.SafeFallHeight();
                nonFatalFallHeight = _statsProvider.NonFatalFallHeight();
                gravity = _statsProvider.Gravity();
            }
            _truePosition = GetTruePosition();
            return new()
            {
                // override the meaning of position to be centered at player collider feet
                {"position", (Vector3)(Vector2)_truePosition},
                {"maxJumpHeight", maxJumpHeight},
                {"velocity", velocity},
                {"maxVelocity", maxVelocity},
                {"safeFallHeight", safeFallHeight},
                {"nonFatalFallHeight", nonFatalFallHeight},
                {"gravity", gravity}
            };
        }

        protected override Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity_Platformer2DPlayer);
        }

        // ReSharper disable once InconsistentNaming
        private Vector2 GetTruePosition()
        {
            var theCollider = gameObject.GetComponent<BoxCollider2D>();
            
            // bottom of the feet centered horizontally
            var actualPosition = new Vector2(theCollider.bounds.center.x, theCollider.bounds.min.y);

            return actualPosition;

        }

        private void OnDrawGizmos()
        {
            if (_truePosition is not null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere((Vector2)_truePosition, 0.125f);
            }
        }
        
    }
    
}
