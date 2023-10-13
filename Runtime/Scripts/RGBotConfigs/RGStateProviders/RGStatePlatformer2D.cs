using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    /**
     * Provides 2D raycast information in the state from the perspective of the
     * GameObject to which this behavior is attached
     */
    [DisallowMultipleComponent]
    public class RGStatePlatformer2D: RGState
    {
        [Header("Platformer 2D")]
        
        [Tooltip("Render visualizations of the detection rays while the game is running in the editor (Requires Gizmos enabled).")]
        public bool debugVisualizations = true;

        [Header("Platformer 2D Raycasts")]
        
        [Tooltip("Offset from transform.position to center raycasts")]
        public Vector2 offset = Vector2.zero;
        
        [Min(1)]
        [Tooltip("Number of rays to check on each side up and down.")]
        public int rayStepCount = 3;

        [Tooltip("The width of the sprite grid.  Used to determine ray distance stepping")]
        public float spriteWidth = 1f;

        //[Tooltip("Distance to cast each ray. Computed based on rayStepCount and spriteWidth.")]
        private float rayDistance = 3f;
        
        // all layers by default
        [Tooltip("Filter to detect Colliders only on certain layers.")]
        public LayerMask layerMask = Physics.DefaultRaycastLayers;

        [Tooltip("Only include objects with a Z coordinate (depth) greater than or equal to this value.")]
        public float minZDepth = -Mathf.Infinity;

        [Tooltip("Only include objects with a Z coordinate (depth) less than or equal to this value.")]
        public float maxZDepth = Mathf.Infinity;

        private Dictionary<string, List<RGStateEntity2DRaycastInfo>> _previousRays = new();

        private void Start()
        {
            rayDistance = rayStepCount * spriteWidth;
        }

        protected override Dictionary<string, object> GetState()
        {
            Dictionary<string, List<RGStateEntity2DRaycastInfo>> rays = new();

            List<RGStateEntity2DRaycastInfo> raycastHits = new();
            rays["leftdown"] = raycastHits;
            raycastHits = new();
            rays["leftup"] = raycastHits;
            raycastHits = new();
            rays["rightdown"] = raycastHits;
            raycastHits = new();
            rays["rightup"] = raycastHits;
            raycastHits = new();
            rays["centerdown"] = raycastHits;
            raycastHits = new();
            rays["centerup"] = raycastHits;
            
            rays["upleft"] = raycastHits;
            raycastHits = new();
            rays["upright"] = raycastHits;
            raycastHits = new();
            rays["downleft"] = raycastHits;
            raycastHits = new();
            rays["downright"] = raycastHits;
            raycastHits = new();
            rays["centerleft"] = raycastHits;
            raycastHits = new();
            rays["centerright"] = raycastHits;
            
            for (var i = 1; i <= rayStepCount; i++)
            {
                evaluateRay(i, Vector2.left, Vector2.down, rays["leftdown"]);
                evaluateRay(i, Vector2.left, Vector2.up, rays["leftup"]);
                evaluateRay(i, Vector2.right, Vector2.down, rays["rightdown"]);
                evaluateRay(i, Vector2.right,Vector2.up, rays["rightup"]);
            }
            
            evaluateRay(0, Vector2.right,Vector2.down, rays["centerdown"]);
            evaluateRay(0, Vector2.right,Vector2.up, rays["centerup"]);
            
            for (var i = 1; i <= rayStepCount; i++)
            {
                evaluateRay(i, Vector2.up, Vector2.left, rays["upleft"]);
                evaluateRay(i, Vector2.up, Vector2.right, rays["upright"]);
                evaluateRay(i, Vector2.down, Vector2.left, rays["downleft"]);
                evaluateRay(i, Vector2.down,Vector2.right, rays["downright"]);
            }
            
            evaluateRay(0, Vector2.up,Vector2.left, rays["centerleft"]);
            evaluateRay(0, Vector2.up,Vector2.right, rays["centerright"]);

            _previousRays = rays;

            return new Dictionary<string, object>
            {
                { "platformer2D", 
                    new Dictionary<string,object>
                    {
                        {"raycasts", rays}
                    }
                }
            };
        }

        private void evaluateRay(int index, Vector2 offsetDirection, Vector2 direction, List<RGStateEntity2DRaycastInfo> raycastHits)
        {
            var startPosition = transform.position + (Vector3)offset + (Vector3)(offsetDirection * (index * spriteWidth));
            RaycastHit2D raycastHit = Physics2D.Raycast(
                startPosition,
                direction,
                rayDistance,
                layerMask,
                minZDepth,
                maxZDepth);
            
            raycastHits.Add(new RGStateEntity2DRaycastInfo()
            {
                from = startPosition,
                direction = direction,
                hitCollider = raycastHit.collider,
                hitPoint = raycastHit.collider != null ? raycastHit.point : null
            });
        }
        
        private void Update()
        {
            if (debugVisualizations)
            {
                if (_previousRays.Count > 0)
                {
                    foreach (var raycasts in _previousRays)
                    {
                        foreach (var ray in raycasts.Value)
                        {
                            Vector3 end = ray.hitPoint ?? (ray.from + ray.direction * rayDistance);
                            // NOTE: Requires Gizmos to be enabled in the editor
                            Debug.DrawLine(ray.from, end, (ray.hitPoint == null ? Color.white : Color.red), 0.0f,
                                false);
                        }
                    }
                }
            }
        }
    }

}
