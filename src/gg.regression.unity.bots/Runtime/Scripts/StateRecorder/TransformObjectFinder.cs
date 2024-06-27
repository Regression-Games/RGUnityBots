using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using RegressionGames.StateRecorder.Types;
using UnityEngine;
using Component = UnityEngine.Component;
// ReSharper disable ForCanBeConvertedToForeach - indexed for is faster and has less allocs than enumerator
// ReSharper disable LoopCanBeConvertedToQuery
namespace RegressionGames.StateRecorder
{
    public class TransformObjectFinder : ObjectFinder
    {
        // this is only a safe pooling optimization because we don't compare colliders/behaviours/rigidbodies between prior frame and current frame state.  If we do, this optimization will become unsafe
        private static readonly List<BehaviourState> BehaviourStateObjectPool = new (100);
        private static readonly List<ColliderRecordState> ColliderStateObjectPool = new (100);
        private static readonly List<Collider2DRecordState> Collider2DStateObjectPool = new (100);
        private static readonly List<RigidbodyRecordState> RigidbodyStateObjectPool = new (100);
        private static readonly List<Rigidbody2DRecordState> Rigidbody2DStateObjectPool = new (100);

        public void Awake()
        {
            TransformStatus.Reset();

            // Is there a better place to do this, maybe.. but for now, this gets the ECS subsystem loaded on the same object as this behaviour
            Type t = Type.GetType("RegressionGames.StateRecorder.ECS.EntityFinder");
            var entityFinder = this.gameObject.GetComponent(t);
            if (entityFinder == null)
            {
                this.gameObject.AddComponent(t);
            }

        }

        // allocate this rather large list 1 time to avoid realloc on each tick object
        private readonly List<Renderer> _rendererQueryList = new(1000);

        // avoid re-allocating all these vector3 objects for each element in each tick
        private readonly Vector3[] _worldCorners =
        {
            new(0, 0, 0),
            new(0, 0, 0),
            new(0, 0, 0),
            new(0, 0, 0),
            new(0, 0, 0),
            new(0, 0, 0),
            new(0, 0, 0),
            new(0, 0, 0),
        };

        // pre-allocate this rather large list 1 time to avoid memory stuff on each tick
        private readonly List<Component> _componentsQueryList = new(100);

        private void ProcessTransformComponents(TransformStatus transformStatus, List<IComponentDataProvider> dataProviders)
        {
            _componentsQueryList.Clear();
            transformStatus.Transform.GetComponents(_componentsQueryList);

            // uses object pools to minimize new allocations and GCs

            // This code re-uses the objects from the prior state as much as possible to avoid allocations
            // we try to minimize calls to GetUniqueTransformPath whenever possible
            var listLength = _componentsQueryList.Count;
            for (var i = 0; i < listLength; i++)
            {
                var component = _componentsQueryList[i];
                if (component is Collider colliderEntry)
                {
                    ColliderRecordState cObject;
                    var poolCount = ColliderStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = ColliderStateObjectPool[poolCount - 1];
                        ColliderStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new ColliderRecordState();
                    }

                    cObject.collider = colliderEntry;

                    dataProviders.Add(cObject);
                }
                else if (component is Collider2D colliderEntry2D)
                {
                    Collider2DRecordState cObject;
                    var poolCount = Collider2DStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = Collider2DStateObjectPool[poolCount - 1];
                        Collider2DStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new Collider2DRecordState();
                    }

                    cObject.collider = colliderEntry2D;

                    dataProviders.Add(cObject);
                }
                else if (component is Rigidbody myRigidbody)
                {
                    RigidbodyRecordState cObject;
                    var poolCount = RigidbodyStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = RigidbodyStateObjectPool[poolCount - 1];
                        RigidbodyStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new RigidbodyRecordState();
                    }

                    cObject.rigidbody = myRigidbody;

                    dataProviders.Add(cObject);
                }
                else if (component is Rigidbody2D myRigidbody2D)
                {
                    Rigidbody2DRecordState cObject;
                    var poolCount = Rigidbody2DStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = Rigidbody2DStateObjectPool[poolCount - 1];
                        Rigidbody2DStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new Rigidbody2DRecordState();
                    }

