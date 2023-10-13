using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    /**
     * Provides 2D raycast information in the state from the perspective of the
     * GameObject to which this behavior is attached
     */
    [DisallowMultipleComponent]
    public class RGStateRadialRaycast2D: RGState
    {
        [Header("Raycast2D State")]
        [Tooltip("Offset from the transform position to start the ray casts from.  Useful to avoid ray casting from within the ground at the sprite's feet.")]
        public Vector2 offset = Vector2.zero;

        [Min(2)]
        [Tooltip("Rays are projected in equal angles of the 360 circle around the player always starting with a ray facing 'forward' from the player.  2 rays provides left and right.  4 rays provides left right up and down.  8 rays provides left right up and down + the 4 diagonals.  Exceeding 8 rays is usually not recommended for performance reasons, especially when there are many instances of this behavior in the scene.")]
        public int numberOfRays = 4;

        [Tooltip("The maximum distance over which to cast each ray.  This should be at least 2x the maximum jump height or distance of your character.  This should be kept as short as possible while still fulfilling your use case for performance reasons.")]
        public float distance = 30f;

        // all layers by default
        [Tooltip("Filter to detect Colliders only on certain layers.")]
        public LayerMask layerMask = Physics.DefaultRaycastLayers;

        [Tooltip("Only include objects with a Z coordinate (depth) greater than or equal to this value.")]
        public float minZDepth = -Mathf.Infinity;

        [Tooltip("Only include objects with a Z coordinate (depth) less than or equal to this value.")]
        public float maxZDepth = Mathf.Infinity;

        [Tooltip("Render visualizations of the rays and their hit points while the game is running.")]
        public bool debugVisualizations = false;

        private List<RGStateEntity2DRaycastInfo> _previousRays = new();

        protected override Dictionary<string, object> GetState()
        {
            var startPosition = transform.position + (Vector3)offset;

            var angleRadians = 2 * Mathf.PI / numberOfRays;
            
            List<RGStateEntity2DRaycastInfo> raycastHits = new();
            for (var i = 0; i < numberOfRays; i++)
            {
                var direction = new Vector2(Mathf.Sin(angleRadians * i), Mathf.Cos(angleRadians * i));
                RaycastHit2D raycastHit = Physics2D.Raycast(
                    startPosition,
                    direction,
                    distance,
                    layerMask,
                    minZDepth,
                    maxZDepth);
                raycastHits.Add( new RGStateEntity2DRaycastInfo()
                {
                    from = startPosition,
                    direction = direction,
                    hitCollider = raycastHit.collider,
                    hitPoint = raycastHit.collider != null ? raycastHit.point : null
                });
            }

            _previousRays = raycastHits;

            return new()
            {
                { "raycast2D", raycastHits.ToArray() }
            };
        }

        private void Update()
        {
            if (debugVisualizations && _previousRays.Count > 0)
            {
                foreach (var ray in _previousRays)
                {
                    Vector3 end = ray.hitPoint ?? (transform.position + (Vector3)offset + (Vector3)ray.direction * distance) ;
                    // NOTE: Requires Gizmos to be enabled in the editor
                    Debug.DrawLine(ray.from, end, (ray.hitPoint ==null ? Color.white : Color.red), 0.0f, false);
                }
            }
        }
    }
    
}
