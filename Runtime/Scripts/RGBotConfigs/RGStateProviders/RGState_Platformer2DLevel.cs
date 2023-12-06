using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{
    [Serializable]
    public class RGPlatformer2DPosition
    {
        public Vector2 position;
        
        //TODO: Remove these concepts as this only accounts for the tileMap, not other objects
        // used by pathfinding to know if this has a wall adjoining it
        public bool blockedRight = false;
        // used by pathfinding to know if this has a wall adjoining it
        public bool blockedLeft = false;
        public override string ToString()
        {
            return $"Position: ({position.x}, {position.y})";
        }
    }

    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntity_Platformer2DLevel : RGStateEntity<RGState_Platformer2DLevel>
    {
        
        /**
         * <summary>The BoundsInt of the whole tileMap</summary>
         */
        public BoundsInt tileMapBounds => (BoundsInt)
            this.GetValueOrDefault("tileMapBounds", new BoundsInt(0,0,0,0,0,0));
        
        /**
         * <summary>The BoundsInt of the current visible portion of the tileMap</summary>
         */
        public BoundsInt currentBounds => (BoundsInt)
            this.GetValueOrDefault("currentBounds", new BoundsInt(0,0,0,0,0,0));
        public Vector3 tileCellSize => (Vector3)this.GetValueOrDefault("tileCellSize", Vector3.one);
        public RGPlatformer2DPosition[] platformPositions => (RGPlatformer2DPosition[])this.GetValueOrDefault("platformPositions", Array.Empty<RGPlatformer2DPosition>());
    }
    
    /**
     * Provides state information about the tile grid in the current visible screen space.
     */
    [Serializable]
    public class RGState_Platformer2DLevel: RGState
    {
        [Tooltip("Draw debug gizmos for platform locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

        private void OnDrawGizmos()
        {
            if (renderDebugGizmos)
            {
                foreach (var platformPosition in _lastPositions)
                {
                    Gizmos.DrawWireSphere(new Vector3(platformPosition.position.x + _lastCellSize.x / 2, platformPosition.position.y + _lastCellSize.y / 2, 0),
                        _lastCellSize.x/2);
                }
            }
        }

        internal Vector3 _lastCellSize = Vector3.one;
        private List<RGPlatformer2DPosition> _lastPositions = new();

        protected override Dictionary<string, object> GetState()
        {
            var tileMap = gameObject.GetComponent<Tilemap>();

            var currentCamera = Camera.main;

            var currentBounds = tileMap.cellBounds;
            var cellBounds = currentBounds;
            
            // limit the conveyed state to the current camera space
            if (currentCamera != null)
            {
                var screenHeight = currentCamera.pixelHeight;
                var screenWidth = currentCamera.pixelWidth;

                var bottomLeft = currentCamera.ScreenToWorldPoint(Vector3.zero);
                var topRight = currentCamera.ScreenToWorldPoint(new Vector3(screenWidth, screenHeight, 0));

                var tileBottomLeft = tileMap.WorldToCell(bottomLeft);
                tileBottomLeft.z = currentBounds.zMin;
                var tileTopRight = tileMap.WorldToCell(topRight);
                tileTopRight.z = currentBounds.zMax;

                // ?? * the max size of the screen size in either dimension
                // this needs to be big enough to avoid getting 'stuck' without a path nearer to the goal
                var fractionOfX = (tileTopRight.x - tileBottomLeft.x) / 2;
                var fractionOfY = (tileTopRight.y - tileBottomLeft.y) / 2;

                var fraction = Math.Max(fractionOfX, fractionOfY);
                tileBottomLeft.x -= fraction;
                tileTopRight.x += fraction;
                
                tileBottomLeft.y -= fraction;
                tileTopRight.y += fraction;

                currentBounds = new BoundsInt(tileBottomLeft, tileTopRight - tileBottomLeft);
                
                // limit this to the edges of the tilemap itself
                tileBottomLeft.x = Math.Max(currentBounds.xMin, tileBottomLeft.x);
                tileBottomLeft.y = Math.Max(currentBounds.yMin, tileBottomLeft.y);
                
                tileTopRight.x = Math.Min(currentBounds.xMax, tileTopRight.x);
                tileTopRight.y = Math.Min(currentBounds.yMax, tileTopRight.y);
                
                cellBounds = new BoundsInt(tileBottomLeft, tileTopRight - tileBottomLeft);
            }

            //performance optimization to avoid computing these over and over in the loops
            int[] minMax = new[]
            {
                cellBounds.xMin, cellBounds.xMax, cellBounds.yMin, cellBounds.yMax, cellBounds.zMin, cellBounds.zMax
            };
            
            var safePositions = new List<RGPlatformer2DPosition>();
            for (int x = minMax[0]; x <= minMax[1]; x++)
            {
                for (int y = minMax[2]; y <= minMax[3]; y++)
                {
                    for (int z = minMax[4]; z <= minMax[5]; z++)
                    {
                        var cellY = y;
                        // checks for colliders to avoid decoration sprites being considered
                        var cellPlace = new Vector3Int(x, y, z);
                        var tileCollider = tileMap.GetColliderType(cellPlace);
                        if (tileCollider != Tile.ColliderType.None)
                        {
                            Vector3Int? finalSpotAbove = null;
                            
                            ++cellY;
                            // faster than += on Vectors
                            cellPlace = new Vector3Int(x, cellY, z);
                            tileCollider = tileMap.GetColliderType(cellPlace);
                            //see if there is a tile above it or not
                            if (tileCollider == Tile.ColliderType.None)
                            {
                                finalSpotAbove = cellPlace;
                            }
  
                            if (finalSpotAbove is not null)
                            {
                                Vector3 place = tileMap.CellToWorld((Vector3Int)finalSpotAbove);
                                var blockedRight = tileMap.GetColliderType(new Vector3Int(x+1,y+1, z)) != Tile.ColliderType.None;
                                var blockedLeft = tileMap.GetColliderType(new Vector3Int(x-1,y+1, z)) != Tile.ColliderType.None;
                                var spot = new RGPlatformer2DPosition
                                {
                                    position = place,
                                    blockedLeft = blockedLeft,
                                    blockedRight = blockedRight
                                };
                                safePositions.Add(spot);
                            }
                        }
                        else
                        {
                            //No tile at "place"
                        }
                    }
                }
            }
            
            _lastPositions = safePositions;
            _lastCellSize = tileMap.cellSize;

            return new Dictionary<string, object>()
            {
                { "tileMapBounds", tileMap.cellBounds},
                { "currentBounds", currentBounds},
                { "tileCellSize", _lastCellSize },
                { "platformPositions", _lastPositions.ToArray() }
            };
        }

        protected override Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity_Platformer2DLevel);
        }
    }

}
