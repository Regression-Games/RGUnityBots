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

        private List<List<RGStateEntity2DRaycastInfo>> _previousRays = new();

        private void Start()
        {
            rayDistance = rayStepCount * spriteWidth;
        }

        protected override Dictionary<string, object> GetState()
        {
            RGStateEntityPlatformer2D platformer2D = new()
            {
                spriteWidth = spriteWidth
            };

            List<RGStateEntity2DRaycastInfo> centerList = new();
            for (var i = 1; i <= rayStepCount; i++)
            {
                evaluateRay(i, Vector2.left, Vector2.down, platformer2D.leftdown);
                evaluateRay(i, Vector2.left, Vector2.up, platformer2D.leftup);
                evaluateRay(i, Vector2.right, Vector2.down, platformer2D.rightdown);
                evaluateRay(i, Vector2.right,Vector2.up, platformer2D.rightup);
            }
            
            platformer2D.down = evaluateRay(0, Vector2.right,Vector2.down, centerList);
            platformer2D.up = evaluateRay(0, Vector2.right,Vector2.up, centerList);
            
            for (var i = 1; i <= rayStepCount; i++)
            {
                evaluateRay(i, Vector2.up, Vector2.left, platformer2D.upleft);
                evaluateRay(i, Vector2.up, Vector2.right, platformer2D.upright);
                evaluateRay(i, Vector2.down, Vector2.left, platformer2D.downleft);
                evaluateRay(i, Vector2.down,Vector2.right, platformer2D.downright);
            }
            
            platformer2D.left = evaluateRay(0, Vector2.up,Vector2.left, centerList);
            platformer2D.right = evaluateRay(0, Vector2.up,Vector2.right, centerList);

            _previousRays = new List<List<RGStateEntity2DRaycastInfo>>
            {
                platformer2D.leftdown,
                platformer2D.leftup,
                platformer2D.rightdown,
                platformer2D.rightup,
                
                platformer2D.upleft,
                platformer2D.upright,
                platformer2D.downleft,
                platformer2D.downright,

                centerList
            };

            return new Dictionary<string, object>
            {
                { "platformer2D", platformer2D}
            };
        }

        private RGStateEntity2DRaycastInfo evaluateRay(int index, Vector2 offsetDirection, Vector2 direction, List<RGStateEntity2DRaycastInfo> raycastHits)
        {
            var startPosition = transform.position + (Vector3)offset + (Vector3)(offsetDirection * (index * spriteWidth));
            RaycastHit2D raycastHit = Physics2D.Raycast(
                startPosition,
                direction,
                rayDistance,
                layerMask,
                minZDepth,
                maxZDepth);

            string objectType = null;
            int? objectId = null;
            if (raycastHit.collider != null)
            {
                var entity = raycastHit.collider.gameObject.GetComponent<RGEntity>();
                if (entity != null)
                {
                    objectType = entity.objectType;
                    objectId = entity.transform.GetInstanceID();
                }
            }

            var result = new RGStateEntity2DRaycastInfo()
            {
                from = startPosition,
                direction = direction,
                hitCollider = raycastHit.collider,
                hitPoint = raycastHit.collider != null ? raycastHit.point : null,
                hitObjectId = objectId,
                hitObjectType = objectType
            };
            raycastHits.Add(result);
            return result;
        }
        
        private void Update()
        {
            if (debugVisualizations)
            {
                if (_previousRays.Count > 0)
                {
                    foreach (var raycasts in _previousRays)
                    {
                        foreach (var ray in raycasts)
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
