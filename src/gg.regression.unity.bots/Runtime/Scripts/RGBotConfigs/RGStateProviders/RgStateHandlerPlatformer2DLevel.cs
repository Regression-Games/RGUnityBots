using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RegressionGames.RGBotConfigs.RGStateProviders
{
    [Serializable]
    public class RgPlatformer2DPosition
    {
        public Vector2 position;

        // used by pathfinding to know if this has a wall adjoining it
        public bool blockedRight;
        // used by pathfinding to know if this has a wall adjoining it
        public bool blockedLeft;

        public override string ToString()
        {
            return $"Position: ({position.x}, {position.y})";
        }
    }

    /**
     * Provides state information about the tile grid in the current visible screen space.
     */
    [Serializable]
    public class RgStateHandlerPlatformer2DLevel : MonoBehaviour
    {
        [Tooltip("How many tile spaces above a platform to consider when determining the height available on top of a platform.  This should match the height of the largest character model navigating the scene in tile units.")]
        [Min(1)]
        public int tileSpaceAbove = 2;

        [NonSerialized]
        [Tooltip("Draw debug gizmos for platform locations in editor runtime ?")]
        public bool renderDebugGizmos = true;

        public Vector3 tileCellSize = Vector3.one;
        public List<RgPlatformer2DPosition> platformPositions = new();
        public BoundsInt tileMapBounds = new BoundsInt();
        public BoundsInt currentBounds = new BoundsInt();

        [NonSerialized]
        [Tooltip("Update state every X seconds.")]
        public float updateInterval = 1.0f;

        private float _lastUpdateTime = float.MinValue;
        public void OnEnable()
        {
            Update();
        }

        public void Update()
        {
            var time = Time.time;
            if (time - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = time;
                UpdateState();
            }
        }

        public void UpdateState()
        {
            var tileMap = gameObject.GetComponent<Tilemap>();

            var currentCamera = Camera.main;

            var theCurrentBounds = tileMap.cellBounds;
            var cellBounds = theCurrentBounds;

            if (currentCamera != null)
            {
                var screenHeight = currentCamera.pixelHeight;
                var screenWidth = currentCamera.pixelWidth;

                var bottomLeft = currentCamera.ScreenToWorldPoint(Vector3.zero);
                var topRight = currentCamera.ScreenToWorldPoint(new Vector3(screenWidth, screenHeight, 0));

                var tileBottomLeft = tileMap.WorldToCell(bottomLeft);
                tileBottomLeft.z = theCurrentBounds.zMin;
                var tileTopRight = tileMap.WorldToCell(topRight);
                tileTopRight.z = theCurrentBounds.zMax;

                // some amount bigger than the max size of the screen size in either dimension
                // this needs to be big enough to avoid getting 'stuck' without a path nearer to the goal
                // but not so huge that we destroy the framerate considering irrelevant information
                var fractionOfX = (tileTopRight.x - tileBottomLeft.x) / 2;
                var fractionOfY = (tileTopRight.y - tileBottomLeft.y) / 2;

                var fraction = Math.Max(fractionOfX, fractionOfY);
                tileBottomLeft.x -= fraction;
                tileTopRight.x += fraction;

                // limit y to the bottom of the tilemap
                tileBottomLeft.y = Math.Max(tileBottomLeft.y - fraction, cellBounds.yMin);

                tileTopRight.y += fraction;

                theCurrentBounds = new BoundsInt(tileBottomLeft, tileTopRight - tileBottomLeft);

                // limit this to the edges of the tilemap itself
                var cellBoundsBottomLeft = new Vector3Int(Math.Max(theCurrentBounds.xMin, cellBounds.xMin),
                    Math.Max(theCurrentBounds.yMin, cellBounds.yMin), cellBounds.zMin);

                var cellBoundsTopRight = new Vector3Int(Math.Min(theCurrentBounds.xMax, cellBounds.xMax),
                    Math.Min(theCurrentBounds.yMax, cellBounds.yMax), cellBounds.zMax);

                cellBounds = new BoundsInt(cellBoundsBottomLeft, cellBoundsTopRight - cellBoundsBottomLeft);
            }

            //performance optimization to avoid computing these over and over in the loops
            int[] minMax = new[]
            {
                cellBounds.xMin, cellBounds.xMax, cellBounds.yMin, cellBounds.yMax, cellBounds.zMin, cellBounds.zMax
            };

            var safePositions = new List<RgPlatformer2DPosition>();
            // avoid re-constructing many Vector3s for perf and GC
            var cellPlace = Vector3Int.zero;
            for (int x = minMax[0]; x <= minMax[1]; x++)
            {
                for (int y = minMax[2]; y <= minMax[3]; y++)
                {
                    for (int z = minMax[4]; z <= minMax[5]; z++)
                    {
                        var cellY = y;
                        // checks for colliders to avoid decoration sprites being considered

                        cellPlace.Set(x, y, z);

                        var tileCollider = tileMap.GetColliderType(cellPlace);
                        if (tileCollider != Tile.ColliderType.None)
                        {
                            Vector3Int? finalSpotAbove = null;

                            ++cellY;

                            cellPlace.y = cellY;
                            tileCollider = tileMap.GetColliderType(cellPlace);
                            //see if there is a tile above it or not
                            if (tileCollider == Tile.ColliderType.None)
                            {
                                finalSpotAbove = cellPlace;
                            }

                            if (finalSpotAbove != null)
                            {
                                Vector3 place = tileMap.CellToWorld((Vector3Int)finalSpotAbove);
                                // avoid constructing or adding vectors if we can... performance reasons
                                cellPlace.Set(x + 1, y + 1, z);
                                var blockedRight = tileMap.GetColliderType(cellPlace) != Tile.ColliderType.None;
                                // avoid constructing or adding vectors if we can... performance reasons
                                cellPlace.x = x - 1;
                                var blockedLeft = tileMap.GetColliderType(cellPlace) != Tile.ColliderType.None;
                                var spot = new RgPlatformer2DPosition
                                {
                                    position = place,
                                    blockedLeft = blockedLeft,
                                    blockedRight = blockedRight
                                };
                                safePositions.Add(spot);
                            }
                        }
                    }
                }
            }

            platformPositions = safePositions;
            tileCellSize = tileMap.cellSize;
            tileMapBounds = tileMap.cellBounds;
            this.currentBounds = theCurrentBounds;
        }

        private void OnDrawGizmos()
        {
            if (renderDebugGizmos)
            {
                foreach (var platformPosition in platformPositions)
                {
                    Gizmos.DrawWireSphere(new Vector3(
                            platformPosition.position.x + tileCellSize.x / 2,
                            platformPosition.position.y + tileCellSize.y / 2,
                            0
                            ),
                        tileCellSize.x / 2);
                }
            }
        }

    }



}
