using System;
using System.Collections.Generic;
using RegressionGames.RGBotConfigs;
using RegressionGames.StateActionTypes;
using RGBotConfigs.RGStateProviders;
using UnityEngine;

namespace RegressionGames
{
    [Serializable]
    public class RGState_Platformer2DPlayer : RGState
    {
        [Tooltip("Draw debug gizmos for player locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

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
                jumpHeight = _statsProvider.JumpHeight();
                maxJumpHeight = _statsProvider.MaxJumpHeight();
                velocity = _statsProvider.Velocity();
                maxVelocity = _statsProvider.MaxVelocity();
                safeFallHeight = _statsProvider.SafeFallHeight();
                nonFatalFallHeight = _statsProvider.NonFatalFallHeight();
            }
            _truePosition = GetTruePosition();
            return new()
            {
                // override the meaning of position to be centered at player collider feet
                {"position", (Vector3)(Vector2)_truePosition},
                {"jumpHeight", jumpHeight},
                {"maxJumpHeight", maxJumpHeight},
                {"velocity", velocity},
                {"maxVelocity", maxVelocity},
                {"safeFallHeight", safeFallHeight},
                {"nonFatalFallHeight", nonFatalFallHeight}
            };
        }

        protected override IRGStateEntity CreateStateEntityClassInstance()
        {
            return new RGStateEntity_Platformer2DPlayer();
        }

        // ReSharper disable once InconsistentNaming
        private Vector2 GetTruePosition()
        {
            var thePosition = transform.position;
            var theCollider = gameObject.GetComponent<BoxCollider2D>();
            var colliderOffset = theCollider.offset;
            var colliderSize = theCollider.size;
            
            // bottom of the feet centered horizontally
            return new Vector2(
                thePosition.x - colliderOffset.x - colliderSize.x/2,
                thePosition.y - colliderOffset.y - colliderSize.y
            );
            
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
    }
}
