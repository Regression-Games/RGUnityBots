using System;
using System.Collections.Generic;
using RegressionGames.StateRecorder.Models;
using RegressionGames.StateRecorder.Types;
using UnityEngine;
using Component = UnityEngine.Component;
// ReSharper disable ForCanBeConvertedToForeach - indexed for is faster and has less allocs than enumerator
// ReSharper disable LoopCanBeConvertedToQuery
namespace RegressionGames.StateRecorder
{
    public class InGameObjectFinder : MonoBehaviour
    {
        // this is only a safe pooling optimization because we don't compare colliders/behaviours/rigidbodies between prior frame and current frame state.  If we do, this optimization will become unsafe
        private static readonly List<BehaviourState> _behaviourStateObjectPool = new (100);
        private static readonly List<ColliderRecordState> _colliderStateObjectPool = new (100);
        private static readonly List<Collider2DRecordState> _collider2DStateObjectPool = new (100);
        private static readonly List<RigidbodyRecordState> _rigidbodyStateObjectPool = new (100);
        private static readonly List<Rigidbody2DRecordState> _rigidbody2DStateObjectPool = new (100);

        private static InGameObjectFinder _this;

        [Tooltip("(WARNING: Performance Impact) Include field/property values for behaviours.")]
        public bool collectStateFromBehaviours;

        public void Awake()
        {
            if (_this != null)
            {
                TransformStatus.Reset();
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

        private void ProcessTransformComponents(TransformStatus transformStatus, IList<BehaviourState> behaviours, IList<ColliderRecordState> collidersState, IList<RigidbodyRecordState> rigidbodiesState)
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
                    var poolCount = _colliderStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = _colliderStateObjectPool[poolCount - 1];
                        _colliderStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new ColliderRecordState();
                    }

                    cObject.collider = colliderEntry;

                    collidersState.Add(cObject);
                }
                else if (component is Collider2D colliderEntry2D)
                {
                    Collider2DRecordState cObject;
                    var poolCount = _collider2DStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = _collider2DStateObjectPool[poolCount - 1];
                        _collider2DStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new Collider2DRecordState();
                    }

                    cObject.collider = colliderEntry2D;

                    collidersState.Add(cObject);
                }
                else if (component is Rigidbody myRigidbody)
                {
                    RigidbodyRecordState cObject;
                    var poolCount = _rigidbodyStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = _rigidbodyStateObjectPool[poolCount - 1];
                        _rigidbodyStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new RigidbodyRecordState();
                    }

                    cObject.rigidbody = myRigidbody;

