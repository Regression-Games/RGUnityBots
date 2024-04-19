using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using StateRecorder.Types;
using UnityEngine;
using Component = UnityEngine.Component;
// ReSharper disable ForCanBeConvertedToForeach - indexed for is faster and has less allocs than enumerator
// ReSharper disable LoopCanBeConvertedToQuery

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
        private static readonly List<ColliderRecordState> _emptyColliderStateList = new(0);
        private static readonly List<RigidbodyRecordState> _emptyRigidbodyStateList = new(0);

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

        // allocate this rather large list 1 time to avoid realloc on each tick object
        private readonly List<Renderer> _rendererQueryList = new(100);

        private RecordedGameObjectState CreateStateForTransform(List<RecordedGameObjectState> priorState, Camera mainCamera, bool replay, int screenWidth, int screenHeight, Transform t)
        {
            // All of this code is verbose in order to optimize performance by avoiding using the Bounds APIs
            // find the full bounds of the statefulGameObject
            var statefulGameObject = t.gameObject;

            _rendererQueryList.Clear();
            statefulGameObject.GetComponentsInChildren(_rendererQueryList);

            var minWorldX = float.MaxValue;
            var maxWorldX = float.MinValue;

            var minWorldY = float.MaxValue;
            var maxWorldY = float.MinValue;

            var minWorldZ = float.MaxValue;
            var maxWorldZ = float.MinValue;

            var hasVisibleRenderer = false;

            var psCount = priorState?.Count ?? -1;

            var rendererListLength = _rendererQueryList.Count;
            // ReSharper disable once ForCanBeConvertedToForeach - faster by index; avoids enumerator
            for (var i = 0; i < rendererListLength; i++)
            {
                var nextRenderer = _rendererQueryList[i];
                hasVisibleRenderer |= nextRenderer.isVisible;
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

            var onCamera = minWorldX < float.MaxValue && (replay || hasVisibleRenderer);
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

                var minX = float.MaxValue;
                var maxX = float.MinValue;

                var minY = float.MaxValue;
                var maxY = float.MinValue;

                var minZ = float.MaxValue;
                var maxZ = float.MinValue;

                // ReSharper disable once ForCanBeConvertedToForeach - faster by index, avoids enumerator
                for (var i=0; i< worldCorners.Length; i++)
                {
                    var screenSpaceObjectCorner = mainCamera.WorldToScreenPoint(worldCorners[i]);
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
                    var objectTransformId = t.GetInstanceID();

                    RecordedGameObjectState resultObject = null;
                    var usingOldObject = false;
                    if (priorState != null)
                    {
                        for (var i = 0; i < psCount; i++)
                        {
                            var priorObject = priorState[i];
                            if (priorObject.id == objectTransformId)
                            {
                                resultObject = priorObject;
                                usingOldObject = true;
                                break;
                            }
                        }
                    }
                    if (resultObject == null)
                    {
                        resultObject = new RecordedGameObjectState()
                        {
                            id = objectTransformId,
                            transform = t,
                            path = GetUniqueTransformPath(t).Path,
                            tag = statefulGameObject.tag,
                            layer = LayerMask.LayerToName(statefulGameObject.layer),
                            scene = statefulGameObject.scene.name,
                            behaviours = new List<BehaviourState>(),
                            colliders = new List<ColliderRecordState>(),
                            worldSpaceBounds = null,
                            rigidbodies = new List<RigidbodyRecordState>()
                        };
                    }

                    // make sure the screen space bounds has a non-zero Z size around 0
                    // we track the true z offset separately for ease of mouse selection on replay
                    var size = new Vector3(maxX - minX, maxY - minY, 0.1f);
                    var center = new Vector3(minX + size.x / 2, minY + size.y / 2, 0);

                    var worldSize = new Vector3(maxWorldX - minWorldX, maxWorldY - minWorldY, maxWorldZ - minWorldZ);
                    var worldCenter = new Vector3(minWorldX + worldSize.x / 2, minWorldY + worldSize.y / 2, minWorldZ + worldSize.z / 2);

                    // update some fields
                    resultObject.rendererCount = rendererListLength;

                    if (usingOldObject)
                    {
                        resultObject.screenSpaceBounds.size = size;
                        resultObject.screenSpaceBounds.center = center;

                        var wsb = resultObject.worldSpaceBounds.Value;
                        wsb.size = worldSize;
                        wsb.center = worldCenter;
                    }
                    else
                    {
                        resultObject.screenSpaceBounds = new Bounds(center, size);
                        resultObject.worldSpaceBounds = new Bounds(worldCenter, worldSize);
                    }
                    resultObject.screenSpaceZOffset = maxZ;


                    var newBehaviours = new List<BehaviourState>();
                    var newColliders = new List<ColliderRecordState>();
                    var newRigidbodies = new List<RigidbodyRecordState>();
                    var behaviours = resultObject.behaviours;
                    var collidersState = resultObject.colliders;
                    var rigidbodiesState = resultObject.rigidbodies;

                    // instead of searching for child components, we instead walk child tree of transforms and check each for components
                    // this allows us to avoid calling GetUniqueTransformPath more than once for any single transform
                    var nextParentTransforms = new List<Transform> {t};
                    var currentParentTransforms = new List<Transform>();
                    while (nextParentTransforms.Count > 0)
                    {
                        // swap the list references
                        (currentParentTransforms, nextParentTransforms) = (nextParentTransforms, currentParentTransforms);
                        nextParentTransforms.Clear();
                        for (var i = 0; i < currentParentTransforms.Count; i++)
                        {
                            var currentParentTransform = currentParentTransforms[i];
                            var childCount = currentParentTransforms[i].childCount;
                            for (var j = 0; j < childCount; j++)
                            {
                                var currentChildTransform = currentParentTransform.GetChild(j);
                                ProcessChildTransformComponents(currentChildTransform, behaviours, collidersState, rigidbodiesState, newBehaviours, newColliders, newRigidbodies);
                                nextParentTransforms.Add(currentChildTransform);
                            }
                        }
                    }

                    resultObject.behaviours = newBehaviours;
                    resultObject.colliders = newColliders;
                    resultObject.rigidbodies = newRigidbodies;

                    return resultObject;
                }
            }

            return null;
        }

        // pre-allocate this rather large list 1 time to avoid memory stuff on each stick
        private readonly List<Component> _childComponentsQueryList = new(100);

        private void ProcessChildTransformComponents(Transform childTransform, List<BehaviourState> priorBehaviours, List<ColliderRecordState> priorCollidersState, List<RigidbodyRecordState> priorRigidbodiesState, List<BehaviourState> behaviours, List<ColliderRecordState> collidersState, List<RigidbodyRecordState> rigidbodiesState)
        {
            _childComponentsQueryList.Clear();
            childTransform.GetComponents(_childComponentsQueryList);
            TransformStatus ts = null;

            var priorBehavioursCount = priorBehaviours?.Count ?? -1;
            var priorCollidersCount = priorCollidersState?.Count ?? -1;
            var priorRigidbodiesCount = priorRigidbodiesState?.Count?? -1;

            // This code re-uses the objects from the prior state as much as possible to avoid allocations
            // we try to minimize calls to GetUniqueTransformPath whenever possible
            var listLength = _childComponentsQueryList.Count;
            for (var i = 0; i < listLength; i++)
            {
                var childComponent = _childComponentsQueryList[i];
                if (childComponent is Collider colliderEntry)
                {
                    ColliderRecordState cObject = null;
                    var instanceId = colliderEntry.GetInstanceID();
                    if (priorCollidersState != null)
                    {
                        for (var j = 0; j < priorCollidersCount; j++)
                        {
                            var priorCollider = priorCollidersState[j];
                            if (priorCollider.id == instanceId)
                            {
                                cObject = priorCollider;
                                break;
                            }
                        }
                    }

                    cObject ??= new ColliderRecordState
                    {
                        id = instanceId,
                        path = (ts ??= GetUniqueTransformPath(childTransform)).Path,
                        collider = colliderEntry
                    };

                    collidersState.Add(cObject);
                }
                else if (childComponent is Collider2D colliderEntry2D)
                {
                    ColliderRecordState cObject = null;
                    var instanceId = colliderEntry2D.GetInstanceID();
                    if (priorCollidersState != null)
                    {
                        for (var j = 0; j < priorCollidersCount; j++)
                        {
                            var priorCollider = priorCollidersState[j];
                            if (priorCollider.id == instanceId)
                            {
                                cObject = priorCollider;
                                break;
                            }
                        }
                    }

                    cObject ??= new Collider2DRecordState
                    {
                        id = instanceId,
                        path = (ts ??= GetUniqueTransformPath(childTransform)).Path,
                        collider = colliderEntry2D
                    };

                    collidersState.Add(cObject);
                }
                else if (childComponent is Rigidbody myRigidbody)
                {
                    RigidbodyRecordState cObject = null;
                    var instanceId = myRigidbody.GetInstanceID();
                    if (priorRigidbodiesState != null)
                    {
                        for (var j = 0; j < priorRigidbodiesCount; j++)
                        {
                            var priorRigidbody = priorRigidbodiesState[j];
                            if (priorRigidbody.id == instanceId)
                            {
                                cObject = priorRigidbody;
                                break;
                            }
                        }
                    }

                    cObject ??= new RigidbodyRecordState
                    {
                        id = instanceId,
                        path = (ts ??= GetUniqueTransformPath(childTransform)).Path,
                        rigidbody = myRigidbody
                    };

                    rigidbodiesState.Add(cObject);
                }
                else if (childComponent is Rigidbody2D myRigidbody2D)
                {
                    RigidbodyRecordState cObject = null;
                    var instanceId = myRigidbody2D.GetInstanceID();
                    if (priorRigidbodiesState != null)
                    {
                        for (var j = 0; j < priorRigidbodiesCount; j++)
                        {
                            var priorRigidbody = priorRigidbodiesState[j];
                            if (priorRigidbody.id == instanceId)
                            {
                                cObject = priorRigidbody;
                                break;
                            }
                        }
                    }

                    cObject ??= new Rigidbody2DRecordState
                    {
                        id = instanceId,
                        path = (ts ??= GetUniqueTransformPath(childTransform)).Path,
                        rigidbody = myRigidbody2D
                    };

                    rigidbodiesState.Add(cObject);
                }
                else if (childComponent is MonoBehaviour childBehaviour)
                {
                    BehaviourState cObject = null;
                    var instanceId = childBehaviour.GetInstanceID();
                    if (priorBehaviours != null)
                    {
                        for (var j = 0; j < priorBehavioursCount; j++)
                        {
                            var priorBehaviour = priorBehaviours[j];
                            if (priorBehaviour.id == instanceId)
                            {
                                cObject = priorBehaviour;
                                break;
                            }
                        }
                    }

                    cObject ??= new BehaviourState
                    {
                        id = instanceId,
                        path = (ts ??= GetUniqueTransformPath(childTransform)).Path,
                        name = (ts ??= GetUniqueTransformPath(childTransform)).TypeFullName,
                        state = childBehaviour
                    };

                    behaviours.Add(cObject);
                }
            }
        }

        // allocate this rather large list 1 time to save allocations on each tick object
        private readonly List<RectTransform> _rectTransformsList = new(100);

        public List<RecordedGameObjectState> GetStateForCurrentFrame(List<RecordedGameObjectState> priorState = null, bool replay = false)
        {
            //find any gameObject with a canvas renderer (rect transform)
            var canvasRenderers = FindObjectsByType<CanvasRenderer>(FindObjectsSortMode.None);

            // try to avoid dynamic list resizing re-allocations
            var resultList = new List<RecordedGameObjectState>(canvasRenderers.Length);

            var psCount = priorState?.Count ?? -1;

            var screenSpaceCorners = new Vector3[4]; // we re-use this over and over instead of allocating multiple times
            // ReSharper disable once ForCanBeConvertedToForeach - index is faster with less allocs than enumerator
            for (var j = 0; j < canvasRenderers.Length; j++)
            {
                var canvasRenderer = canvasRenderers[j];
                var statefulUIObject = canvasRenderer.gameObject;
                if (statefulUIObject != null && statefulUIObject.GetComponentInParent<RGExcludeFromState>() == null)
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
                        _rectTransformsList.Clear();
                        statefulUIObject.GetComponentsInChildren(_rectTransformsList);
                        var rectTransformsListLength = _rectTransformsList.Count;
                        if (rectTransformsListLength > 0)
                        {
                            //The returned array of 4 vertices is clockwise.
                            //It starts bottom left and rotates to top left, then top right, and finally bottom right.
                            //Note that bottom left, for example, is an (x, y, z) vector with x being left and y being bottom.
                            _rectTransformsList[0].GetWorldCorners(screenSpaceCorners);

                            var min = screenSpaceCorners[0];
                            var max = screenSpaceCorners[2];

                            for (var i = 1; i < rectTransformsListLength; ++i)
                            {
                                _rectTransformsList[i].GetWorldCorners(screenSpaceCorners);
                                // Vector3.min and Vector3.max re-allocate new vectors on each call, avoid using them
                                min.x = Mathf.Min(min.x, screenSpaceCorners[0].x);
                                min.y = Mathf.Min(min.y, screenSpaceCorners[0].y);
                                min.z = Mathf.Min(min.z, screenSpaceCorners[0].z);

                                max.x = Mathf.Min(max.x, screenSpaceCorners[2].x);
                                max.y = Mathf.Min(max.y, screenSpaceCorners[2].y);
                                max.z = Mathf.Min(max.z, screenSpaceCorners[2].z);
                            }

                            // see if we had this object last state.. if so, we can use a lot of the saved information and not re-allocate/re-compute it
                            var objectTransform = statefulUIObject.transform;
                            var objectTransformId = objectTransform.GetInstanceID();

                            RecordedGameObjectState resultObject = null;
                            var usingOldObject = false;
                            if (priorState != null)
                            {
                                for (var i = 0; i < psCount; i++)
                                {
                                    var priorObject = priorState[i];
                                    if (priorObject.id == objectTransformId)
                                    {
                                        resultObject = priorObject;
                                        usingOldObject = true;
                                        break;
                                    }
                                }
                            }

                            if (resultObject == null)
                            {
                                resultObject = new RecordedGameObjectState()
                                {
                                    id = objectTransformId,
                                    transform = objectTransform,
                                    path = GetUniqueTransformPath(objectTransform).Path,
                                    tag = statefulUIObject.tag,
                                    layer = LayerMask.LayerToName(statefulUIObject.layer),
                                    scene = statefulUIObject.scene.name,
                                    behaviours = new List<BehaviourState>(),
                                    colliders = _emptyColliderStateList,
                                    worldSpaceBounds = null,
                                    rigidbodies = _emptyRigidbodyStateList,
                                    screenSpaceZOffset = 0.0f
                                };
                            }

                            resultList.Add(resultObject);

                            // make sure the screen space bounds has a non-zero Z size around 0
                            var size = new Vector3(max.x - min.x, max.y-min.y, 0.1f);
                            var center = new Vector3(min.x + (size.x / 2), min.y + (size.y / 2), 0f);

                            List<BehaviourState> priorBehaviours = resultObject.behaviours;
                            List<BehaviourState> newBehaviours = new();

                            if (usingOldObject)
                            {
                                resultObject.screenSpaceBounds.size = size;
                                resultObject.screenSpaceBounds.center = center;
                            }
                            else
                            {
                                resultObject.screenSpaceBounds = new Bounds(center, size);
                            }

                            // need to update some fields
                            resultObject.rendererCount = rectTransformsListLength;

                            // instead of searching for child Behaviours, we instead walk child tree of transforms and check each for Behaviours
                            // this allows us to avoid calling GetUniqueTransformPath more than once for any single transform
                            var nextParentTransforms = new List<Transform> {objectTransform};
                            var currentParentTransforms = new List<Transform>();
                            while (nextParentTransforms.Count > 0)
                            {
                                // swap the list references
                                (currentParentTransforms, nextParentTransforms) = (nextParentTransforms, currentParentTransforms);
                                nextParentTransforms.Clear();
                                for (var i = 0; i < currentParentTransforms.Count; i++)
                                {
                                    var currentParentTransform = currentParentTransforms[i];
                                    var childCount = currentParentTransforms[i].childCount;
                                    for (var k = 0; k < childCount; k++)
                                    {
                                        var currentChildTransform = currentParentTransform.GetChild(k);
                                        ProcessChildTransformComponents(currentChildTransform, priorBehaviours, null, null, newBehaviours, null, null);
                                        nextParentTransforms.Add(currentChildTransform);
                                    }
                                }
                            }

                            resultObject.behaviours = newBehaviours;
                        }
                    }
                }
            }

            // find everything with a renderer.. then select the last parent walking up the tree that has
            // one of the key types.. in most cases that should be the correct 'parent' game object

            // add all the requisite transforms... avoided using Linq here for performance reasons
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var includeInStateObjects = FindObjectsByType<RGIncludeInState>(FindObjectsSortMode.None);

            var transformsForThisFrame = new HashSet<Transform>(renderers.Length); // start with a size that should fit everything to avoid numerous re-alloc

            if (!collapseRenderersIntoTopLevelGameObject)
            {
                for (var index = 0; index < renderers.Length; index++)
                {
                    var renderer1 = renderers[index];
                    transformsForThisFrame.Add(renderer1.transform);
                }

                for (var i = 0; i < includeInStateObjects.Length; i++)
                {
                    var includeInStateObject = includeInStateObjects[i];
                    transformsForThisFrame.Add(includeInStateObject.transform);
                }
            }
            else
            {
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer1 = renderers[i];
                    ProcessTransform(renderer1.transform, transformsForThisFrame);
                }

                for (var i = 0; i < includeInStateObjects.Length; i++)
                {
                    var includeInStateObject = includeInStateObjects[i];
                    ProcessTransform(includeInStateObject.transform, transformsForThisFrame);
                }
            }

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            var mainCamera = Camera.main;
            foreach (var statefulTransform in transformsForThisFrame) // can't use for with index as this is a hashset and enumerator is better in this case
            {
                if (statefulTransform != null)
                {
                    var stateEntry = CreateStateForTransform(priorState, mainCamera, replay, screenWidth, screenHeight, statefulTransform);
                    // depending on the include only on camera setting, this object may be null
                    if (stateEntry != null)
                    {
                        resultList.Add(stateEntry);
                    }
                }
            }

            return resultList;
        }

        // allocate this rather large list 1 time to avoid realloc on each tick object
        private readonly List<Component> _componentsInParentList = new(100);


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
                    var parentHasKeyTypes = false;
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
                        _componentsInParentList.Clear();
                        // if we already saw that parent before, this frame or otherwise, we won't do this again
                        nextParent.GetComponentsInParent(false, _componentsInParentList);
                        // ReSharper disable once ForCanBeConvertedToForeach - index iteration faster with less enumerator allocs
                        // ReSharper disable once LoopCanBeConvertedToQuery - index iteration faster with less enumerator allocs
                        var componentsInParentListLength = _componentsInParentList.Count;
                        for (var i = 0; i < componentsInParentListLength; i++)
                        {
                            if (_componentsInParentList[i] is Renderer or Collider or Collider2D or Rigidbody or Rigidbody2D or Animator
                                or ParticleSystem or RGIncludeInState)
                            {
                                parentHasKeyTypes = true;
                                break;
                            }
                        }
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
