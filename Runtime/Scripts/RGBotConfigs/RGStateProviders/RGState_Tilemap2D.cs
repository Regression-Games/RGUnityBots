using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{
    [Serializable]
    public class RGTilemap2DPosition
    {
        public Vector3 worldPosition;
        public Vector3Int cellPosition;

        public override string ToString()
        {
            return $"WorldPosition: ({worldPosition.x}, {worldPosition.y}), CellPosition: ({cellPosition.x}, {cellPosition.y})";
        }
    }

    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntity_Tilemap2D : RGStateEntity<RGState_Tilemap2D>
    {
        public Vector3 tileCellSize => (Vector3)this.GetValueOrDefault("tileCellSize", Vector3.one);
        public RGTilemap2DPosition[] platformPositions => (RGTilemap2DPosition[])this.GetValueOrDefault("tilePositions", Array.Empty<RGTilemap2DPosition>());
    }
    
    /**
     * Provides state information about the tile grid in the current visible screen space.
     */
    [Serializable]
    public class RGState_Tilemap2D: RGState
    {
        [Tooltip("Include only cells that have a collider?  Can be used to ignore decoration sprites in maps that include both.  Default: False")]
        public bool includeOnlyCollidableCells = false;

        [NonSerialized]
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
        private List<RGTilemap2DPosition> _lastPositions = new();

        protected override Dictionary<string, object> GetState()
        {
            var tileMap = gameObject.GetComponent<Tilemap>();

            var cellBounds = tileMap.cellBounds;

            //performance optimization to avoid computing these over and over in the loops
            int[] minMax = new[]
            {
                cellBounds.xMin, cellBounds.xMax, cellBounds.yMin, cellBounds.yMax, cellBounds.zMin, cellBounds.zMax
            };
            Vector3Int upInt = Vector3Int.up;

            var safePositions = new List<RGTilemap2DPosition>();
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
                            Vector3 place = tileMap.CellToWorld(cellPlace);
                            var spot = new RGTilemap2DPosition
                            {
                                worldPosition = place,
                                cellPosition = cellPlace
                            };
                            safePositions.Add(spot);
                        }
                    }
                }
            }
            
            _lastPositions = safePositions;
            _lastCellSize = tileMap.cellSize;

            return new Dictionary<string, object>()
            {
                { "tileCellSize", _lastCellSize },
                { "tilePositions", _lastPositions.ToArray() }
            };
        }

        private void DrawDebugPositions(List<RGTilemap2DPosition> platformPositions, Vector3 cellSize)
        {
            foreach (var platformPosition in platformPositions)
            {
                Gizmos.DrawWireSphere(new Vector3(platformPosition.worldPosition.x + cellSize.x / 2, platformPosition.worldPosition.y + cellSize.y / 2, 0),
                    cellSize.x/2);
            }
        }

        protected override Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity_Tilemap2D);
        }
    }
    

    
}