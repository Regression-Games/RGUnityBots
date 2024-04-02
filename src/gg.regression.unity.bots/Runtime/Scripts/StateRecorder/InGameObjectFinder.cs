using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using StateRecorder.Types;
using UnityEngine;
using Component = UnityEngine.Component;

namespace RegressionGames.StateRecorder
{
    public class TransformStatus
    {
        public bool? HasKeyTypes;
        public string Path;
        public string TypeFullName;
        /**
         * <summary>cached pointer to the top level transform of this transform.. must check != null to avoid stale unity object references</summary>
         */
        public Transform TopLevelForThisTransform;
    }

    public class InGameObjectFinder : MonoBehaviour
    {
        private static InGameObjectFinder _this;

        [Tooltip(
            "Collapse all renderers into their top level gameObject. If a gameObject hierarchy exists that has colliders/renderers/animators/etc at multiple levels, they will all be represented by a single entry in the state.  This defaults to True as it is normally desired to see each player, car, building, etc as a single entry in the state.  However, it can be useful to set this to false in cases where you want to validate individual render bounds, colliders, rigibodies, etc on individual armatures, weapons, or other components that are children in the hierarchy.")]
        /*
-         * Objects will be grouped based on the highest things in the hierarchy that have colliders/renderers/animators/particle-systems/rigibodies/etc.
-         * If everything in your scene is under a single parent with one of these types (highly unlikely) .. then you're gonna have a bad time OR would need to set this to false and just deal with the granular object tracking.
-         * The other case that currently doesn't work perfectly as expected OOB is if you have a lot of things parented on an empty gameObject without one of those components. Today those will show up as different entries in the state. That is 'fine', just not exactly what they may want.
-         * Future: I expect us to add a 'RGStatefulParent' behaviour you can add to a game object to identify it as the 'top' of a game object tree to fix both of these cases when/if your game layout requires it. This behaviour would have no code in it, but would be a marker for the hierarchy search to use as a stopping point when walking up the tree.
-         */
        public bool collapseRenderersIntoTopLevelGameObject = true;

        [Tooltip("Include only objects that are within the render bounds of the main camera view.")]
        public bool includeOnlyOnCameraObjects = true;

        [Tooltip("(WARNING: Performance Impact) Include field/property values for behaviours.")]
        public bool collectStateFromBehaviours;

        // right now this resets on awake, but we may have to deal with dynamically re-parented transforms better at some point...
        // <transform, (hasKeyTypes, isTopLevelParent)>
        private readonly Dictionary<Transform, TransformStatus> _transformsIveSeen = new();

