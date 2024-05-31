using System.Collections.Generic;
using System.Text;
using StateRecorder.Types;
using UnityEngine;
using Component = UnityEngine.Component;
// ReSharper disable ForCanBeConvertedToForeach - indexed for is faster and has less allocs than enumerator
// ReSharper disable LoopCanBeConvertedToQuery
namespace RegressionGames.StateRecorder
{
    public class TransformStatus
    {
        public int Id;
        public bool? HasKeyTypes;
        public string Path;
        /**
         * <summary>Has things like ' (1)' and ' (Clone)' stripped off of object names.</summary>
         */
        public string NormalizedPath;

        public Transform Transform;

        /**
         * <summary>cached pointer to the top level transform of this transform.. must check != null to avoid stale unity object references</summary>
         */
        public Transform TopLevelForThisTransform;

        public int rendererCount;

        public Bounds? screenSpaceBounds;
        public float screenSpaceZOffset;
        public Bounds? worldSpaceBounds;
    }

    public class InGameObjectFinder : MonoBehaviour
    {
        // this is only a safe pooling optimization because we don't compare colliders/behaviours/rigidbodies between prior frame and current frame state.  If we do, this optimization will become unsafe
        private static readonly List<BehaviourState> _behaviourStateObjectPool = new (100);
        private static readonly List<ColliderRecordState> _colliderStateObjectPool = new (100);
        private static readonly List<Collider2DRecordState> _collider2DStateObjectPool = new (100);
        private static readonly List<RigidbodyRecordState> _rigidbodyStateObjectPool = new (100);
        private static readonly List<Rigidbody2DRecordState> _rigidbody2DStateObjectPool = new (100);

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
        private readonly Dictionary<int, TransformStatus> _transformsIveSeen = new(1000);

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
            // Removes '(Clone)' and ' (1)' uniqueness numbers for copies
            // may also remove some valid naming pieces like (TMP).. but oh well, REGEX for this performed horribly
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

        // re-use these objects
        private static readonly StringBuilder _tPathBuilder = new StringBuilder(500);

        private TransformStatus GetOrCreateTransformStatus(Transform theTransform)
        {
            string tPath = null;
            string tPathNormalized = null;

            var id = theTransform.GetInstanceID();

            if (_transformsIveSeen.TryGetValue(id, out var status))
            {
                if (status.Path != null)
                {
                    tPath = status.Path;
                }

                if (status.NormalizedPath != null)
                {
                    tPathNormalized = status.NormalizedPath;
                }
            }

            if (tPath == null || tPathNormalized == null)
            {
                // now .. get the path in the scene.. but only from 1 level down
                // iow.. ignore the name of the scene itself for cases where many scenes are loaded together like bossroom
                var tName = theTransform.name;
                var tNameNormalized = SanitizeObjectName(theTransform.name);

                tPath = tName;
                tPathNormalized = tNameNormalized;
                var parent = theTransform.parent;
                while (parent != null)
                {
                    _tPathBuilder.Clear();
                    tPath = _tPathBuilder.Append(parent.gameObject.name).Append("/").Append(tPath).ToString();
                    _tPathBuilder.Clear();
                    tPathNormalized = _tPathBuilder.Append(SanitizeObjectName(parent.gameObject.name)).Append("/").Append(tPathNormalized).ToString();
                    parent = parent.transform.parent;
                }

                if (status != null)
                {
                    // update the cache our result
                    status.Id = id;
                    status.Transform = theTransform;
                    status.Path = tPath;
                    status.NormalizedPath = tPathNormalized;
                }
                else
                {
                    status = new TransformStatus
                    {
                        Id = id,
                        Transform = theTransform,
                        Path = tPath,
                        NormalizedPath = tPathNormalized
                    };
                    // add our result to the cache
                    _transformsIveSeen[id] = status;
                }
            }

            return status;
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
        private readonly List<Component> _childComponentsQueryList = new(100);

        private void ProcessChildTransformComponents(Transform childTransform, IList<BehaviourState> behaviours, IList<ColliderRecordState> collidersState, IList<RigidbodyRecordState> rigidbodiesState)
        {
            _childComponentsQueryList.Clear();
            childTransform.GetComponents(_childComponentsQueryList);
            TransformStatus ts = null;

            // uses object pools to minimize new allocations and GCs

            // This code re-uses the objects from the prior state as much as possible to avoid allocations
            // we try to minimize calls to GetUniqueTransformPath whenever possible
            var listLength = _childComponentsQueryList.Count;
            for (var i = 0; i < listLength; i++)
            {
                var childComponent = _childComponentsQueryList[i];
                if (childComponent is Collider colliderEntry)
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

                    cObject.path = (ts ??= GetOrCreateTransformStatus(childTransform)).Path;
                    cObject.normalizedPath = (ts ??= GetOrCreateTransformStatus(childTransform)).NormalizedPath;
                    cObject.collider = colliderEntry;

                    collidersState.Add(cObject);
                }
                else if (childComponent is Collider2D colliderEntry2D)
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

                    cObject.path = (ts ??= GetOrCreateTransformStatus(childTransform)).Path;
                    cObject.normalizedPath = (ts ??= GetOrCreateTransformStatus(childTransform)).NormalizedPath;
                    cObject.collider = colliderEntry2D;

                    collidersState.Add(cObject);
                }
                else if (childComponent is Rigidbody myRigidbody)
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

                    cObject.path = (ts ??= GetOrCreateTransformStatus(childTransform)).Path;
                    cObject.normalizedPath = (ts ??= GetOrCreateTransformStatus(childTransform)).NormalizedPath;
                    cObject.rigidbody = myRigidbody;

                    rigidbodiesState.Add(cObject);
                }
                else if (childComponent is Rigidbody2D myRigidbody2D)
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

