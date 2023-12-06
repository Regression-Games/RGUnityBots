using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{

    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntity_Platformer2DPlatformOrBlock : RGStateEntity<RGState_Platformer2DPlatformOrBlock>
    {
        public Vector3 size => (Vector3)this.GetValueOrDefault("size", Vector3.zero);
        public new Vector3 position => (Vector3)this.GetValueOrDefault("position", Vector3.zero);
        public bool breakable => (bool)this.GetValueOrDefault("breakable", false);
    }
    
    /**
     * Provides state information about the tile grid in the current visible screen space.
     */
    [Serializable]
    public class RGState_Platformer2DPlatformOrBlock: RGState
    {
        
        public bool breakable = false;
        
        [Tooltip("Draw debug gizmos for platform locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

        private RGState_Platformer2DLevel levelState;
        
        private void OnEnable()
        {
            levelState = FindObjectOfType<RGState_Platformer2DLevel>();
        }

        private void OnDrawGizmos()
        {
            if (renderDebugGizmos)
            {
                if (_lastPosition != null)
                {
                    var lp = (Vector3)_lastPosition;
                    if (levelState != null)
                    {
                        // draw the right circles to represent all the nodes
                        var width = _lastSize.x;
                        var cellWidth = levelState._lastCellSize.x;
                        var cellHeight = levelState._lastCellSize.y;
                        var xPosition = lp.x + cellWidth / 2;
                        while (xPosition < lp.x + width)
                        {
                            Gizmos.DrawWireSphere(new Vector3(xPosition, lp.y + cellHeight / 2, lp.z),
                                cellWidth / 2);
                            xPosition += cellWidth;
                        }
                    }
                    else
                    {
                        // just draw one big guess circle
                        Gizmos.DrawWireSphere(new Vector3(lp.x + _lastSize.x / 2, lp.y + _lastSize.y / 2, lp.z),
                            _lastSize.x / 2);
                    }
                }
            }
        }

        private Vector3 _lastSize = Vector3.one;
        private Vector3? _lastPosition = null;

        protected override Dictionary<string, object> GetState()
        {

            var colliderThing = GetComponentInChildren<Collider2D>();
            if (colliderThing == null)
            {
                throw new Exception(
                    "RGState_Platformer2DPlatformOrBlock must have a Collider2D in its GameObject structure");
            }

            var bounds = colliderThing.bounds;
            var minBounds = bounds.min;
            var size = bounds.size;
            // faster than adding vector3s
            _lastPosition = new Vector3(minBounds.x, minBounds.y + size.y, minBounds.z);
            _lastSize = size;

            return new Dictionary<string, object>()
            {
                { "breakable", breakable},
                { "size", _lastSize },
                { "position", _lastPosition }
            };
        }

        protected override Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity_Platformer2DPlatformOrBlock);
        }
    }

}