        public void Awake()
        {
            if (_this != null)
            {
                _this._transformsIveSeen.Clear();
                // only allow 1 of these to be alive
                if (_this.gameObject != gameObject)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            // keep this thing alive across scenes
            DontDestroyOnLoad(gameObject);
            _this = this;
        }

        public static InGameObjectFinder GetInstance()
        {
            return _this;
        }

        private string FastTrim(string input)
        {
            var index = input.Length - 1;
            while (input[index] == ' ')
            {
                --index;
            }

            if (index != input.Length - 1)
            {
                return input.Substring(0, index + 1);
            }

            return input;
        }

        private string SanitizeObjectName(string objectName)
        {
            objectName = FastTrim(objectName);
            //Removes '(Clone)' and ' (1)' uniqueness numbers for copies
            // may also remove some valid naming pieces like (TMP).. but oh well REGEX for this performed horribly
            while (objectName.EndsWith(')'))
            {
                var li = objectName.LastIndexOf('(');
                if (li > 0)
                {
                    objectName = FastTrim(objectName.Substring(0, li));
                }
            }

            return objectName;
        }

        private TransformStatus GetUniqueTransformPath(Transform theTransform, [CanBeNull] Behaviour behaviour = null)
        {
            string tPath = null;

            if (_transformsIveSeen.TryGetValue(theTransform, out var status))
            {
                if (status.Path != null)
                {
                    tPath = status.Path;
                }
            }

            if (tPath == null)
            {
                // now .. get the path in the scene.. but only from 1 level down
                // iow.. ignore the name of the scene itself for cases where many scenes are loaded together like bossroom
                var tName = SanitizeObjectName(theTransform.name);

                tPath = tName;
                var parent = theTransform.parent;
                // don't need overloaded unity != null-ness check as if we were found alive, our parent is alive
                while (parent is not null)
                {
                    tPath = string.Concat(SanitizeObjectName(parent.gameObject.name), "/", tPath);
                    parent = parent.transform.parent;
                }

                if (status != null)
                {
                    // update the cache our result
                    status.Path = tPath;
                }
                else
                {
                    status = new TransformStatus
                    {
                        Path = tPath,
                    };
                    // add our result to the cache
                    _transformsIveSeen[theTransform] = status;
                }
            }

            status.TypeFullName = behaviour != null ? behaviour.GetType().FullName : null;

            return status;
        }

        private BehaviourState GetStateForBehaviour(Behaviour behaviour)
        {
            var tStatus = GetUniqueTransformPath(behaviour.transform, behaviour);
            return new BehaviourState
            {
                path = tStatus.Path,
                name = tStatus.TypeFullName,
                state = behaviour
            };
        }

        private RecordedGameObjectState CreateStateForTransform(bool replay, int screenWidth, int screenHeight, Transform t)
        {
            // All of this code is verbose in order to optimize performance by avoiding using the Bounds APIs
            var gameObjectPath = GetUniqueTransformPath(t).Path;

            // find the full bounds of the statefulGameObject
            var statefulGameObject = t.gameObject;
            var renderers = statefulGameObject.GetComponentsInChildren<Renderer>();

            var minWorldX = float.MaxValue;
            var maxWorldX = float.MinValue;

            var minWorldY = float.MaxValue;
            var maxWorldY = float.MinValue;

            var minWorldZ = float.MaxValue;
            var maxWorldZ = float.MinValue;

            foreach (var nextRenderer in renderers)
            {
                if (nextRenderer.gameObject.GetComponentInParent<RGExcludeFromState>() == null)
                {
                    var theBounds = nextRenderer.bounds;
                    var theMin = theBounds.min;
                    var theMax = theBounds.max;

                    if (theMin.x < minWorldX)
                    {
                        minWorldX = theMin.x;
                    }

                    if (theMax.x > maxWorldX)
                    {
                        maxWorldX = theMax.x;
                    }

                    if (theMin.y < minWorldY)
                    {
                        minWorldY = theMin.y;
                    }

                    if (theMax.y > maxWorldY)
                    {
                        maxWorldY = theMax.y;
                    }

                    if (theMin.z < minWorldZ)
                    {
                        minWorldZ = theMin.z;
                    }

                    if (theMax.z > maxWorldZ)
                    {
                        maxWorldZ = theMax.z;
                    }
                }
            }

            var onCamera = minWorldX < float.MaxValue;
            if (onCamera)
            {
                // convert world space to screen space
                Vector3[] worldCorners =
                {
                    new(minWorldX, minWorldY, minWorldZ),
                    new(maxWorldX, minWorldY, minWorldZ),
                    new(maxWorldX, maxWorldY, minWorldZ),
                    new(minWorldX, maxWorldY, minWorldZ),
                    new(minWorldX, minWorldY, maxWorldZ),
                    new(maxWorldX, minWorldY, maxWorldZ),
                    new(maxWorldX, maxWorldY, maxWorldZ),
                    new(minWorldX, maxWorldY, maxWorldZ),
                };

                var screenSpaceObjectCorners =
                    worldCorners.Select(corner => Camera.main.WorldToViewportPoint(corner));

                var minX = float.MaxValue;
                var maxX = float.MinValue;

                var minY = float.MaxValue;
                var maxY = float.MinValue;

                foreach (var screenSpaceObjectCorner in screenSpaceObjectCorners)
                {
                    var x = screenSpaceObjectCorner.x;
                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    var y = screenSpaceObjectCorner.y;
                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }

                if (includeOnlyOnCameraObjects)
                {
                    var yBuffer = 0;
                    var xBuffer = 0;
                    if (replay)
                    {
                        // look a bit outside the view on replay to account for edge cases and small intentional variances in the game code
                        yBuffer = screenHeight / 2;
                        xBuffer = screenWidth / 2;
                    }

                    var xLowerLimit = 0 - xBuffer;
                    var xUpperLimit = screenWidth + xBuffer;
                    var yLowerLimit = 0 - yBuffer;
                    var yUpperLimit = screenWidth + yBuffer;
                    if (!(minX <= xUpperLimit && maxX >= xLowerLimit && minY <= yUpperLimit && maxY >= yLowerLimit))
                    {
                        // not in camera..
                        onCamera = false;
                    }
                }

                if (onCamera)
                {
                    // make sure the screen space bounds has a non-zero Z size around 0
                    var size = new Vector3(maxX - minX, maxY - minY, 0.1f);
                    var center = new Vector3(minX + size.x / 2, minY + size.y / 2, 0f);
                    var screenSpaceBounds = new Bounds(center, size);

                    var childComponents = statefulGameObject.GetComponentsInChildren<Component>();

                    var behaviours = new List<BehaviourState>();
                    var collidersState = new List<ColliderState>();
                    List<RigidbodyState> rigidbodiesState = new();
                    foreach (var childComponent in childComponents)
                    {
                        if (childComponent is Collider colliderEntry)
                        {
                            collidersState.Add(new ColliderState
                            {
                                path = GetUniqueTransformPath(colliderEntry.transform).Path,
                                bounds = colliderEntry.bounds,
                                isTrigger = colliderEntry.isTrigger
                            });
                        }
                        else if (childComponent is Collider2D colliderEntry2D)
                        {
                            collidersState.Add(new ColliderState
                            {
                                path = GetUniqueTransformPath(colliderEntry2D.transform).Path,
                                bounds = colliderEntry2D.bounds,
                                isTrigger = colliderEntry2D.isTrigger
                            });
                        }
                        else if (childComponent is Rigidbody myRigidbody)
                        {
                            rigidbodiesState.Add(new RigidbodyState
                                {
                                    path = GetUniqueTransformPath(myRigidbody.transform).Path,
                                    position = myRigidbody.position,
                                    rotation = myRigidbody.rotation,
                                    velocity = myRigidbody.velocity,
                                    drag = myRigidbody.drag,
                                    angularDrag = myRigidbody.angularDrag,
                                    useGravity = myRigidbody.useGravity,
                                    isKinematic = myRigidbody.isKinematic
                                }
                            );
                        }
                        else if (childComponent is Rigidbody2D myRigidbody2D)
                        {
                            rigidbodiesState.Add(new RigidbodyState
                                {
                                    path = GetUniqueTransformPath(myRigidbody2D.transform).Path,
                                    position = myRigidbody2D.position,
                                    rotation = Quaternion.Euler(0, 0, myRigidbody2D.rotation),
                                    velocity = myRigidbody2D.velocity
                                }
                            );
                        }
                        else if (childComponent is MonoBehaviour childBehaviour)
                        {
                            behaviours.Add(GetStateForBehaviour(childBehaviour));
                        }
                    }

                    var worldSize = new Vector3(maxWorldX - minWorldX, maxWorldY - minWorldY, maxWorldZ - minWorldZ);
                    var worldCenter = new Vector3(minWorldX + worldSize.x / 2, minWorldY + worldSize.y / 2, minWorldZ + worldSize.z / 2);
                    var worldBounds = new Bounds(worldCenter, worldSize);

                    return new RecordedGameObjectState
                    {
                        id = statefulGameObject.transform.GetInstanceID(),
                        path = gameObjectPath,
                        screenSpaceBounds = screenSpaceBounds,
                        worldSpaceBounds = worldBounds,
                        rendererCount = renderers.Length,
                        position = statefulGameObject.transform.position,
                        rotation = statefulGameObject.transform.rotation,
                        tag = statefulGameObject.tag,
                        layer = LayerMask.LayerToName(statefulGameObject.layer),
                        scene = statefulGameObject.scene.name,
                        behaviours = behaviours,
                        colliders = collidersState,
                        rigidbodies = rigidbodiesState
                    };
                }
            }

            return null;
        }


        public List<RecordedGameObjectState> GetStateForCurrentFrame(bool replay = false)
        {
            var resultList = new List<RecordedGameObjectState>();

            //find any gameObject with a renderer or canvas renderer (rect transform)
            var statefulUIObjects =
                FindObjectsByType<CanvasRenderer>(FindObjectsSortMode.None).Select(r => r.gameObject.GetComponentInParent<RGExcludeFromState>() == null ? r.gameObject : null);

            var screenSpaceCorners = new Vector3[4];
            foreach (var statefulUIObject in statefulUIObjects)
            {
                if (statefulUIObject != null)
                {
                    // screen space
                    var canvasGroup = statefulUIObject.GetComponentInParent<CanvasGroup>();
                    var cgEnabled = true;
                    while (cgEnabled && canvasGroup != null)
                    {
                        cgEnabled &= canvasGroup.enabled &&
                                     (canvasGroup.blocksRaycasts || canvasGroup.interactable || canvasGroup.alpha > 0);
                        if (canvasGroup.ignoreParentGroups)
                        {
                            break;
                        }

                        // see if there are any more above this in the parent
                        var parent = canvasGroup.transform.parent;
                        canvasGroup = parent == null ? null : parent.GetComponentInParent<CanvasGroup>();
                    }

                    if (cgEnabled)
                    {
                        var rectTransforms = statefulUIObject.GetComponentsInChildren<RectTransform>();
                        if (rectTransforms.Length > 0)
                        {
                            //The returned array of 4 vertices is clockwise.
                            //It starts bottom left and rotates to top left, then top right, and finally bottom right.
                            //Note that bottom left, for example, is an (x, y, z) vector with x being left and y being bottom.
                            rectTransforms[0].GetWorldCorners(screenSpaceCorners);

                            var min = screenSpaceCorners[0];
                            var max = screenSpaceCorners[2];

                            for (var i = 1; i < rectTransforms.Length; ++i)
                            {
                                rectTransforms[i].GetWorldCorners(screenSpaceCorners);
                                min = Vector3.Min(min, screenSpaceCorners[0]);
                                max = Vector3.Max(max, screenSpaceCorners[2]);
                            }

                            // make sure the screen space bounds has a non-zero Z size around 0
                            var size = max - min;
                            size.z = 0.1f;
                            var center = min + ((max - min) / 2);
                            center.z = 0f;
                            var screenSpaceBounds = new Bounds(center, size);

                            var gameObjectPath = GetUniqueTransformPath(statefulUIObject.transform).Path;
                            var cbs = statefulUIObject.GetComponentsInChildren<Behaviour>();
                            var behaviours = new List<BehaviourState>();
                            foreach (var cb in cbs)
                            {
                                behaviours.Add(GetStateForBehaviour(cb));
                            }

                            var soTransform = statefulUIObject.transform;
                            resultList.Add(new RecordedGameObjectState
                            {
                                id = soTransform.GetInstanceID(),
                                path = gameObjectPath,
                                rendererCount = rectTransforms.Length,
                                screenSpaceBounds = screenSpaceBounds,
                                position = soTransform.position,
                                rotation = soTransform.rotation,
                                tag = statefulUIObject.tag,
                                layer = LayerMask.LayerToName(statefulUIObject.layer),
                                scene = statefulUIObject.scene.name,
                                behaviours = behaviours,
                                colliders = new List<ColliderState>(),
                                worldSpaceBounds = null,
                                rigidbodies = new List<RigidbodyState>()
                            });
                        }
                    }
                }
            }

            // find everything with a renderer.. then select the last parent walking up the tree that has
            // one of the key types.. in most cases that should be the correct 'parent' game object

            var transformsForThisFrame = new HashSet<Transform>();

            // add all the requisite transforms... avoided using Linq here for performance reasons
            var transforms = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var includeInStateObjects = FindObjectsByType<RGIncludeInState>(FindObjectsSortMode.None);

            if (!collapseRenderersIntoTopLevelGameObject)
            {
                foreach (var transform1 in transforms)
                {
                    transformsForThisFrame.Add(transform1.transform);
                }

                foreach (var includeInStateObject in includeInStateObjects)
                {
                    transformsForThisFrame.Add(includeInStateObject.transform);
                }
            }
            else
            {
                foreach (var transform1 in transforms)
                {
                    ProcessTransform(transform1.transform, transformsForThisFrame);
                }

                foreach (var includeInStateObject in includeInStateObjects)
                {
                    ProcessTransform(includeInStateObject.transform, transformsForThisFrame);
                }

            }

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            foreach (var statefulTransform in transformsForThisFrame)
            {
                if (statefulTransform != null)
                {
                    var stateEntry = CreateStateForTransform(replay, screenWidth, screenHeight, statefulTransform);
                    // depending on the include only on camera setting, this object may be null
                    if (stateEntry != null)
                    {
                        resultList.Add(stateEntry);
                    }
                }
            }

            return resultList;
        }


        private void ProcessTransform(Transform theTransform, HashSet<Transform> transformsForThisFrame)
        {

            // we walk all the way to the root and record which ones had key types to find the 'parent'
            if (_transformsIveSeen.TryGetValue(theTransform, out var tStatus))
            {
                tStatus.HasKeyTypes = true;
            }
            else
            {
                tStatus = new TransformStatus
                {
                    HasKeyTypes = true
                };
                _transformsIveSeen[theTransform] = tStatus;
            }

            var maybeTopLevel = theTransform;


            if (tStatus is { TopLevelForThisTransform: not null})
            {
                maybeTopLevel = tStatus.TopLevelForThisTransform;
            }
            else
            {
                // find any parents we need to evaluate
                var nextParent = theTransform.parent;

                // go until the root of the tree
                while (nextParent != null)
                {
                    bool parentHasKeyTypes;
                    _transformsIveSeen.TryGetValue(nextParent, out var nextParentStatus);

                    if (nextParentStatus is { TopLevelForThisTransform: not null })
                    {
                        maybeTopLevel = nextParentStatus.TopLevelForThisTransform;
                        break;
                    }

                    if (nextParentStatus is { HasKeyTypes: not null })
                    {
                        parentHasKeyTypes = nextParentStatus.HasKeyTypes.Value;
                    }
                    else
                    {
                        // if we already saw that parent before, this frame or otherwise, we won't do this again
                        parentHasKeyTypes = nextParent.GetComponentsInParent<Component>()
                            .FirstOrDefault(pc =>
                                pc is Renderer or Collider or Collider2D or Rigidbody or Rigidbody2D or Animator
                                    or ParticleSystem or RGIncludeInState
                            ) != null;
                    }

                    if (nextParentStatus != null)
                    {
                        nextParentStatus.HasKeyTypes = parentHasKeyTypes;
                    }
                    else
                    {
                        nextParentStatus = new TransformStatus
                        {
                            HasKeyTypes = parentHasKeyTypes
                        };
                        _transformsIveSeen[nextParent] = nextParentStatus;
                    }

                    if (parentHasKeyTypes)
                    {
                        // track the new one
                        maybeTopLevel = nextParent;

                        if (!collapseRenderersIntoTopLevelGameObject)
                        {
                            transformsForThisFrame.Add(nextParent);
                        }
                    }

                    nextParent = nextParent.parent;
                }
            }

            // set the top level parent we found on that path
            if (maybeTopLevel != null)
            {
                tStatus.TopLevelForThisTransform = maybeTopLevel;
                if (collapseRenderersIntoTopLevelGameObject)
                {
                    transformsForThisFrame.Add(maybeTopLevel);
                }
            }
        }
    }
}
