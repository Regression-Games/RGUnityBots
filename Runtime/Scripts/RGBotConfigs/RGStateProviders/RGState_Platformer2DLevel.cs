using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace RegressionGames.RGBotConfigs
{
    /**
     * Provides 2D raycast information in the state from the perspective of the
     * GameObject to which this behavior is attached
     */
    [DisallowMultipleComponent]
    [Tooltip("Provides state information about the tile grid in the current visible screen space.")]
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
                DrawDebugPositions(lastPositions, lastCellSize);
            }
        }

        private List<RGPlatformer2DPosition> lastPositions = new();
        private Vector3 lastCellSize = Vector3.one;

        protected override Dictionary<string, object> GetState()
        {
            var tileMap = gameObject.GetComponent<Tilemap>();

            var mainCamera = Camera.main;

            var cellBounds = tileMap.cellBounds;
            
            if (mainCamera != null)
            {
                var screenHeight = mainCamera.pixelHeight;
                var screenWidth = mainCamera.pixelWidth;

                var bottomLeft = mainCamera.ScreenToWorldPoint(Vector3.zero);
                var topRight = mainCamera.ScreenToWorldPoint(new Vector3(screenWidth, screenHeight, 0));

                var tileBottomLeft = tileMap.WorldToCell(bottomLeft);
                tileBottomLeft.z = cellBounds.zMin;
                var tileTopRight = tileMap.WorldToCell(topRight);
                tileTopRight.z = cellBounds.zMax;
                
                
                cellBounds = new BoundsInt(tileBottomLeft, tileTopRight - tileBottomLeft);
            }

            //performance optimization to avoid computing these over and over in the loops
            int[] minMax = new[]
            {
                cellBounds.xMin, cellBounds.xMax, cellBounds.yMin, cellBounds.yMax, cellBounds.zMin, cellBounds.zMax
            };
            Vector3Int upInt = Vector3Int.up;

            var safePositions = new List<RGPlatformer2DPosition>();
            for (int x = minMax[0]; x < minMax[1]; x++)
            {
                for (int y = minMax[2]; y < minMax[3]; y++)
                {
                    for (int z = minMax[4]; z <= minMax[5]; z++)
                    {
                        // checks for colliders to avoid decoration sprites being considered
                        var cellPlace = new Vector3Int(x, y, z);
                        var tile = tileMap.GetTile<Tile>(cellPlace);
                        if (tile is not null && tile.colliderType != Tile.ColliderType.None)
                        {
                            var heightAvailable = 0;
                            Vector3Int? finalSpotAbove = null;
                            cellPlace += upInt;
                            var tileAbove = tileMap.GetTile<Tile>(cellPlace);
                            //see if there is a tile above it or not
                            if (tileAbove is null || tileAbove.colliderType == Tile.ColliderType.None)
                            {
                                finalSpotAbove = cellPlace;
                                heightAvailable = 1;
                            }
                            
                            // check up to the tileSpaceAbove
                            for (int i = 2; i <= tileSpaceAbove; i++)
                            {
                                cellPlace += upInt;
                                tileAbove = tileMap.GetTile<Tile>(cellPlace);
                                //see if there is a tile above it or not
                                if (tileAbove is null || tileAbove.colliderType == Tile.ColliderType.None)
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
                                var spot = new RGPlatformer2DPosition
                                {
                                    tilesHeight = heightAvailable,
                                    height = heightAvailable * tileMap.cellSize.y,
                                    position = place
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
            
            lastPositions = safePositions;
            lastCellSize = tileMap.cellSize;

            return new Dictionary<string, object>()
            {
                {
                    "platformer2D", new RGStateEntity_Platformer2DLevel()
                    {
                        tileCellSize = lastCellSize,
                        platformPositions = safePositions.ToArray()
                    }
                }
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
        
        protected override IRGStateEntity CreateStateEntityClassInstance()
        {
            return new RGStateEntity_Platformer2DLevel();
        }
    }

    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntity_Platformer2DLevel : RGStateEntity<RGState_Platformer2DLevel>
    {
        
        [FormerlySerializedAs("spriteSize")]
        public Vector3 tileCellSize = Vector3.one;
        public RGPlatformer2DPosition[] platformPositions = Array.Empty<RGPlatformer2DPosition>();
    }

    [Serializable]
    public class RGPlatformer2DPosition
    {
        public Vector2 position;
        
        // number of grid tiles tall this position is (computed up to the configured max on RGState_Platformer2DLevel)
        public int tilesHeight;

        // height in world units of this space
        public float height;

        public override string ToString()
        {
            return $"Position: ({position.x}, {position.y}) , Height: {height} , TileHeight: {tilesHeight}";
        }
    }
}
