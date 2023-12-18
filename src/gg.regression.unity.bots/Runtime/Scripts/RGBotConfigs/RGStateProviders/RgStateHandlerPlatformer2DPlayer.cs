using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using RGBotConfigs.RGStateProviders;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RgStateEntityBasePlatformer2DPlayer : Dictionary<string,object>, IRGStateEntity
    {
        public float jumpHeight => (float)this["jumpHeight"];
        public float maxJumpHeight => (float)this["maxJumpHeight"];
        public float velocity => (float)this["velocity"];
        public float maxVelocity => (float)this["maxVelocity"];
        public float safeFallHeight => (float)this["safeFallHeight"];
        public float nonFatalFallHeight => (float)this["nonFatalFallHeight"];
        public string GetEntityType()
        {
            return EntityTypeName;
        }

        public bool GetIsPlayer()
        {
            return true;
        }

        public static readonly string EntityTypeName = "platformer2DPlayer";
    }
    
    [Serializable]
    public class RgStateHandlerPlatformer2DPlayer : RGStateBehaviour<RgStateEntityBasePlatformer2DPlayer>
    {
        [NonSerialized]
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
        
        protected override RgStateEntityBasePlatformer2DPlayer CreateStateEntityInstance()
        {
            return new RgStateEntityBasePlatformer2DPlayer();
        }

        protected override void PopulateStateEntity(RgStateEntityBasePlatformer2DPlayer stateEntity)
        {
            if (_statsProvider != null)
            {
                jumpHeight = _statsProvider.JumpHeight();
                maxJumpHeight = _statsProvider.MaxJumpHeight();
                velocity = _statsProvider.Velocity();
                maxVelocity = _statsProvider.MaxVelocity();
                safeFallHeight = _statsProvider.SafeFallHeight();
                nonFatalFallHeight = _statsProvider.NonFatalFallHeight();
            }
            _truePosition = GetTruePosition();
            stateEntity["position"] = (Vector3)(Vector2)_truePosition;
            stateEntity["jumpHeight"] = jumpHeight;
            stateEntity["maxJumpHeight"] = maxJumpHeight;
            stateEntity["velocity"] = velocity;
            stateEntity["maxVelocity"] = maxVelocity;
            stateEntity["safeFallHeight"] = safeFallHeight;
            stateEntity["nonFatalFallHeight"] = nonFatalFallHeight;
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
            if (_truePosition != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere((Vector2)_truePosition, 0.125f);
            }
        }
        
    }
    
}
