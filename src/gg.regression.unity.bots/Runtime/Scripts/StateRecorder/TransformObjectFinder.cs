using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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

            StringBuilder typesString = new StringBuilder();
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type ty in a.GetTypes())
                {
                    typesString.Append(ty.AssemblyQualifiedName).Append("\r\n");
                }
            }

            var theString = typesString.ToString();
            Debug.Log(theString);

            // Is there a better place to do this, maybe.. but for now, this gets the ECS subsystem loaded on the same object as this behaviour
            Type t = Type.GetType("RegressionGames.StateRecorder.ECS.EntityObjectFinder, RegressionGames_ECS, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
            if (t == null)
            {
                RGDebug.LogInfo("Regression Games ECS Package not found, support for ECS won't be loaded");
            }
            else
            {
                var entityFinder = this.gameObject.GetComponent(t);
                if (entityFinder == null)
                {
                    this.gameObject.AddComponent(t);
                }
            }

        }

        // allocate this rather large list 1 time to avoid realloc on each tick object
        private static readonly List<Renderer> RendererQueryList = new(1000);

        // avoid re-allocating all these vector3 objects for each element in each tick
        private static readonly Vector3[] WorldCorners =
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
        private static readonly List<RectTransform> RectTransformsList = new(100);
        private readonly HashSet<Transform> _transformsForThisFrame = new (1000);

        private static readonly Vector3[] WorldSpaceCorners = new Vector3[4];

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

        public override (Dictionary<long, ObjectStatus>, Dictionary<long, ObjectStatus>) GetObjectStatusForCurrentFrame()
        {
            var frameCount = Time.frameCount;
            if (frameCount == _objectFrameNumber)
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

        public static (Bounds?, float, Bounds?) SelectBoundsForTransform(Transform theTransform)
        {

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var mainCamera = Camera.main;

            var canvasRenderer = theTransform.GetComponent<CanvasRenderer>();
            if (canvasRenderer != null)
            {
                // UI component object
                var canvas = theTransform.GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    // did Not think having canvas as a child instead of a parent was allowed.. but one of our partner's games gets away with it :/
                    canvas = theTransform.GetComponentInChildren<Canvas>();
                }

                if (canvas != null && canvas.enabled)
                {
                    // screen space
                    var canvasGroup = theTransform.GetComponentInParent<CanvasGroup>();
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
                        RectTransformsList.Clear();
                        theTransform.GetComponentsInChildren(RectTransformsList);
                        var rectTransformsListLength = RectTransformsList.Count;

                        if (rectTransformsListLength > 0)
                        {
                            Vector2 min, max;
                            var worldMin = Vector3.zero;
                            var worldMax = Vector3.zero;
                            RectTransformsList[0].GetWorldCorners(WorldSpaceCorners);
                            if (isWorldSpace)
                            {
                                min = mainCamera.WorldToScreenPoint(WorldSpaceCorners[0]);
                                max = mainCamera.WorldToScreenPoint(WorldSpaceCorners[2]);
                                worldMin = WorldSpaceCorners[0];
                                worldMax = WorldSpaceCorners[2];
                            }
                            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                            {
                                min = canvasCamera.WorldToScreenPoint(WorldSpaceCorners[0]);
                                max = canvasCamera.WorldToScreenPoint(WorldSpaceCorners[2]);
                            }
                            else // if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                            {
                                min = WorldSpaceCorners[0];
                                max = WorldSpaceCorners[2];
                            }

                            for (var i = 1; i < rectTransformsListLength; ++i)
                            {
                                Vector2 nextMin, nextMax;
                                var nextWorldMin = Vector3.zero;
                                var nextWorldMax = Vector3.zero;
                                RectTransformsList[i].GetWorldCorners(WorldSpaceCorners);
                                if (isWorldSpace)
                                {
                                    nextMin = mainCamera.WorldToScreenPoint(WorldSpaceCorners[0]);
                                    nextMax = mainCamera.WorldToScreenPoint(WorldSpaceCorners[2]);
                                    nextWorldMin = WorldSpaceCorners[0];
                                    nextWorldMax = WorldSpaceCorners[2];
                                }
                                else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                                {
                                    nextMin = canvasCamera.WorldToScreenPoint(WorldSpaceCorners[0]);
                                    nextMax = canvasCamera.WorldToScreenPoint(WorldSpaceCorners[2]);
                                }
                                else // if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                                {
                                    nextMin = WorldSpaceCorners[0];
                                    nextMax = WorldSpaceCorners[2];
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
                                var size = new Vector3((max.x - min.x), (max.y - min.y), 0.05f);
                                var center = new Vector3(min.x + size.x / 2, min.y + size.y / 2, 0f);

                                if (isWorldSpace)
                                {
                                    var worldSize = new Vector3((worldMax.x - worldMin.x), (worldMax.y - worldMin.y), (worldMax.z - worldMin.z));
                                    var worldCenter = new Vector3(worldMin.x + worldSize.x, worldMin.y + worldSize.y / 2, worldMin.z + worldSize.z / 2);

                                    // get the screen point values for the world max / min and find the screen space z offset closest the camera
                                    var minSp = mainCamera.WorldToScreenPoint(worldMin);
                                    var maxSp = mainCamera.WorldToScreenPoint(worldMax);
                                    return (new Bounds(center, size), Math.Min(minSp.z, maxSp.z), new Bounds(worldCenter, worldSize));
                                }

                                return (new Bounds(center, size), 0f, null);
                            }
                        }
                    }
                }
            }
            else
            {

                // non ui object
                // All of this code is verbose in order to optimize performance by avoiding using the Bounds APIs
                // find the full bounds of the statefulGameObject
                var statefulGameObject = theTransform.gameObject;

                RendererQueryList.Clear();
                statefulGameObject.GetComponentsInChildren(RendererQueryList);

                var minWorldX = float.MaxValue;
                var maxWorldX = float.MinValue;

                var minWorldY = float.MaxValue;
                var maxWorldY = float.MinValue;

                var minWorldZ = float.MaxValue;
                var maxWorldZ = float.MinValue;

                var hasVisibleRenderer = false;

                var rendererListLength = RendererQueryList.Count;
                for (var i = 0; i < rendererListLength; i++)
                {
                    var nextRenderer = RendererQueryList[i];
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
                    WorldCorners[0].x = minWorldX;
                    WorldCorners[0].y = minWorldY;
                    WorldCorners[0].z = minWorldZ;

                    WorldCorners[1].x = maxWorldX;
                    WorldCorners[1].y = minWorldY;
                    WorldCorners[1].z = minWorldZ;

                    WorldCorners[2].x = maxWorldX;
                    WorldCorners[2].y = maxWorldY;
                    WorldCorners[2].z = minWorldZ;

                    WorldCorners[3].x = minWorldX;
                    WorldCorners[3].y = maxWorldY;
                    WorldCorners[3].z = minWorldZ;

                    WorldCorners[4].x = minWorldX;
                    WorldCorners[4].y = minWorldY;
                    WorldCorners[4].z = maxWorldZ;

                    WorldCorners[5].x = maxWorldX;
                    WorldCorners[5].y = minWorldY;
                    WorldCorners[5].z = maxWorldZ;

                    WorldCorners[6].x = maxWorldX;
                    WorldCorners[6].y = maxWorldY;
                    WorldCorners[6].z = maxWorldZ;

                    WorldCorners[7].x = minWorldX;
                    WorldCorners[7].y = maxWorldY;
                    WorldCorners[7].z = maxWorldZ;

                    var minX = float.MaxValue;
                    var maxX = float.MinValue;

                    var minY = float.MaxValue;
                    var maxY = float.MinValue;

                    var minZ = float.MaxValue;
                    var maxZ = float.MinValue;

                    var worldCornersLength = WorldCorners.Length;
                    for (var i = 0; i < worldCornersLength; i++)
                    {
                        var screenSpaceObjectCorner = mainCamera.WorldToScreenPoint(WorldCorners[i]);
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

                        var worldSize = new Vector3((maxWorldX - minWorldX), (maxWorldY - minWorldY), (maxWorldZ - minWorldZ));
                        var worldCenter = new Vector3(minWorldX + worldSize.x / 2, minWorldY + worldSize.y / 2, minWorldZ + worldSize.z / 2);

                        return (new Bounds(center, size), Math.Min(minZ, maxZ), new Bounds(worldCenter, worldSize));
                    }
                    else
                    {
                        return (null, 0f, null);
                    }
                }

            }


            return (null, 0f, null);
        }

        private void PopulateUITransformsForCurrentFrame()
        {

            var canvasRenderers = FindObjectsByType(typeof(CanvasRenderer), FindObjectsSortMode.None);

            // we re-use this over and over instead of allocating multiple times
            var canvasRenderersLength = canvasRenderers.Length;
            for (var j = 0; j < canvasRenderersLength; j++)
            {
                var canvasRenderer = canvasRenderers[j];
                var statefulUIObject = ((CanvasRenderer)canvasRenderer).gameObject;
                if (statefulUIObject != null && statefulUIObject.GetComponentInParent<RGExcludeFromState>() == null)
                {
                    var tStatus = TransformStatus.GetOrCreateTransformStatus(statefulUIObject.transform);
                    _newObjects[tStatus.Id] = tStatus;

                    var bounds = SelectBoundsForTransform(statefulUIObject.transform);
                    tStatus.screenSpaceBounds = bounds.Item1;
                    tStatus.screenSpaceZOffset = bounds.Item2;
                    tStatus.worldSpaceBounds = bounds.Item3;
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

            foreach (var theTransform in _transformsForThisFrame)
            {
                var tStatus = TransformStatus.GetOrCreateTransformStatus(theTransform);
                _newObjects[tStatus.Id] = tStatus;
                var bounds = SelectBoundsForTransform(theTransform);
                tStatus.screenSpaceBounds = bounds.Item1;
                tStatus.screenSpaceZOffset = bounds.Item2;
                tStatus.worldSpaceBounds = bounds.Item3;
            }

        }

        private RecordedGameObjectState GetStateForTransformObject(TransformStatus tStatus)
        {
            // only process visible objects into the state
            if (tStatus.Transform != null && tStatus.screenSpaceBounds.HasValue)
            {
                var usingOldObject = _priorStates.TryGetValue(tStatus.Id, out var resultObject);

                if (!usingOldObject)
                {
                    resultObject = new RecordedGameObjectState()
                    {
                        id = tStatus.Id,
                        parentId = tStatus.ParentId,
                        type = ObjectType.Transform,
                        path = tStatus.Path,
                        normalizedPath = tStatus.NormalizedPath,
                        tag = tStatus.Tag,
                        layer = tStatus.LayerName,
                        scene = tStatus.Scene,
                        componentDataProviders = new List<IComponentDataProvider>()
                        {
                            new TransformComponentDataProvider()
                            {
                                Transform = tStatus.Transform
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
                    var newState = GetStateForTransformObject(tStatus);
                    if (newState != null)
                    {
                        _newStates[tStatus.Id] = newState;
                    }
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

                            var tStatus = TransformStatus.GetOrCreateTransformStatus(parentTransform);
                            var resultObject = GetStateForTransformObject(tStatus);

                            // don't update the _newStates dictionary while iterating
                            _fillInStates.Add(resultObject);

                            parentTransform = parentTransform.parent;
                            parentId = resultObject.parentId;
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