                    rigidbodiesState.Add(cObject);
                }
                else if (component is Rigidbody2D myRigidbody2D)
                {
                    Rigidbody2DRecordState cObject;
                    var poolCount = _rigidbody2DStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = _rigidbody2DStateObjectPool[poolCount - 1];
                        _rigidbody2DStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new Rigidbody2DRecordState();
                    }

                    cObject.rigidbody = myRigidbody2D;

                    rigidbodiesState.Add(cObject);
                }
                else if (component is MonoBehaviour childBehaviour)
                {
                    BehaviourState cObject;
                    var poolCount = _behaviourStateObjectPool.Count;
                    if (poolCount > 0)
                    {
                        // remove from end of list
                        cObject = _behaviourStateObjectPool[poolCount - 1];
                        _behaviourStateObjectPool.RemoveAt(poolCount - 1);
                    }
                    else
                    {
                        cObject = new BehaviourState();
                    }

                    cObject.name = childBehaviour.GetType().FullName;
                    cObject.state = childBehaviour;

                    behaviours.Add(cObject);
                }
            }
        }

        // allocate these rather large things 1 time to save allocations on each tick object
        private readonly List<RectTransform> _rectTransformsList = new(100);
        private readonly HashSet<Transform> _transformsForThisFrame = new (1000);

        private readonly Vector3[] _worldSpaceCorners = new Vector3[4];

        private int _uiObjectFrameNumber = -1;
        private int _gameObjectFrameNumber = -1;
        private int _stateFrameNumber = -1;

        // pre-allocate a big list we can re-use
        private Dictionary<int,RecordedGameObjectState> _priorStates = new (1000);
        private Dictionary<int,RecordedGameObjectState> _newStates = new(1000);
        private readonly List<RecordedGameObjectState> _fillInStates = new (1000);

        private Dictionary<int,TransformStatus> _priorUIObjects = new(1000);
        private Dictionary<int,TransformStatus> _newUIObjects = new(1000);
        private Dictionary<int,TransformStatus> _priorGameObjects = new(1000);
        private Dictionary<int,TransformStatus> _newGameObjects = new(1000);

        public void Cleanup()
        {
            _priorStates.Clear();
            _newStates.Clear();
            _fillInStates.Clear();

            _priorUIObjects.Clear();
            _newUIObjects.Clear();
            _priorGameObjects.Clear();
            _newGameObjects.Clear();
            _uiObjectFrameNumber = -1;
            _gameObjectFrameNumber = -1;
            _stateFrameNumber = -1;
        }

        /**
         * argument lists are keyed by transform id
         *
         * returns hasDelta on spawns or de-spawns or change in camera visibility .. result is keyed on path hash
         */
        public Dictionary<int, PathBasedDeltaCount> ComputeNormalizedPathBasedDeltaCounts(Dictionary<int,TransformStatus> priorTransformStatusList, Dictionary<int, TransformStatus> currentTransformStatusList, out bool hasDelta)
        {
            hasDelta = false;
            var result = new Dictionary<int, PathBasedDeltaCount>(); // keyed by path hash
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
            foreach( KeyValuePair<int, TransformStatus> entry in priorTransformStatusList)
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

        /**
         * <returns>uiObjects transform status for previous and current frame, ... will have null screenSpaceBounds if off camera</returns>
         */
        public (Dictionary<int, TransformStatus>,Dictionary<int, TransformStatus>) GetUITransformsForCurrentFrame()
        {
            var frameCount = Time.frameCount;
            if (frameCount == _uiObjectFrameNumber)
            {
                // we already processed this frame (happens when recording during replay and they both call this)
                return (_priorUIObjects, _newUIObjects);
            }

            _uiObjectFrameNumber = frameCount;

            // switch the list references
            (_priorUIObjects, _newUIObjects) = (_newUIObjects, _priorUIObjects);
            _newUIObjects.Clear();

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
                                _newUIObjects[tStatus.Id] = tStatus;

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
            return (_priorUIObjects, _newUIObjects);
        }

        /**
         * <returns>worldSpaceObjects transform status for previous and current frame, ... will have null screenSpaceBounds if off camera</returns>
         */
        public (Dictionary<int, TransformStatus>,Dictionary<int, TransformStatus>) GetGameObjectTransformsForCurrentFrame()
        {
            var frameCount = Time.frameCount;
            if (frameCount == _gameObjectFrameNumber)
            {
                // we already processed this frame (happens when recording during replay and they both call this)
                return (_priorGameObjects, _newGameObjects);
            }

            _gameObjectFrameNumber = frameCount;

            // switch the list references
            (_priorGameObjects, _newGameObjects) = (_newGameObjects, _priorGameObjects);
            _newGameObjects.Clear();

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
                    _newGameObjects[tStatus.Id] = tStatus;

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

            return (_priorGameObjects, _newGameObjects);
        }

        /**
         * <returns>(priorState, currentState, offCameraTransforms)</returns>
         */
        public (Dictionary<int, RecordedGameObjectState>, Dictionary<int, RecordedGameObjectState>) GetStateForCurrentFrame(IEnumerable<TransformStatus> uiObjectTransformStatusList, IEnumerable<TransformStatus> gameObjectTransformStatusList)
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

            foreach (var tStatus in uiObjectTransformStatusList)
            {
                var uiObjectTransformId = tStatus.Id;
                // only process visible objects into the state
                if (tStatus.screenSpaceBounds.HasValue)
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
                            transform = tStatus.Transform,
                            path = tStatus.Path,
                            normalizedPath = tStatus.NormalizedPath,
                            tag = theGameObject.tag,
                            layer = LayerMask.LayerToName(theGameObject.layer),
                            scene = theGameObject.scene,
                            behaviours = new List<BehaviourState>(),
                            colliders = new List<ColliderRecordState>(),
                            rigidbodies = new List<RigidbodyRecordState>()
                        };
                    }

                    resultObject.screenSpaceZOffset = tStatus.screenSpaceZOffset;
                    resultObject.screenSpaceBounds = tStatus.screenSpaceBounds.Value;
                    resultObject.worldSpaceBounds = tStatus.worldSpaceBounds;

                    _newStates[resultObject.id] = resultObject;

                    var collidersState = resultObject.colliders;
                    var collidersStateCount = collidersState.Count;
                    for (var i = 0; i < collidersStateCount; i++)
                    {
                        var cs = collidersState[i];
                        if (cs is Collider2DRecordState c2d)
                        {
                            _collider2DStateObjectPool.Add(c2d);
                        }
                        else
                        {
                            _colliderStateObjectPool.Add(cs);
                        }
                    }
                    collidersState.Clear();

                    IList<BehaviourState> behaviours = resultObject.behaviours;
                    _behaviourStateObjectPool.AddRange(behaviours);
                    behaviours.Clear();

                    ProcessTransformComponents(tStatus, behaviours, collidersState, null);
                }
            }

            foreach (var tStatus in gameObjectTransformStatusList)
            {
                var gameObjectTransformId = tStatus.Id;
                // only process visible objects into the state
                if (tStatus.screenSpaceBounds.HasValue)
                {
                    var usingOldObject = _priorStates.TryGetValue(gameObjectTransformId, out var resultObject);

                    if (!usingOldObject)
                    {
                        var theGameObject = tStatus.Transform.gameObject;
                        var parentTransform = tStatus.Transform.parent;

                        resultObject = new RecordedGameObjectState()
                        {
                            id = gameObjectTransformId,
                            parentId = parentTransform != null ? parentTransform.GetInstanceID() : null,
                            transform = tStatus.Transform,
                            path = tStatus.Path,
                            normalizedPath = tStatus.NormalizedPath,
                            tag = theGameObject.tag,
                            layer = LayerMask.LayerToName(theGameObject.layer),
                            scene = theGameObject.scene,
                            behaviours = new List<BehaviourState>(),
                            colliders = new List<ColliderRecordState>(),
                            rigidbodies = new List<RigidbodyRecordState>()
                        };
                    }

                    resultObject.screenSpaceZOffset = tStatus.screenSpaceZOffset;
                    resultObject.screenSpaceBounds = tStatus.screenSpaceBounds.Value;
                    resultObject.worldSpaceBounds = tStatus.worldSpaceBounds;

                    var behaviours = resultObject.behaviours;
                    _behaviourStateObjectPool.AddRange(behaviours);
                    behaviours.Clear();

                    var collidersState = resultObject.colliders;
                    var collidersStateCount = collidersState.Count;
                    for (var i = 0; i < collidersStateCount; i++)
                    {
                        var cs = collidersState[i];
                        if (cs is Collider2DRecordState c2d)
                        {
                            _collider2DStateObjectPool.Add(c2d);
                        }
                        else
                        {
                            _colliderStateObjectPool.Add(cs);
                        }
                    }
                    collidersState.Clear();

                    var rigidbodiesState = resultObject.rigidbodies;
                    var rigidbodiesStateCount = rigidbodiesState.Count;
                    for (var i = 0; i < rigidbodiesStateCount; i++)
                    {
                        var rs = rigidbodiesState[i];
                        if (rs is Rigidbody2DRecordState r2d)
                        {
                            _rigidbody2DStateObjectPool.Add(r2d);
                        }
                        else
                        {
                            _rigidbodyStateObjectPool.Add(rs);
                        }
                    }
                    rigidbodiesState.Clear();

                    // process the first object here
                    ProcessTransformComponents(tStatus, behaviours, collidersState, rigidbodiesState);
                    _newStates[resultObject.id] = resultObject;
                }
            }

            _fillInStates.Clear();
            // now fill in any 'missing' parent objects in the state so that the UI can render the full tree
            foreach (var newStateEntry in _newStates.Values)
            {
                int? parentId = newStateEntry.parentId;
                var parentTransform = newStateEntry.transform.parent;
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
                            transform = parentTransform,
                            path = tStatus.Path,
                            normalizedPath = tStatus.NormalizedPath,
                            tag = theGameObject.tag,
                            layer = LayerMask.LayerToName(theGameObject.layer),
                            scene = theGameObject.scene,
                            behaviours = new List<BehaviourState>(),
                            colliders = new List<ColliderRecordState>(),
                            rigidbodies = new List<RigidbodyRecordState>(),
                            screenSpaceBounds = null,
                            screenSpaceZOffset = 0,
                            worldSpaceBounds = null
                        };
                    }

                    var behaviours = resultObject.behaviours;
                    _behaviourStateObjectPool.AddRange(behaviours);
                    behaviours.Clear();

                    var collidersState = resultObject.colliders;
                    var collidersStateCount = collidersState.Count;
                    for (var i = 0; i < collidersStateCount; i++)
                    {
                        var cs = collidersState[i];
                        if (cs is Collider2DRecordState c2d)
                        {
                            _collider2DStateObjectPool.Add(c2d);
                        }
                        else
                        {
                            _colliderStateObjectPool.Add(cs);
                        }
                    }
                    collidersState.Clear();

                    var rigidbodiesState = resultObject.rigidbodies;
                    var rigidbodiesStateCount = rigidbodiesState.Count;
                    for (var i = 0; i < rigidbodiesStateCount; i++)
                    {
                        var rs = rigidbodiesState[i];
                        if (rs is Rigidbody2DRecordState r2d)
                        {
                            _rigidbody2DStateObjectPool.Add(r2d);
                        }
                        else
                        {
                            _rigidbodyStateObjectPool.Add(rs);
                        }
                    }
                    rigidbodiesState.Clear();

                    // process the first object here
                    ProcessTransformComponents(tStatus, behaviours, collidersState, rigidbodiesState);

                    // don't update the dictionary while iterating
                    _fillInStates.Add(resultObject);

                    parentTransform = parentParentTransform;
                    parentId = parentParentId;
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
