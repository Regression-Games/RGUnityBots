using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RegressionGames.RGBotConfigs
{
    /**
     * Provides 2D raycast information in the state from the perspective of the
     * GameObject to which this behavior is attached
     */
    [DisallowMultipleComponent]
    [Tooltip("Provides state information about the tile grid in the current visible screen space.")]
    public class RGStatePlatformer2D: RGState
    {
        [Tooltip("Draw debug gizmos for platform locations in editor runtime ?")]
        public bool renderDebugGizmos = true;
        
        private void OnDrawGizmos()
        {
            if (renderDebugGizmos)
            {
                DrawDebugPositions(lastPositions, lastCellSize);
            }
        }

        private List<Vector2> lastPositions = new();
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
            
            

            var safePositions = new List<Vector2>();
            for (int x = cellBounds.xMin; x < cellBounds.xMax; x++)
            {
                for (int y = cellBounds.yMin; y < cellBounds.yMax; y++)
                {
                    for (int z = cellBounds.zMin; z <= cellBounds.zMax; z++)
                    {
                        // checks for colliders to avoid decoration sprites being considered
                        var cellPlace = new Vector3Int(x, y, z);
                        var tile = tileMap.GetTile<UnityEngine.Tilemaps.Tile>(cellPlace);
                        if (tile != null && tile.colliderType  != Tile.ColliderType.None)
                        {
                            var cellPlaceAbove = cellPlace + Vector3Int.up;
                            var tileAbove = tileMap.GetTile<UnityEngine.Tilemaps.Tile>(cellPlaceAbove);
                            //Tile at "place", see if there is a tile above it or not before adding
                            if (tileAbove == null || tileAbove.colliderType == Tile.ColliderType.None)
                            {
                                Vector3 place = tileMap.CellToWorld(cellPlaceAbove);
                                safePositions.Add(place);
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
                    "platformer2D", new RGStateEntityPlatformer2D()
                    {
                        spriteSize = lastCellSize,
                        platformPositions = safePositions.ToArray()
                    }
                }
            };
        }

        private void DrawDebugPositions(List<Vector2> platformPositions, Vector3 cellSize)
        {
            foreach (var platformPosition in platformPositions)
            {
                Gizmos.DrawWireSphere(new Vector3(platformPosition.x + cellSize.x / 2, platformPosition.y + cellSize.y / 2, 0),
                    cellSize.x/2);
            }
        }
    }
}