                    cObject.rigidbody = myRigidbody2D;

                    dataProviders.Add(cObject);
                }
                else if (component is MonoBehaviour childBehaviour)
                {
                    BehaviourState cObject;
                    var poolCount = BehaviourStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = BehaviourStateObjectPool[poolCount - 1];
                        BehaviourStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new BehaviourState();
                    }

                    cObject.name = childBehaviour.GetType().FullName;
                    cObject.state = childBehaviour;

                    dataProviders.Add(cObject);
                }
            }
        }

        // allocate these rather large things 1 time to save allocations on each tick object
        private readonly List<RectTransform> _rectTransformsList = new(100);
        private readonly HashSet<Transform> _transformsForThisFrame = new (1000);

        private readonly Vector3[] _worldSpaceCorners = new Vector3[4];

        private int _objectFrameNumber = -1;
        private int _stateFrameNumber = -1;

        // pre-allocate a big list we can re-use
        private Dictionary<long,RecordedGameObjectState> _priorStates = new (1000);
        private Dictionary<long,RecordedGameObjectState> _newStates = new(1000);
        private readonly List<RecordedGameObjectState> _fillInStates = new (1000);

        private Dictionary<long,ObjectStatus> _priorObjects = new(1000);
        private Dictionary<long,ObjectStatus> _newObjects = new(1000);


        public override void Cleanup()
        {
            _priorStates.Clear();
            _newStates.Clear();
            _fillInStates.Clear();

            _priorObjects.Clear();
            _newObjects.Clear();
            _objectFrameNumber = -1;
            _stateFrameNumber = -1;
        }

        public override Dictionary<long, PathBasedDeltaCount> ComputeNormalizedPathBasedDeltaCounts(Dictionary<long, ObjectStatus> priorTransformStatusList, Dictionary<long, ObjectStatus> currentTransformStatusList, out bool hasDelta)
        {
            hasDelta = false;
            var result = new Dictionary<long, PathBasedDeltaCount>(); // keyed by path hash
            /*
             * go through the new state and add up the totals
             * - track the ids for each path
             * - compute spawns vs old state
             *
             * go through the old state
             *  - track paths that have had de-spawns
             */
            foreach (var currentEntry in currentTransformStatusList.Values)
            {
                var pathHash = currentEntry.NormalizedPath.GetHashCode();
                if (!result.TryGetValue(pathHash, out var pathCountEntry))
                {
                    pathCountEntry = new PathBasedDeltaCount(pathHash, currentEntry.NormalizedPath);
                    result[pathHash] = pathCountEntry;
                }

                var onCameraNow = (currentEntry.screenSpaceBounds != null);

                // only update 'count' for things on screen, but added/removed count are updated always
                if (onCameraNow)
                {
                    pathCountEntry.count++;
                }

                // ids is used to track despawns
                pathCountEntry.ids.Add(currentEntry.Id);

                if (!priorTransformStatusList.TryGetValue(currentEntry.Id, out var oldStatus))
                {
                    hasDelta = true;
                    pathCountEntry.addedCount++;
                }
                else
                {
                    var onCameraBefore = (oldStatus.screenSpaceBounds != null);
                    if ( onCameraBefore != onCameraNow )
                    {
                        if (onCameraNow)
                        {
                            pathCountEntry.higherLowerCountTracker++;
                        }
                        else
                        {
                            pathCountEntry.higherLowerCountTracker--;
                        }
                        // camera visibility changed.. only need to do this in one place, because if both lists didn't have it we can't compare
                        hasDelta = true;
                    }
                }
            }

            // figure out de-spawns
            foreach( KeyValuePair<long, ObjectStatus> entry in priorTransformStatusList)
            {
                var pathHash = entry.Value.NormalizedPath.GetHashCode();
                if (!result.TryGetValue(pathHash, out var pathCountEntry))
                {
                    // this object wasn't in our result.. add an entry to so we can track the despawn
                    pathCountEntry = new PathBasedDeltaCount(pathHash, entry.Value.NormalizedPath);
                    result[pathHash] = pathCountEntry;
                }

                if (!pathCountEntry.ids.Contains(entry.Key))
                {
                    hasDelta = true;
                    pathCountEntry.removedCount++;
                }
            }

            return result;
        }

        public override (Dictionary<long, ObjectStatus>, Dictionary<long, ObjectStatus>) GetObjectStatusForCurrentFrame()
        {
            var frameCount = Time.frameCount;
            if (frameCount == _stateFrameNumber)
            {
                // we already processed this frame (happens when recording during replay and they both call this)
                return (_priorObjects, _newObjects);
            }

            _objectFrameNumber = frameCount;

            // switch the list references
            (_priorObjects, _newObjects) = (_newObjects, _priorObjects);
            _newObjects.Clear();

            // populate the data
            PopulateUITransformsForCurrentFrame();
            PopulateGameObjectTransformsForCurrentFrame();

            return (_priorObjects, _newObjects);
        }

        private void PopulateUITransformsForCurrentFrame()
        {

            var canvasRenderers = FindObjectsByType(typeof(CanvasRenderer), FindObjectsSortMode.None);

            var screenWidth = UnityEngine.Device.Screen.width;
            var screenHeight = UnityEngine.Device.Screen.height;

            var mainCamera = Camera.main;

            // we re-use this over and over instead of allocating multiple times
            var canvasRenderersLength = canvasRenderers.Length;
            for (var j = 0; j < canvasRenderersLength; j++)
            {
                var canvasRenderer = canvasRenderers[j];
                var statefulUIObject = ((CanvasRenderer)canvasRenderer).gameObject;
                if (statefulUIObject != null && statefulUIObject.GetComponentInParent<RGExcludeFromState>() == null)
                {
                    var canvas = statefulUIObject.GetComponentInParent<Canvas>();
                    if (canvas == null)
                    {
                        // did Not think having canvas as a child instead of a parent was allowed.. but one of our partner's games gets away with it :/
                        canvas = statefulUIObject.GetComponentInChildren<Canvas>();
                    }

                    if (canvas != null && canvas.enabled)
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
                            var canvasCamera = canvas.worldCamera == null ? mainCamera : canvas.worldCamera;
                            var isWorldSpace = canvas.renderMode == RenderMode.WorldSpace;
                            _rectTransformsList.Clear();
                            statefulUIObject.GetComponentsInChildren(_rectTransformsList);
                            var rectTransformsListLength = _rectTransformsList.Count;

                            if (rectTransformsListLength > 0)
                            {
                                var objectTransform = statefulUIObject.transform;
                                var tStatus = TransformStatus.GetOrCreateTransformStatus(objectTransform);
                                _newObjects[tStatus.Id] = tStatus;

                                Vector2 min, max;
                                var worldMin = Vector3.zero;
                                var worldMax = Vector3.zero;
                                _rectTransformsList[0].GetWorldCorners(_worldSpaceCorners);
                                if (isWorldSpace)
                                {
                                    min = mainCamera.WorldToScreenPoint(_worldSpaceCorners[0]);
                                    max = mainCamera.WorldToScreenPoint(_worldSpaceCorners[2]);
                                    worldMin = _worldSpaceCorners[0];
                                    worldMax = _worldSpaceCorners[2];
                                }
                                else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                                {
                                    min = canvasCamera.WorldToScreenPoint(_worldSpaceCorners[0]);
                                    max = canvasCamera.WorldToScreenPoint(_worldSpaceCorners[2]);
                                }
                                else // if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                                {
                                    min = _worldSpaceCorners[0];
                                    max = _worldSpaceCorners[2];
                                }

                                for (var i = 1; i < rectTransformsListLength; ++i)
                                {
                                    Vector2 nextMin, nextMax;
                                    var nextWorldMin = Vector3.zero;
                                    var nextWorldMax = Vector3.zero;
                                    _rectTransformsList[i].GetWorldCorners(_worldSpaceCorners);
                                    if (isWorldSpace)
                                    {
                                        nextMin = mainCamera.WorldToScreenPoint(_worldSpaceCorners[0]);
                                        nextMax = mainCamera.WorldToScreenPoint(_worldSpaceCorners[2]);
                                        nextWorldMin = _worldSpaceCorners[0];
                                        nextWorldMax = _worldSpaceCorners[2];
                                    }
                                    else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                                    {
                                        nextMin = canvasCamera.WorldToScreenPoint(_worldSpaceCorners[0]);
                                        nextMax = canvasCamera.WorldToScreenPoint(_worldSpaceCorners[2]);
                                    }
                                    else // if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                                    {
                                        nextMin = _worldSpaceCorners[0];
                                        nextMax = _worldSpaceCorners[2];
                                    }

                                    // Vector3.min and Vector3.max re-allocate new vectors on each call, avoid using them
                                    min.x = Mathf.Min(min.x, nextMin.x);
                                    min.y = Mathf.Min(min.y, nextMin.y);

                                    max.x = Mathf.Max(max.x, nextMax.x);
                                    max.y = Mathf.Max(max.y, nextMax.y);

                                    if (isWorldSpace)
                                    {
                                        worldMin.x = Mathf.Min(worldMin.x, nextWorldMin.x);
                                        worldMin.y = Mathf.Min(worldMin.y, nextWorldMin.y);
                                        worldMin.z = Mathf.Min(worldMin.z, nextWorldMin.z);
                                        worldMax.x = Mathf.Max(worldMax.x, nextWorldMax.x);
                                        worldMax.y = Mathf.Max(worldMax.y, nextWorldMax.y);
                                        worldMax.z = Mathf.Max(worldMax.z, nextWorldMax.z);
                                    }
                                }

                                var onCamera = true;
                                if (isWorldSpace || canvasCamera != mainCamera)
                                {
                                    var xLowerLimit = 0;
                                    var xUpperLimit = screenWidth;
                                    var yLowerLimit = 0;
                                    var yUpperLimit = screenHeight;
                                    if (!(min.x <= xUpperLimit && max.x >= xLowerLimit && min.y <= yUpperLimit && max.y >= yLowerLimit))
                                    {
                                        // not in camera..
                                        onCamera = false;
                                    }
                                }

                                if (onCamera)
                                {
                                    // make sure the screen space bounds has a non-zero Z size around 0
                                    var size = new Vector3((max.x - min.x) , (max.y - min.y) , 0.05f);
                                    var center = new Vector3(min.x + size.x/2, min.y + size.y/2, 0f);
                                    tStatus.screenSpaceBounds = new Bounds(center, size);
                                    tStatus.screenSpaceZOffset = 0f;

                                    if (isWorldSpace)
                                    {
                                        var worldSize = new Vector3((worldMax.x - worldMin.x), (worldMax.y - worldMin.y), (worldMax.z - worldMin.z));
                                        var worldCenter = new Vector3(worldMin.x + worldSize.x, worldMin.y + worldSize.y/2, worldMin.z + worldSize.z/2);
                                        tStatus.worldSpaceBounds = new Bounds(worldCenter, worldSize);

                                        // get the screen point values for the world max / min and find the screen space z offset closest the camera
                                        var minSp = mainCamera.WorldToScreenPoint(worldMin);
                                        var maxSp = mainCamera.WorldToScreenPoint(worldMax);
                                        tStatus.screenSpaceZOffset = Math.Min(minSp.z, maxSp.z);
                                    }
                                }
                                else
                                {
                                    tStatus.screenSpaceZOffset = 0f;
                                    tStatus.screenSpaceBounds = null;
                                    tStatus.worldSpaceBounds = null;
                                }
                            }
                        }
                    }
                }
            }
        }

        /**
         * <returns>worldSpaceObjects transform status for previous and current frame, ... will have null screenSpaceBounds if off camera</returns>
         */
        private void PopulateGameObjectTransformsForCurrentFrame()
        {
            // find everything with a renderer.. then select the last parent walking up the tree that has
            // one of the key types.. in most cases that should be the correct 'parent' game object
            // ignore UI items we already added above (these might have particle effect or other renderers on them, but does not necessarily make them world space)

            // add all the requisite transforms... avoided using Linq here for performance reasons
            var renderers = FindObjectsByType(typeof(Renderer), FindObjectsSortMode.None);
            var includeInStateObjects = FindObjectsByType(typeof(RGIncludeInState), FindObjectsSortMode.None);

            // start with a size that should fit everything to avoid numerous re-alloc
            _transformsForThisFrame.Clear();

            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer1 = (Renderer)renderers[index];
                _transformsForThisFrame.Add(renderer1.transform);
            }

            for (var i = 0; i < includeInStateObjects.Length; i++)
            {
                var includeInStateObject = (RGIncludeInState)includeInStateObjects[i];
                _transformsForThisFrame.Add(includeInStateObject.transform);
            }

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            var mainCamera = Camera.main;

            foreach (var statefulTransform in _transformsForThisFrame) // can't use for with index as this is a hashset and enumerator is better in this case
            {
                if (statefulTransform != null)
                {
                    // All of this code is verbose in order to optimize performance by avoiding using the Bounds APIs
                    // find the full bounds of the statefulGameObject
                    var statefulGameObject = statefulTransform.gameObject;

                    _rendererQueryList.Clear();
                    statefulGameObject.GetComponentsInChildren(_rendererQueryList);

                    var minWorldX = float.MaxValue;
                    var maxWorldX = float.MinValue;

                    var minWorldY = float.MaxValue;
                    var maxWorldY = float.MinValue;

                    var minWorldZ = float.MaxValue;
                    var maxWorldZ = float.MinValue;

                    var hasVisibleRenderer = false;

                    var rendererListLength = _rendererQueryList.Count;
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

                    // depending on the include only on camera setting, this object may be null
                    var tStatus = TransformStatus.GetOrCreateTransformStatus(statefulTransform);
                    _newObjects[tStatus.Id] = tStatus;

                    var onCamera = minWorldX < float.MaxValue && hasVisibleRenderer;
                    if (onCamera)
                    {

                        // convert world space to screen space
                        _worldCorners[0].x = minWorldX;
                        _worldCorners[0].y = minWorldY;
                        _worldCorners[0].z = minWorldZ;

                        _worldCorners[1].x = maxWorldX;
                        _worldCorners[1].y = minWorldY;
                        _worldCorners[1].z = minWorldZ;

                        _worldCorners[2].x = maxWorldX;
                        _worldCorners[2].y = maxWorldY;
                        _worldCorners[2].z = minWorldZ;

                        _worldCorners[3].x = minWorldX;
                        _worldCorners[3].y = maxWorldY;
                        _worldCorners[3].z = minWorldZ;

                        _worldCorners[4].x = minWorldX;
                        _worldCorners[4].y = minWorldY;
                        _worldCorners[4].z = maxWorldZ;

                        _worldCorners[5].x = maxWorldX;
                        _worldCorners[5].y = minWorldY;
                        _worldCorners[5].z = maxWorldZ;

                        _worldCorners[6].x = maxWorldX;
                        _worldCorners[6].y = maxWorldY;
                        _worldCorners[6].z = maxWorldZ;

                        _worldCorners[7].x = minWorldX;
                        _worldCorners[7].y = maxWorldY;
                        _worldCorners[7].z = maxWorldZ;

                        var minX = float.MaxValue;
                        var maxX = float.MinValue;

                        var minY = float.MaxValue;
                        var maxY = float.MinValue;

                        var minZ = float.MaxValue;
                        var maxZ = float.MinValue;

                        var worldCornersLength = _worldCorners.Length;
                        for (var i = 0; i < worldCornersLength; i++)
                        {
                            var screenSpaceObjectCorner = mainCamera.WorldToScreenPoint(_worldCorners[i]);
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

                        var xLowerLimit = 0;
                        var xUpperLimit = screenWidth;
                        var yLowerLimit = 0;
                        var yUpperLimit = screenHeight;
                        if (!(minX <= xUpperLimit && maxX >= xLowerLimit && minY <= yUpperLimit && maxY >= yLowerLimit))
                        {
                            // not in camera..
                            onCamera = false;
                        }

                        if (onCamera)
                        {
                            // make sure the screen space bounds has a non-zero Z size around 0
                            // we track the true z offset separately for ease of mouse selection on replay
                            var size = new Vector3((maxX - minX), (maxY - minY), 0.05f);
                            var center = new Vector3(minX + size.x / 2, minY + size.y / 2, 0);
                            tStatus.screenSpaceBounds = new Bounds(center, size);
                            tStatus.screenSpaceZOffset = Math.Min(minZ, maxZ);

                            var worldSize = new Vector3((maxWorldX - minWorldX), (maxWorldY - minWorldY), (maxWorldZ - minWorldZ));
                            var worldCenter = new Vector3(minWorldX + worldSize.x / 2, minWorldY + worldSize.y / 2, minWorldZ + worldSize.z / 2);
                            tStatus.worldSpaceBounds = new Bounds(worldCenter, worldSize);
                        }
                        else
                        {
                            tStatus.screenSpaceBounds = null;
                            tStatus.worldSpaceBounds = null;
                            tStatus.screenSpaceZOffset = 0f;
                        }
                    }
                    else
                    {
                        tStatus.screenSpaceBounds = null;
                        tStatus.worldSpaceBounds = null;
                        tStatus.screenSpaceZOffset = 0f;
                    }
                }
            }
        }

        public override List<KeyFrameCriteria> GetKeyFrameCriteriaForCurrentFrame(out bool hasDeltas)
        {
            var transforms = GetObjectStatusForCurrentFrame();

            var deltas = ComputeNormalizedPathBasedDeltaCounts(transforms.Item1, transforms.Item2, out hasDeltas);

            return deltas.Values
                .Select(a => new KeyFrameCriteria()
                {
                    type = KeyFrameCriteriaType.NormalizedPath,
                    transient = true,
                    data = new PathKeyFrameCriteriaData()
                    {
                        path = a.path,
                        count = a.count,
                        addedCount = a.addedCount,
                        removedCount = a.removedCount,
                        countRule = a.higherLowerCountTracker == 0 ? (a.count == 0 ? CountRule.Zero : CountRule.NonZero) : (a.higherLowerCountTracker > 0 ? CountRule.GreaterThanEqual : CountRule.LessThanEqual)
                    }
                })
                .ToList();
        }

        public RecordedGameObjectState GetStateForTransformObject(TransformStatus tStatus)
        {
            var uiObjectTransformId = tStatus.Id;
            // only process visible objects into the state
            if (tStatus.Transform != null && tStatus.screenSpaceBounds.HasValue)
            {
                var usingOldObject = _priorStates.TryGetValue(uiObjectTransformId, out var resultObject);

                if (!usingOldObject)
                {
                    var theGameObject = tStatus.Transform.gameObject;
                    var parentTransform = tStatus.Transform.parent;

                    resultObject = new RecordedGameObjectState()
                    {
                        id = uiObjectTransformId,
                        parentId = parentTransform != null ? parentTransform.GetInstanceID() : null,
                        path = tStatus.Path,
                        normalizedPath = tStatus.NormalizedPath,
                        tag = theGameObject.tag,
                        layer = LayerMask.LayerToName(theGameObject.layer),
                        scene = theGameObject.scene,
                        componentDataProviders = new List<IComponentDataProvider>()
                        {
                            new TransformComponentDataProvider()
                            {
                                Transform = transform
                            }
                        }
                    };
                }

                resultObject.position = tStatus.Transform.position;
                resultObject.rotation = tStatus.Transform.rotation;

                resultObject.screenSpaceZOffset = tStatus.screenSpaceZOffset;
                resultObject.screenSpaceBounds = tStatus.screenSpaceBounds.Value;
                resultObject.worldSpaceBounds = tStatus.worldSpaceBounds;

                var dataProviders = resultObject.componentDataProviders;
                var dataProvidersCount = dataProviders.Count;
                for (var i = 0; i < dataProvidersCount; i++)
                {
                    var cs = resultObject.componentDataProviders[i];
                    if (cs is Collider2DRecordState c2d)
                    {
                        Collider2DStateObjectPool.Add(c2d);
                    }
                    else if (cs is ColliderRecordState cd)
                    {
                        ColliderStateObjectPool.Add(cd);
                    }
                    else if (cs is Rigidbody2DRecordState r2d)
                    {
                        Rigidbody2DStateObjectPool.Add(r2d);
                    }
                    else if (cs is RigidbodyRecordState rd)
                    {
                        RigidbodyStateObjectPool.Add(rd);
                    }
                    else if (cs is BehaviourState bd)
                    {
                        BehaviourStateObjectPool.Add(bd);
                    }
                }

                dataProviders.Clear();
                ProcessTransformComponents(tStatus, dataProviders);
                return resultObject;

            }

            return null;
        }

        /**
         * <returns>(priorState, currentState)</returns>
         */
        public override (Dictionary<long, RecordedGameObjectState>, Dictionary<long, RecordedGameObjectState>) GetStateForCurrentFrame()
        {
            var frameCount = Time.frameCount;
            if (frameCount == _stateFrameNumber)
            {
                // we already processed this frame (happens when recording during replay and they both call this)
                return (_priorStates, _newStates);
            }

            _stateFrameNumber = frameCount;

            // switch the list references
            (_priorStates, _newStates) = (_newStates, _priorStates);
            _newStates.Clear();

            var statusList = GetObjectStatusForCurrentFrame().Item2;

            foreach (var oStatus in statusList.Values)
            {
                if (oStatus is TransformStatus tStatus)
                {
                    var resultObject = GetStateForTransformObject(tStatus);
                    _newStates[resultObject.id] = resultObject;
                }
            }

            _fillInStates.Clear();
            // now fill in any 'missing' parent objects in the state so that the UI can render the full tree
            foreach (var newStateEntry in _newStates.Values)
            {
                var parentId = newStateEntry.parentId;
                if (parentId.HasValue)
                {
                    if (statusList.TryGetValue(parentId.Value, out var parentStatus))
                    {
                        var parentTransform = (parentStatus is TransformStatus ptStatus) ? ptStatus.Transform : null;

                        // go up the tree until we find something in our parent hierarchy existing..
                        // stop if we hit the top
                        while (parentId.HasValue && parentTransform != null && !_newStates.ContainsKey(parentId.Value))
                        {
                            var parentParentTransform = parentTransform.parent;
                            int? parentParentId = parentParentTransform != null ? parentParentTransform.GetInstanceID() : null;
                            var usingOldObject = _priorStates.TryGetValue(parentId.Value, out var resultObject);
                            var tStatus = TransformStatus.GetOrCreateTransformStatus(parentTransform);
                            if (!usingOldObject)
                            {
                                var theGameObject = parentTransform.gameObject;
                                resultObject = new RecordedGameObjectState()
                                {
                                    id = parentId.Value,
                                    parentId = parentParentId,
                                    path = tStatus.Path,
                                    normalizedPath = tStatus.NormalizedPath,
                                    tag = theGameObject.tag,
                                    layer = LayerMask.LayerToName(theGameObject.layer),
                                    scene = theGameObject.scene,
                                    componentDataProviders = new List<IComponentDataProvider>()
                                    {
                                        new TransformComponentDataProvider()
                                        {
                                            Transform = parentTransform
                                        }
                                    },
                                    screenSpaceBounds = null,
                                    screenSpaceZOffset = 0,
                                    worldSpaceBounds = null
                                };
                            }

                            resultObject.position = parentTransform.position;
                            resultObject.rotation = parentTransform.rotation;

                            var dataProviders = resultObject.componentDataProviders;
                            var dataProvidersCount = dataProviders.Count;
                            for (var i = 0; i < dataProvidersCount; i++)
                            {
                                var cs = resultObject.componentDataProviders[i];
                                if (cs is Collider2DRecordState c2d)
                                {
                                    Collider2DStateObjectPool.Add(c2d);
                                }
                                else if (cs is ColliderRecordState cd)
                                {
                                    ColliderStateObjectPool.Add(cd);
                                }
                                else if (cs is Rigidbody2DRecordState r2d)
                                {
                                    Rigidbody2DStateObjectPool.Add(r2d);
                                }
                                else if (cs is RigidbodyRecordState rd)
                                {
                                    RigidbodyStateObjectPool.Add(rd);
                                }
                                else if (cs is BehaviourState bd)
                                {
                                    BehaviourStateObjectPool.Add(bd);
                                }
                            }

                            dataProviders.Clear();
                            ProcessTransformComponents(tStatus, dataProviders);

                            // don't update the dictionary while iterating
                            _fillInStates.Add(resultObject);

                            parentTransform = parentParentTransform;
                            parentId = parentParentId;
                        }
                    }
                }
            }

            foreach (var recordedGameObjectState in _fillInStates)
            {
                _newStates[recordedGameObjectState.id] = recordedGameObjectState;
            }

            return (_priorStates, _newStates);
        }

    }
}