                    cObject.path = (ts ??= GetOrCreateTransformStatus(childTransform)).Path;
                    cObject.normalizedPath = (ts ??= GetOrCreateTransformStatus(childTransform)).NormalizedPath;
                    cObject.rigidbody = myRigidbody2D;

                    rigidbodiesState.Add(cObject);
                }
                else if (childComponent is MonoBehaviour childBehaviour)
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

                    cObject.path = (ts ??= GetOrCreateTransformStatus(childTransform)).Path;
                    cObject.normalizedPath = (ts ??= GetOrCreateTransformStatus(childTransform)).NormalizedPath;
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

        private List<Transform> _nextParentTransforms = new(100);
        private List<Transform> _currentParentTransforms = new(100);

        private int _uiObjectFrameNumber = -1;
        private int _gameObjectFrameNumber = -1;
        private int _stateFrameNumber = -1;

        // pre-allocate a big list we can re-use
        private Dictionary<int,RecordedGameObjectState> _priorStates = new (1000);
        private Dictionary<int,RecordedGameObjectState> _newStates = new(1000);
        private Dictionary<int,TransformStatus> _priorUIObjects = new(1000);
        private Dictionary<int,TransformStatus> _newUIObjects = new(1000);
        private Dictionary<int,TransformStatus> _priorGameObjects = new(1000);
        private Dictionary<int,TransformStatus> _newGameObjects = new(1000);

        public void Cleanup()
        {
            _priorStates.Clear();
            _newStates.Clear();
            _priorUIObjects.Clear();
            _newUIObjects.Clear();
            _priorGameObjects.Clear();
            _newGameObjects.Clear();
            _uiObjectFrameNumber = -1;
            _gameObjectFrameNumber = -1;
            _stateFrameNumber = -1;
        }

        public class PathBasedDeltaCount
        {
            public PathBasedDeltaCount(int pathHash, string path)
            {
                this.pathHash = pathHash;
                this.path = path;
            }
            public List<int> ids = new ();
            public int pathHash;
            public string path;

            public int count;
            public int addedCount;
            public int removedCount;
            // if negative, this count went down CountRule.LessThanEqual; if positive, this count went up CountRule.GreaterThanEqual; if zero, this count didn't change CountRule.NonZero; if zero and count ==0, CountRule.Zero
            public int higherLowerCountTracker;
        }

