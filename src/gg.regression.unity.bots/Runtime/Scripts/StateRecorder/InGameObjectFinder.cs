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
        private readonly Dictionary<Transform, RecordedGameObjectState> _cachedTransformStates = new();

        public void Awake()
        {
            if (_this != null)
            {
                _this._transformsIveSeen.Clear();
                _this._cachedTransformStates.Clear();
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

        private BehaviourState CreateStateForBehaviour(Behaviour behaviour)
        {
            var state = new BehaviourState();
            UpdateStateForBehaviour(state, behaviour);
            return state;
        }

        private void UpdateStateForBehaviour(BehaviourState state, Behaviour behaviour)
        {
            var tStatus = GetUniqueTransformPath(behaviour.transform, behaviour);
            state.path = tStatus.Path;
            state.name = tStatus.TypeFullName;
            state.state = behaviour;
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
                            var soTransform = statefulUIObject.transform;
                            if (!_cachedTransformStates.TryGetValue(soTransform, out var recordedGameObjectState))
                            {
                                recordedGameObjectState = new RecordedGameObjectState
                                {
                                    id = soTransform.GetInstanceID(),
                                    path = GetUniqueTransformPath(statefulUIObject.transform).Path,
                                    rendererCount = rectTransforms.Length,
                                    screenSpaceBounds = new Bounds(),
                                    position = soTransform.position,
                                    rotation = soTransform.rotation,
                                    tag = statefulUIObject.tag,
                                    layer = LayerMask.LayerToName(statefulUIObject.layer),
                                    scene = statefulUIObject.scene.name,
                                    behaviours = new List<BehaviourState>(),
                                    colliders = new List<ColliderState>(),
                                    worldSpaceBounds = null,
                                    rigidbodies = new List<RigidbodyState>()
                                };
                            }
                            else
                            {
                                recordedGameObjectState.position = soTransform.position;
                                recordedGameObjectState.rotation = soTransform.rotation;
                                recordedGameObjectState.rendererCount = rectTransforms.Length;
                            }
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
                            // this basically treats everything as 1 flat plane for the camera (screen) bounds we capture
                            // this is to make processing mouse x,y input bounds checks non-complicated
                            var size = max - min;
                            size.z = 0.1f;
                            var center = min + ((max - min) / 2);
                            center.z = 0f;
                            recordedGameObjectState.screenSpaceBounds.center = center;
                            recordedGameObjectState.screenSpaceBounds.size = size;

                            var cbs = statefulUIObject.GetComponentsInChildren<Behaviour>();
                            var behaviours = recordedGameObjectState.behaviours;
                            var behavioursIndex = 0;
                            foreach (var cb in cbs)
                            {
                                if (behavioursIndex < behaviours.Count)
                                {
                                    // re-use existing object
                                    var beh = behaviours[behavioursIndex++];
                                    UpdateStateForBehaviour(beh, cb);
                                }
                                else
                                {
                                    behaviours.Add(CreateStateForBehaviour(cb));
                                    ++behavioursIndex;
                                }
                            }


                            resultList.Add(recordedGameObjectState);
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
            var mainCamera = Camera.main;

            foreach (var statefulTransform in transformsForThisFrame)
            {
                if (statefulTransform != null)
                {
                    var stateEntry = CreateStateForTransform(mainCamera, replay, screenWidth, screenHeight, statefulTransform);
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

        private RecordedGameObjectState CreateStateForTransform(Camera mainCamera, bool replay, int screenWidth, int screenHeight, Transform t)
        {
            // All of this code is verbose in order to optimize performance by avoiding using the Bounds APIs

            // find the full bounds of the statefulGameObject
            var statefulGameObject = t.gameObject;
            var renderers = statefulGameObject.GetComponentsInChildren<Renderer>();

            var minWorldX = float.MaxValue;
            var maxWorldX = float.MinValue;

            var minWorldY = float.MaxValue;
            var maxWorldY = float.MinValue;

            var minWorldZ = float.MaxValue;
            var maxWorldZ = float.MinValue;

            var isVisible = false;

            foreach (var nextRenderer in renderers)
            {
                if (nextRenderer.gameObject.GetComponentInParent<RGExcludeFromState>() == null)
                {
                    if (nextRenderer.isVisible)
                    {
                        isVisible = true;

                        var theBounds = nextRenderer.bounds;
                        var center = theBounds.center;
                        var extents = theBounds.extents;

                        var theMinX = center.x - extents.x;
                        var theMaxX = center.x + extents.x;
                        if (theMinX < minWorldX)
                        {
                            minWorldX = theMinX;
                        }

                        if (theMaxX > maxWorldX)
                        {
                            maxWorldX = theMaxX;
                        }

                        var theMinY = center.y - extents.y;
                        var theMaxY = center.y + extents.y;
                        if (theMinY < minWorldY)
                        {
                            minWorldY = theMinY;
                        }

                        if (theMaxY > maxWorldY)
                        {
                            maxWorldY = theMaxY;
                        }

                        var theMinZ = center.z - extents.z;
                        var theMaxZ = center.z + extents.z;
                        if (theMinZ < minWorldZ)
                        {
                            minWorldZ = theMinZ;
                        }

                        if (theMaxZ > maxWorldZ)
                        {
                            maxWorldZ = theMaxZ;
                        }
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
                    worldCorners.Select(corner => mainCamera.WorldToScreenPoint(corner));

                var minX = float.MaxValue;
                var maxX = float.MinValue;

                var minY = float.MaxValue;
                var maxY = float.MinValue;

                var minZ = float.MaxValue;
                var maxZ = float.MinValue;

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

                    var z = screenSpaceObjectCorner.z;
                    if (z < minZ)
                    {
                        minZ = z;
                    }

                    if (z > maxZ)
                    {
                        maxZ = z;
                    }
                }

                if (includeOnlyOnCameraObjects)
                {
                    // use this so that things hidden by UI elements aren't consider (this is especially important for loading scenes / etc)
                    onCamera = isVisible;
                }

                if (onCamera)
                {
                    if (!_cachedTransformStates.TryGetValue(t, out var recordedGameObjectState))
                    {
                        recordedGameObjectState = new RecordedGameObjectState ()
                        {
                            id = statefulGameObject.transform.GetInstanceID(),
                            path = GetUniqueTransformPath(t).Path,
                            screenSpaceBounds = new Bounds(),
                            screenSpaceZOffset = maxZ,
                            worldSpaceBounds = new Bounds(),
                            rendererCount = renderers.Length,
                            position = t.position,
                            rotation = t.rotation,
                            tag = statefulGameObject.tag,
                            layer = LayerMask.LayerToName(statefulGameObject.layer),
                            scene = statefulGameObject.scene.name,
                            behaviours = new List<BehaviourState>(),
                            colliders = new List<ColliderState>(),
                            rigidbodies = new List<RigidbodyState>()
                        };
                        _cachedTransformStates[t] = recordedGameObjectState;
                    }
                    else
                    {
                        recordedGameObjectState.position = t.position;
                        recordedGameObjectState.rotation = t.rotation;
                        recordedGameObjectState.rendererCount = renderers.Length;
                        recordedGameObjectState.screenSpaceZOffset = maxZ;
                    }

                    var extentX = (maxX - minX) / 2;
                    var extentY = (maxY - minY) / 2;
                    recordedGameObjectState.screenSpaceBounds.center = new Vector3(minX + extentX, minY + extentY, 0);
                    recordedGameObjectState.screenSpaceBounds.extents = new Vector3(extentX, extentY, 0.1f);

                    extentX = (maxWorldX - minWorldX) / 2;
                    extentY = (maxWorldY - minWorldY) / 2;
                    var extentZ = (maxWorldZ - minWorldZ) / 2;
                    Bounds theBounds = recordedGameObjectState.worldSpaceBounds.Value;
                    theBounds.center = new Vector3(minWorldX + extentX, minWorldY + extentY, minWorldZ + extentZ);
                    theBounds.extents = new Vector3(extentX, extentY, extentZ);

                    var childComponents = statefulGameObject.GetComponentsInChildren<Component>();

                    var behaviours = recordedGameObjectState.behaviours;
                    var behavioursIndex = 0;
                    var collidersState = recordedGameObjectState.colliders;
                    var colliderIndex = 0;
                    List<RigidbodyState> rigidbodiesState = recordedGameObjectState.rigidbodies;
                    var rigidbodiesIndex = 0;

                    foreach (var childComponent in childComponents)
                    {
                        if (childComponent is Collider colliderEntry)
                        {
                            if (colliderIndex < collidersState.Count)
                            {
                                // re-use existing object
                                var col = collidersState[colliderIndex++];
                                col.path = GetUniqueTransformPath(colliderEntry.transform).Path;
                                col.bounds = colliderEntry.bounds;
                                col.isTrigger = colliderEntry.isTrigger;
                            }
                            else
                            {
                                collidersState.Add(new ColliderState
                                {
                                    path = GetUniqueTransformPath(colliderEntry.transform).Path,
                                    bounds = colliderEntry.bounds,
                                    isTrigger = colliderEntry.isTrigger
                                });
                                ++colliderIndex;
                            }
                        }
                        else if (childComponent is Collider2D colliderEntry2D)
                        {
                            if (colliderIndex < collidersState.Count)
                            {
                                // re-use existing object
                                var col = collidersState[colliderIndex++];
                                col.path = GetUniqueTransformPath(colliderEntry2D.transform).Path;
                                col.bounds = colliderEntry2D.bounds;
                                col.isTrigger = colliderEntry2D.isTrigger;
                            }
                            else
                            {
                                collidersState.Add(new ColliderState
                                {
                                    path = GetUniqueTransformPath(colliderEntry2D.transform).Path,
                                    bounds = colliderEntry2D.bounds,
                                    isTrigger = colliderEntry2D.isTrigger
                                });
                                ++colliderIndex;
                            }
                        }
                        else if (childComponent is Rigidbody myRigidbody)
                        {
                            if (rigidbodiesIndex < rigidbodiesState.Count)
                            {
                                // re-use existing object
                                var rb = rigidbodiesState[rigidbodiesIndex++];
                                rb.path = GetUniqueTransformPath(myRigidbody.transform).Path;
                                rb.position = myRigidbody.position;
                                rb.rotation = myRigidbody.rotation;
                                rb.velocity = myRigidbody.velocity;
                                rb.drag = myRigidbody.drag;
                                rb.angularDrag = myRigidbody.angularDrag;
                                rb.useGravity = myRigidbody.useGravity;
                                rb.isKinematic = myRigidbody.isKinematic;
                            }
                            else
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
                                ++rigidbodiesIndex;
                            }

                        }
                        else if (childComponent is Rigidbody2D myRigidbody2D)
                        {
                            if (rigidbodiesIndex < rigidbodiesState.Count)
                            {
                                // re-use existing object
                                var rb = rigidbodiesState[rigidbodiesIndex++];
                                rb.path = GetUniqueTransformPath(myRigidbody2D.transform).Path;
                                rb.position = myRigidbody2D.position;
                                rb.rotation = Quaternion.Euler(0, 0, myRigidbody2D.rotation);
                                rb.velocity = myRigidbody2D.velocity;
                            }
                            else
                            {
                                rigidbodiesState.Add(new RigidbodyState
                                    {
                                        path = GetUniqueTransformPath(myRigidbody2D.transform).Path,
                                        position = myRigidbody2D.position,
                                        rotation = Quaternion.Euler(0, 0, myRigidbody2D.rotation),
                                        velocity = myRigidbody2D.velocity
                                    }
                                );
                                ++rigidbodiesIndex;
                            }
                        }
                        else if (childComponent is MonoBehaviour childBehaviour)
                        {
                            if (behavioursIndex < behaviours.Count)
                            {
                                // re-use existing object
                                var beh = behaviours[behavioursIndex++];
                                UpdateStateForBehaviour(beh, childBehaviour);
                            }
                            else
                            {
                                behaviours.Add(CreateStateForBehaviour(childBehaviour));
                                ++behavioursIndex;
                            }
                        }
                    }

                    return recordedGameObjectState;
                }
            }

            return null;
        }
    }
}
