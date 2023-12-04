using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Tilemaps;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{
    [Serializable]
    public class RGPlatformer2DPosition
    {
        public Vector2 position;
        
        // number of grid tiles tall this position is (computed up to the configured max on RGState_Platformer2DLevel)
        public int tilesHeight;

        // used by pathfinding to know if this has a wall adjoining it
        public bool blockedRight = false;
        // used by pathfinding to know if this has a wall adjoining it
        public bool blockedLeft = false;
        
        //worldspace Height
        public float Height(float tileHeight) => tileHeight * tileHeight;
        
        public override string ToString()
        {
            return $"Position: ({position.x}, {position.y}) , TileHeight: {tilesHeight}";
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
        [Tooltip("How many tile spaces above a platform to consider when determining the height available on top of a platform.  This should match the height of the largest character model navigating the scene in tile units.")]
        [Min(1)]
        public int tileSpaceAbove = 2;
        
        [Tooltip("Draw debug gizmos for platform locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

        private void OnDrawGizmos()
        {
            if (renderDebugGizmos)
            {
                DrawDebugPositions(_lastPositions, _lastCellSize);
            }
        }

        private Vector3 _lastCellSize = Vector3.one;
        private List<RGPlatformer2DPosition> _lastPositions = new();

        protected override Dictionary<string, object> GetState()
        {
            var tileMap = gameObject.GetComponent<Tilemap>();

            var currentCamera = Camera.main;

            var cellBounds = tileMap.cellBounds;
            
            // limit the conveyed state to the current camera space
            if (currentCamera != null)
            {
                var screenHeight = currentCamera.pixelHeight;
                var screenWidth = currentCamera.pixelWidth;

                var bottomLeft = currentCamera.ScreenToWorldPoint(Vector3.zero);
                var topRight = currentCamera.ScreenToWorldPoint(new Vector3(screenWidth, screenHeight, 0));

                var tileBottomLeft = tileMap.WorldToCell(bottomLeft);
                tileBottomLeft.z = cellBounds.zMin;
                var tileTopRight = tileMap.WorldToCell(topRight);
                tileTopRight.z = cellBounds.zMax;

                // ?? * the max size of the screen size in either dimension
                // this needs to be big enough to avoid getting 'stuck' without a path nearer to the goal
                var fractionOfX = (tileTopRight.x - tileBottomLeft.x) / 2;
                var fractionOfY = (tileTopRight.y - tileBottomLeft.y) / 2;

                var fraction = Math.Max(fractionOfX, fractionOfY);
                tileBottomLeft.x -= fraction;
                tileTopRight.x += fraction;
                
                tileBottomLeft.y -= fraction;
                tileTopRight.y += fraction;
                
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
                            var heightAvailable = 0;
                            Vector3Int? finalSpotAbove = null;
                            
                            ++cellY;
                            // faster than += on Vectors
                            cellPlace = new Vector3Int(x, cellY, z);
                            tileCollider = tileMap.GetColliderType(cellPlace);
                            //see if there is a tile above it or not
                            if (tileCollider == Tile.ColliderType.None)
                            {
                                finalSpotAbove = cellPlace;
                                heightAvailable = 1;
                            }
                            
                            // check up to the tileSpaceAbove
                            for (int i = 2; i <= tileSpaceAbove; i++)
                            {
                                ++cellY;
                                // faster than += on Vectors
                                cellPlace = new Vector3Int(x, cellY, z);
                                tileCollider = tileMap.GetColliderType(cellPlace);
                                //see if there is a tile above it or not
                                if (tileCollider == Tile.ColliderType.None)
                                {
                                    ++heightAvailable;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (finalSpotAbove is not null)
                            {
                                Vector3 place = tileMap.CellToWorld((Vector3Int)finalSpotAbove);
                                var blockedRight = tileMap.GetColliderType(new Vector3Int(x+1,y+1, z)) != Tile.ColliderType.None;
                                var blockedLeft = tileMap.GetColliderType(new Vector3Int(x-1,y+1, z)) != Tile.ColliderType.None;
                                var spot = new RGPlatformer2DPosition
                                {
                                    tilesHeight = heightAvailable,
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
                { "currentBounds", cellBounds},
                { "tileCellSize", _lastCellSize },
                { "platformPositions", _lastPositions.ToArray() }
            };
        }

        private void DrawDebugPositions(List<RGPlatformer2DPosition> platformPositions, Vector3 cellSize)
        {
            foreach (var platformPosition in platformPositions)
            {
                Gizmos.DrawWireSphere(new Vector3(platformPosition.position.x + cellSize.x / 2, platformPosition.position.y + cellSize.y / 2, 0),
                    cellSize.x/2);
            }
        }

        protected override Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity_Platformer2DLevel);
        }
    }
    

    
}