        /**
         * argument lists are keyed by transform id
         *
         * returns hasDelta on spawns or de-spawns or change in camera visibility
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

                pathCountEntry.count++;
                pathCountEntry.ids.Add(currentEntry.Id);

                if (!priorTransformStatusList.TryGetValue(currentEntry.Id, out var oldStatus))
                {
                    hasDelta = true;
                    pathCountEntry.addedCount++;
                }
                else
                {
                    var onCameraBefore = (oldStatus.screenSpaceBounds != null);
                    var onCameraNow = (currentEntry.screenSpaceBounds != null);
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
                        // camera visibility changed.. only need to do this in one place, because if both lists didnt' have it we can't compare
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
                        // did Not think this was allowed.. but one of our partner's games gets away with it :/
                        canvas = statefulUIObject.GetComponentInChildren<Canvas>();
                    }

                    if (canvas != null)
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
                            var canvasCamera = canvas.worldCamera;
                            // will be null for overlays, but for other modes.. must be non null
                            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvasCamera != null)
                            {
                                var isWorldSpace = canvas.renderMode == RenderMode.WorldSpace;
                                _rectTransformsList.Clear();
                                statefulUIObject.GetComponentsInChildren(_rectTransformsList);
                                var rectTransformsListLength = _rectTransformsList.Count;

                                if (rectTransformsListLength > 0)
                                {
                                    Vector2 min, max;
                                    _rectTransformsList[0].GetWorldCorners(_worldSpaceCorners);
                                    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvasCamera != null)
                                    {
                                        min = RectTransformUtility.WorldToScreenPoint(canvasCamera, _worldSpaceCorners[0]);
                                        max = RectTransformUtility.WorldToScreenPoint(canvasCamera, _worldSpaceCorners[2]);
                                    }
                                    else
                                    {
                                        min = _worldSpaceCorners[0];
                                        max = _worldSpaceCorners[2];
                                    }

                                    // default values, may or may not be used depending on if isWorldSpace
                                    Vector3 worldMin = _worldSpaceCorners[0];
                                    Vector3 worldMax = _worldSpaceCorners[2];

                                    for (var i = 1; i < rectTransformsListLength; ++i)
                                    {
                                        Vector2 nextMin, nextMax;
                                        _rectTransformsList[i].GetWorldCorners(_worldSpaceCorners);
                                        if (canvasCamera != null)
                                        {
                                            nextMin = RectTransformUtility.WorldToScreenPoint(canvasCamera, _worldSpaceCorners[0]);
                                            nextMax = RectTransformUtility.WorldToScreenPoint(canvasCamera, _worldSpaceCorners[2]);
                                        }
                                        else
                                        {
                                            nextMin = _worldSpaceCorners[0];
                                            nextMax = _worldSpaceCorners[2];
                                        }

                                        // Vector3.min and Vector3.max re-allocate new vectors on each call, avoid using them
                                        min.x = Mathf.Min(min.x, nextMin.x);
                                        min.y = Mathf.Min(min.y, nextMin.y);

                                        max.x = Mathf.Min(max.x, nextMax.x);
                                        max.y = Mathf.Min(max.y, nextMax.y);

                                        if (isWorldSpace)
                                        {
                                            worldMin.x = Mathf.Min(worldMin.x, _worldSpaceCorners[0].x);
                                            worldMin.y = Mathf.Min(worldMin.y, _worldSpaceCorners[0].y);
                                            worldMin.z = Mathf.Min(worldMin.y, _worldSpaceCorners[0].z);
                                            worldMax.x = Mathf.Min(worldMin.x, _worldSpaceCorners[2].x);
                                            worldMax.y = Mathf.Min(worldMin.y, _worldSpaceCorners[2].y);
                                            worldMin.z = Mathf.Min(worldMin.y, _worldSpaceCorners[2].z);
                                        }
                                    }

                                    var onCamera = true;
                                    if (isWorldSpace)
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

                                    var objectTransform = statefulUIObject.transform;
                                    var tStatus = GetOrCreateTransformStatus(objectTransform);
                                    _newUIObjects[tStatus.Id] = tStatus;

                                    if (onCamera)
                                    {
                                        // make sure the screen space bounds has a non-zero Z size around 0
                                        var size = new Vector3((max.x - min.x) , (max.y - min.y) , 0.05f);
                                        var center = new Vector3(min.x + size.x/2, min.y + size.y/2, 0f);
                                        tStatus.screenSpaceBounds = new Bounds(center, size);
                                        tStatus.screenSpaceZOffset = 0f;

                                        tStatus.rendererCount = canvasRenderersLength;

                                        if (isWorldSpace)
                                        {
                                            var worldSize = new Vector3((worldMax.x - worldMin.x), (worldMax.y - worldMin.y), (worldMax.z - worldMin.z));
                                            var worldCenter = new Vector3(worldMin.x + worldSize.x, worldMin.y + worldSize.y/2, worldMin.z + worldSize.z/2);
                                            tStatus.worldSpaceBounds = new Bounds(worldCenter, worldSize);
                                            //TODO: tStatus.screenSpaceZOffset = ???
                                        }
                                    }
                                    else
                                    {
                                        tStatus.screenSpaceBounds = null;
                                        tStatus.worldSpaceBounds = null;
                                    }
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

            if (!collapseRenderersIntoTopLevelGameObject)
            {
                for (var index = 0; index < renderers.Length; index++)
                {
                    var renderer1 = (Renderer)renderers[index];
                    if (!_newStates.ContainsKey(renderer1.transform.GetInstanceID()))
                    {
                        _transformsForThisFrame.Add(renderer1.transform);
                    }
                }

                for (var i = 0; i < includeInStateObjects.Length; i++)
                {
                    var includeInStateObject = (RGIncludeInState)includeInStateObjects[i];
                    if (!_newStates.ContainsKey(includeInStateObject.transform.GetInstanceID()))
                    {
                        _transformsForThisFrame.Add(includeInStateObject.transform);
                    }
                }
            }
            else
            {
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer1 = (Renderer)renderers[i];
                    FindTransformsForThisFrame(renderer1.transform, _transformsForThisFrame);
                }

                for (var i = 0; i < includeInStateObjects.Length; i++)
                {
                    var includeInStateObject = (RGIncludeInState)includeInStateObjects[i];
                    FindTransformsForThisFrame(includeInStateObject.transform, _transformsForThisFrame);
                }
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

                        // depending on the include only on camera setting, this object may be null
                        var tStatus = GetOrCreateTransformStatus(statefulTransform);

                        tStatus.rendererCount = rendererListLength;

                        if (onCamera)
                        {
                            // make sure the screen space bounds has a non-zero Z size around 0
                            // we track the true z offset separately for ease of mouse selection on replay
                            var size = new Vector3((maxX - minX), (maxY - minY), 0.05f);
                            var center = new Vector3(minX + size.x / 2, minY + size.y / 2, 0);
                            tStatus.screenSpaceBounds = new Bounds(center, size);
                            tStatus.screenSpaceZOffset = maxZ;

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

                        _newGameObjects[tStatus.Id] = tStatus;
                    }
                }
            }

            return (_priorGameObjects, _newGameObjects);
        }

        /**
         * <returns>(priorState, currentState, offCameraTransforms)</returns>
         */
        //TODO Use passed in ui / world objects to record state.
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
                if (tStatus.screenSpaceBounds.HasValue)
                {
                    var usingOldObject = _priorStates.TryGetValue(uiObjectTransformId, out var resultObject);

                    if (!usingOldObject)
                    {
                        resultObject = new RecordedGameObjectState()
                        {
                            id = uiObjectTransformId,
                            transform = tStatus.Transform,
                            path = tStatus.Path,
                            normalizedPath = tStatus.NormalizedPath,
                            tag = tStatus.Transform.gameObject.tag,
                            layer = LayerMask.LayerToName(tStatus.Transform.gameObject.layer),
                            scene = tStatus.Transform.gameObject.scene,
                            behaviours = new List<BehaviourState>(),
                            colliders = new List<ColliderRecordState>(),
                            rigidbodies = new List<RigidbodyRecordState>(),
                            screenSpaceZOffset = tStatus.screenSpaceZOffset,
                            screenSpaceBounds = tStatus.screenSpaceBounds.Value,
                            worldSpaceBounds = tStatus.worldSpaceBounds
                        };
                    }

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

                    // instead of searching for child Behaviours, we instead walk child tree of transforms and check each for Behaviours
                    // this allows us to avoid calling GetUniqueTransformPath more than once for any single transform
                    _nextParentTransforms.Clear();
                    _currentParentTransforms.Clear();

                    // process the first object here, then evaluate its children
                    ProcessChildTransformComponents(tStatus.Transform, behaviours, collidersState, null);
                    var childCount = tStatus.Transform.childCount;
                    for (var k = 0; k < childCount; k++)
                    {
                        var currentChildTransform = tStatus.Transform.GetChild(k);
                        _nextParentTransforms.Add(currentChildTransform);
                    }

                    // evaluate the children
                    while (_nextParentTransforms.Count > 0)
                    {
                        // swap the list references so we can process from 'current' and be able to setup 'next'
                        (_currentParentTransforms, _nextParentTransforms) = (_nextParentTransforms, _currentParentTransforms);
                        _nextParentTransforms.Clear();

                        // load the next layer as we go
                        var currentCount = _currentParentTransforms.Count;
                        for (var i = 0; i < currentCount; i++)
                        {
                            var currentTransform = _currentParentTransforms[i];
                            // only process down so far.. if we hit another ui component.. stop traversing that part of the tree
                            var hasRenderer = currentTransform.GetComponent(typeof(CanvasRenderer)) != null;
                            if (!hasRenderer)
                            {
                                ProcessChildTransformComponents(currentTransform, behaviours, collidersState, null);
                                childCount = currentTransform.childCount;
                                for (var k = 0; k < childCount; k++)
                                {
                                    var currentChildTransform = currentTransform.GetChild(k);
                                    _nextParentTransforms.Add(currentChildTransform);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var tStatus in gameObjectTransformStatusList)
            {
                var gameObjectTransformId = tStatus.Id;
                if (tStatus.screenSpaceBounds.HasValue)
                {
                    var usingOldObject = _priorStates.TryGetValue(gameObjectTransformId, out var resultObject);

                    if (!usingOldObject)
                    {
                        resultObject = new RecordedGameObjectState()
                        {
                            id = gameObjectTransformId,
                            transform = tStatus.Transform,
                            path = tStatus.Path,
                            normalizedPath = tStatus.NormalizedPath,
                            tag = tStatus.Transform.gameObject.tag,
                            layer = LayerMask.LayerToName(tStatus.Transform.gameObject.layer),
                            scene = tStatus.Transform.gameObject.scene,
                            behaviours = new List<BehaviourState>(),
                            colliders = new List<ColliderRecordState>(),
                            rigidbodies = new List<RigidbodyRecordState>(),
                            screenSpaceBounds = tStatus.screenSpaceBounds.Value,
                            screenSpaceZOffset = tStatus.screenSpaceZOffset,
                            worldSpaceBounds = tStatus.worldSpaceBounds
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


                    // instead of searching for child Behaviours, we instead walk child tree of transforms and check each for Behaviours
                    // this allows us to avoid calling GetUniqueTransformPath more than once for any single transform
                    _nextParentTransforms.Clear();
                    _currentParentTransforms.Clear();

                    // process the first object here, then evaluate its children
                    ProcessChildTransformComponents(tStatus.Transform, behaviours, collidersState, rigidbodiesState);
                    var childCount = tStatus.Transform.childCount;
                    for (var k = 0; k < childCount; k++)
                    {
                        var currentChildTransform = tStatus.Transform.GetChild(k);
                        _nextParentTransforms.Add(currentChildTransform);
                    }

                    // evaluate the children
                    while (_nextParentTransforms.Count > 0)
                    {
                        // swap the list references so we can process from 'current' and be able to setup 'next'
                        (_currentParentTransforms, _nextParentTransforms) = (_nextParentTransforms, _currentParentTransforms);
                        _nextParentTransforms.Clear();

                        // load the next layer as we go
                        var currentCount = _currentParentTransforms.Count;
                        for (var i = 0; i < currentCount; i++)
                        {
                            var currentTransform = _currentParentTransforms[i];
                            // only process down so far.. if we hit another rendered component.. stop traversing that part of the tree
                            var hasRenderer = currentTransform.GetComponent(typeof(Renderer)) != null;
                            if (!hasRenderer)
                            {
                                ProcessChildTransformComponents(currentTransform, behaviours, collidersState, rigidbodiesState);
                                childCount = currentTransform.childCount;
                                for (var k = 0; k < childCount; k++)
                                {
                                    var currentChildTransform = currentTransform.GetChild(k);
                                    _nextParentTransforms.Add(currentChildTransform);
                                }
                            }
                        }

                    }
                    _newStates[resultObject.id] = resultObject;
                }
            }

            return (_priorStates, _newStates);
        }

        // allocate this rather large list 1 time to avoid re-allocation on each tick object
        private readonly List<Component> _componentsInParentList = new(100);

        private void FindTransformsForThisFrame(Transform startingTransform, HashSet<Transform> transformsForThisFrame)
        {

            var transformId = startingTransform.GetInstanceID();
            // we walk all the way to the root and record which ones had key types to find the 'parent'
            if (_transformsIveSeen.TryGetValue(transformId, out var tStatus))
            {
                tStatus.HasKeyTypes = true;
            }
            else
            {
                tStatus = new TransformStatus
                {
                    HasKeyTypes = true
                };
                _transformsIveSeen[transformId] = tStatus;
            }

            var maybeTopLevel = startingTransform;


            if (tStatus is { TopLevelForThisTransform: not null})
            {
                maybeTopLevel = tStatus.TopLevelForThisTransform;
            }
            else
            {
                // find any parents we need to evaluate
                var nextParent = startingTransform.parent;

                // go until the root of the tree
                while (nextParent != null)
                {
                    var nextParentId = nextParent.GetInstanceID();
                    var parentHasKeyTypes = false;
                    _transformsIveSeen.TryGetValue(nextParentId, out var nextParentStatus);

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
                        _transformsIveSeen[nextParentId] = nextParentStatus;
                    }

                    if (parentHasKeyTypes)
                    {
                        // track the new one
                        maybeTopLevel = nextParent;

                        if (!collapseRenderersIntoTopLevelGameObject && !_newStates.ContainsKey(nextParent.transform.GetInstanceID()))
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
                if (collapseRenderersIntoTopLevelGameObject && !_newStates.ContainsKey(maybeTopLevel.transform.GetInstanceID()))
                {
                    transformsForThisFrame.Add(maybeTopLevel);
                }
            }
        }
    }
}
