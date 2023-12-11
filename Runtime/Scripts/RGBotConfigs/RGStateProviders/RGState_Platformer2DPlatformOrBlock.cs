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
        public Vector3 size => (Vector3)this["size"];
        public new Vector3 position => (Vector3)this["position"];
        public bool breakable => (bool)this["breakable"];
        public bool movable => (bool)this["movable"];
        public bool dropthroughAble => (bool)this["dropthroughAble"];
        public bool jumpthroughAble => (bool)this["jumpthroughAble"];
    }

    /**
     * Provides state information about the tile grid in the current visible screen space.
     */
    [Serializable]
    public class RGState_Platformer2DPlatformOrBlock: RGState
    {
        public bool movable = false;
        
        public bool breakable = false;

        public bool dropthroughAble = false;

        public bool jumpthroughAble = false;

        [Tooltip("Draw debug gizmos for platform locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

        private RGState_Platformer2DLevel levelState;

        private Collider2D colliderThing;
        
        private void OnEnable()
        {
            levelState = FindObjectOfType<RGState_Platformer2DLevel>();
            colliderThing = GetComponentInChildren<Collider2D>();
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
                            if (dropthroughAble || jumpthroughAble)
                            {
                                Gizmos.DrawWireCube(new Vector3(xPosition, lp.y + cellHeight / 2, lp.z),
                                    new Vector3(cellWidth, cellHeight, 1));   
                            }
                            else
                            {
                                Gizmos.DrawWireSphere(new Vector3(xPosition, lp.y + cellHeight / 2, lp.z),
                                    cellWidth / 2);
                            }
                            xPosition += cellWidth;
                        }
                    }
                    else
                    {
                        if (dropthroughAble || jumpthroughAble)
                        {
                            Gizmos.DrawWireCube(new Vector3(lp.x + _lastSize.x / 2, lp.y + _lastSize.y / 2, lp.z),
                                new Vector3(_lastSize.x, _lastSize.y, 1));
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
        }

        private Vector3 _lastSize = Vector3.one;
        private Vector3 _lastPosition = Vector3.zero;


        protected override void PopulateRGEntityState(IRGStateEntity stateEntity)
        {
            if (colliderThing == null)
            {
                throw new Exception(
                    "RGState_Platformer2DPlatformOrBlock must have a Collider2D in its GameObject structure");
            }

            var bounds = colliderThing.bounds;
            var minBounds = bounds.min;
            _lastSize = bounds.size;
            
            // faster than adding vector3s
            _lastPosition = new Vector3(minBounds.x, minBounds.y + _lastSize.y, minBounds.z);

            // avoid all this allocation and set after first call
            if (!stateEntity.ContainsKey("jumpthroughAble"))
            {
                stateEntity["jumpthroughAble"] = jumpthroughAble;
                stateEntity["dropthroughAble"] = dropthroughAble;
                stateEntity["breakable"] = breakable;
                stateEntity["movable"] = movable;
                stateEntity["size"] = _lastSize;
                stateEntity["position"] = _lastPosition;
            }
            // set every time if movable
            if (movable)
            {
                stateEntity["position"] = _lastPosition;
            }
        }

        protected override Dictionary<string, object> GetState()
        {
            // obsolete
            return null;

        }

        protected override Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity_Platformer2DPlatformOrBlock);
        }
    }

}
