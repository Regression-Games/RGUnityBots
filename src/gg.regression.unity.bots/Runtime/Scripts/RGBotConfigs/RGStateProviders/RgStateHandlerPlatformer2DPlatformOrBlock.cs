using System;
using RegressionGames.RGBotConfigs.RGStateProviders;
using UnityEngine;

namespace RGBotConfigs.RGStateProviders
{

    /**
     * Provides state information about the tile grid in the current visible screen space.
     */
    [Serializable]
    public class RgStateHandlerPlatformer2DPlatformOrBlock : MonoBehaviour
    {

        [NonSerialized]
        [Tooltip("Draw debug gizmos for platform locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

        public Vector3 size = Vector3.one;
        public Vector3 position = Vector3.zero;

        public bool movable;
        public bool breakable;
        public bool dropthroughAble;
        public bool jumpthroughAble;

        private RgStateHandlerPlatformer2DLevel _levelState;

        private Collider2D _colliderThing;

        public void OnEnable()
        {
            _levelState = FindObjectOfType<RgStateHandlerPlatformer2DLevel>();
            _colliderThing = GetComponentInChildren<Collider2D>();
            UpdateState();
        }

        public void UpdateState()
        {
            if (_colliderThing == null)
            {
                throw new Exception(
                    "RGState_Platformer2DPlatformOrBlock must have a Collider2D in its GameObject structure");
            }

            var bounds = _colliderThing.bounds;
            var minBounds = bounds.min;
            size = bounds.size;

            // faster than adding vector3s
            position = new Vector3(minBounds.x, minBounds.y + size.y, minBounds.z);

        }

        private void OnDrawGizmos()
        {
            if (renderDebugGizmos)
            {
                var lp = position;
                if (_levelState != null)
                {
                    // draw the right circles to represent all the nodes
                    var width = size.x;
                    var cellWidth = _levelState.tileCellSize.x;
                    var cellHeight = _levelState.tileCellSize.y;
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
                        Gizmos.DrawWireCube(new Vector3(lp.x + size.x / 2, lp.y + size.y / 2, lp.z),
                            new Vector3(size.x, size.y, 1));
                    }
                    else
                    {
                        // just draw one big guess circle
                        Gizmos.DrawWireSphere(new Vector3(lp.x + size.x / 2, lp.y + size.y / 2, lp.z),
                            size.x / 2);
                    }
                }
            }
        }
    }
}
