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
            // uses reflection as the ECS package is an add-on that may no exist in the runtime
            Type t = Type.GetType("RegressionGames.StateRecorder.ECS.EntityObjectFinder, RegressionGames_ECS, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
            if (t == null)
            {
                RGDebug.LogInfo("Regression Games ECS Package not found, support for ECS won't be loaded");
            }
            else
            {
                var entityFinder = GetComponent(t);
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
        private static readonly List<CanvasGroup> CanvasGroupsList = new(100);
        private readonly HashSet<Transform> _transformsForThisFrame = new (1000);

        private static readonly Vector3[] WorldSpaceCorners = new Vector3[4];

        private int _objectFrameNumber = -1;
        private int _stateFrameNumber = -1;

        // pre-allocate a big list we can re-use
        private Dictionary<long,RecordedGameObjectState> _priorStates = new (1000);
        private Dictionary<long,RecordedGameObjectState> _newStates = new(1000);
        private readonly Dictionary<long,RecordedGameObjectState> _fillInStates = new (1000);

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

        public static (float,float,float,float,float,float)? ConvertWorldSpaceBoundsToScreenSpace(float minWorldX, float maxWorldX, float minWorldY, float maxWorldY, float minWorldZ, float maxWorldZ)
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
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
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

                return (minX, maxX, minY, maxY, minZ, maxZ);
            }

            return null;
        }

        public static Bounds? ConvertWorldSpaceBoundsToScreenSpace(Bounds worldSpaceBounds)
        {
            var values = ConvertWorldSpaceBoundsToScreenSpace(worldSpaceBounds.min.x, worldSpaceBounds.max.x, worldSpaceBounds.min.y, worldSpaceBounds.max.y, worldSpaceBounds.min.z, worldSpaceBounds.max.z);
            if (values.HasValue)
            {
                var extents = new Vector3((values.Value.Item2 - values.Value.Item1) / 2, (values.Value.Item4 - values.Value.Item3) / 2, (values.Value.Item6 - values.Value.Item5) / 2);
                return new Bounds(new Vector3(values.Value.Item1 + extents.x, values.Value.Item3 + extents.y, values.Value.Item5 + extents.z), extents );
            }

            return null;
        }

        // ReSharper disable once MemberCanBePrivate.Global - Keep this method public, while not called from this package module, it is called from some of our extension packages
        public static (Bounds?, float, Bounds?) SelectBoundsForTransform(Camera mainCamera, int screenWidth, int screenHeight, Transform theTransform)
        {
            if (theTransform.TryGetComponent(out CanvasRenderer _))
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
                    CanvasGroupsList.Clear();
                    // this API already handles reversing the list so that the deepest tree element is first so we can walk up the tree as desired
                    theTransform.GetComponentsInParent(false,CanvasGroupsList);
                    var cgEnabled = true;
                    foreach (var canvasGroup in CanvasGroupsList)
                    {
                        if (!cgEnabled)
                        {
                            break;
                        }

                        cgEnabled &= canvasGroup.enabled && (canvasGroup.blocksRaycasts || canvasGroup.interactable) && (canvasGroup.alpha > 0.01f); // 0ish float comparisons ... lovely but necessary
                        if (canvasGroup.ignoreParentGroups)
                        {
                            break;
                        }
                    }

                    if (cgEnabled)
                    {
                        var isWorldSpace = canvas.renderMode == RenderMode.WorldSpace;
                        RectTransformsList.Clear();
                        theTransform.GetComponentsInChildren(RectTransformsList);
                        var rectTransformsListLength = RectTransformsList.Count;

                        if (rectTransformsListLength > 0)
                        {
                            var minWorldX = float.MaxValue;
                            var maxWorldX = float.MinValue;

                            var minWorldY = float.MaxValue;
                            var maxWorldY = float.MinValue;

                            var minWorldZ = float.MaxValue;
                            var maxWorldZ = float.MinValue;

                            RectTransformsList[0].GetWorldCorners(WorldSpaceCorners);
                            var min = WorldSpaceCorners[0];
                            var max = WorldSpaceCorners[2];
                            if (isWorldSpace)
                            {
                                minWorldX = WorldSpaceCorners[0].x;
                                minWorldY = WorldSpaceCorners[0].y;
                                minWorldZ = WorldSpaceCorners[0].z;
                                maxWorldX = WorldSpaceCorners[2].x;
                                maxWorldY = WorldSpaceCorners[2].y;
                                maxWorldZ = WorldSpaceCorners[2].z;
                            }

                            for (var i = 1; i < rectTransformsListLength; ++i)
                            {
                                RectTransformsList[i].GetWorldCorners(WorldSpaceCorners);
                                var nextMin = WorldSpaceCorners[0];
                                var nextMax = WorldSpaceCorners[2];

                                // Vector3.min and Vector3.max re-allocate new vectors on each call, avoid using them
                                min.x = Mathf.Min(min.x, nextMin.x);
                                min.y = Mathf.Min(min.y, nextMin.y);

                                max.x = Mathf.Max(max.x, nextMax.x);
                                max.y = Mathf.Max(max.y, nextMax.y);

                                if (isWorldSpace)
                                {
                                    minWorldX = Mathf.Min(minWorldX, nextMin.x);
                                    minWorldY = Mathf.Min(minWorldY, nextMin.y);
                                    minWorldZ = Mathf.Min(minWorldZ, nextMin.z);
                                    maxWorldX = Mathf.Max(maxWorldX, nextMax.x);
                                    maxWorldY = Mathf.Max(maxWorldY, nextMax.y);
                                    maxWorldZ = Mathf.Max(maxWorldZ, nextMax.z);
                                }
                            }

                            if (mainCamera != null)
                            {
                                if (isWorldSpace)
                                {
                                    min = mainCamera.WorldToScreenPoint(min);
                                    max = mainCamera.WorldToScreenPoint(max);
                                }
                                else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                                {
                                    var cameraToUse = canvas.worldCamera == null ? mainCamera : canvas.worldCamera;
                                    min = cameraToUse.WorldToScreenPoint(min);
                                    max = cameraToUse.WorldToScreenPoint(max);
                                }
                                else // if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                                {
                                    // already set.. use world space corners
                                }
                            }

                            var onCamera = true;
                            if (isWorldSpace || canvas.renderMode == RenderMode.ScreenSpaceCamera)
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

                                var ssBounds = new Bounds(center, size);

                                if (isWorldSpace)
                                {
                                    var worldSize = new Vector3((maxWorldX - minWorldX), (maxWorldY - minWorldY), (maxWorldZ - minWorldZ));
                                    var worldCenter = new Vector3(minWorldX + worldSize.x, minWorldY + worldSize.y / 2, minWorldZ + worldSize.z / 2);


                                    var zOffset = Math.Min(min.z, max.z);
                                    // if the zOffset is negative, our camera is inside the bounding volume for this object, choose the farthest z instead.. this happens for particle effects like fx_ImpSpawner in bossroom
                                    // we still need to track down why this thing has HUGE screenspace bounds compared to what is shown in the editor
                                    if (zOffset < 0.0f)
                                    {
                                        zOffset = Math.Max(min.z, max.z);
                                    }
                                    // don't let world space objects be <= 0.0f as they would appear on top of true ui overlay objects in our processing
                                    if (zOffset <= 0f)
                                    {
                                        zOffset = 0.0001f; // Logic with these numbers is also in MouseInputActionObserver.. this value needs to be further from the camera than the one in MouseInputActionObserver
                                    }


                                    // get the screen point values for the world max / min and find the screen space z offset closest the camera
                                    return (ssBounds, zOffset, new Bounds(worldCenter, worldSize));

                                }

                                return (ssBounds, 0f, null);
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
                var statefulGameObjectTransform = theTransform.transform;

                var transformName = ""+statefulGameObjectTransform.name; // used for debugging object bounds and easily seeing the name in the debugger.. don't remove
                RendererQueryList.Clear();
                statefulGameObjectTransform.GetComponentsInChildren(RendererQueryList);

                var minWorldX = float.MaxValue;
                var maxWorldX = float.MinValue;

                var minWorldY = float.MaxValue;
                var maxWorldY = float.MinValue;

                var minWorldZ = float.MaxValue;
                var maxWorldZ = float.MinValue;

                var rendererListLength = RendererQueryList.Count;
                for (var i = 0; i < rendererListLength; i++)
                {
                    var nextRenderer = RendererQueryList[i];
                    // exclude objects outside the view frustrum of ANY camera... this can be a problem in the editor scene view though as that counts as a camera
                    if (nextRenderer.isVisible && nextRenderer.transform.GetComponentInParent<RGExcludeFromState>() == null)
                    {
                        var theBounds = nextRenderer.bounds;

                        // faster to do this ourself vs using the built in bounds min/max methods
                        // also avoids vector math and allocation
                        var center = theBounds.center;
                        var extents = theBounds.extents;

                        var minX = center.x - extents.x;
                        if (minX < minWorldX)
                        {
                            minWorldX = minX;
                        }

                        var maxX = center.x + extents.x;
                        if (maxX > maxWorldX)
                        {
                            maxWorldX = maxX;
                        }

                        var minY = center.y - extents.y;
                        if (minY < minWorldY)
                        {
                            minWorldY = minY;
                        }

                        var maxY = center.y + extents.y;
                        if (maxY > maxWorldY)
                        {
                            maxWorldY = maxY;
                        }

                        var minZ = center.z - extents.z;
                        if (minZ < minWorldZ)
                        {
                            minWorldZ = minZ;
                        }

                        var maxZ = center.z + extents.z;
                        if (maxZ > maxWorldZ)
                        {
                            maxWorldZ = maxZ;
                        }
                    }
                }

                var onCamera = minWorldX < float.MaxValue;
                if (onCamera)
                {

                    var boundsValues = ConvertWorldSpaceBoundsToScreenSpace(minWorldX, maxWorldX, minWorldY, maxWorldY, minWorldZ, maxWorldZ);
                    if (!boundsValues.HasValue)
                    {
                        onCamera = false;
                    }

                    if (onCamera)
                    {
                        var xLowerLimit = 0;
                        var xUpperLimit = screenWidth;
                        var yLowerLimit = 0;
                        var yUpperLimit = screenHeight;
                        if (!(boundsValues.Value.Item1 <= xUpperLimit && boundsValues.Value.Item2 >= xLowerLimit && boundsValues.Value.Item3 <= yUpperLimit && boundsValues.Value.Item4 >= yLowerLimit))
                        {
                            // not in camera..
                            onCamera = false;
                        }
                    }

                    if (onCamera)
                    {
                        // make sure the screen space bounds has a non-zero Z size around 0
                        // we track the true z offset separately for ease of mouse selection on replay
                        var size = new Vector3((boundsValues.Value.Item2 - boundsValues.Value.Item1), (boundsValues.Value.Item4 - boundsValues.Value.Item3), 0.05f);
                        var center = new Vector3(boundsValues.Value.Item1 + size.x / 2, boundsValues.Value.Item3 + size.y / 2, 0);

                        var worldSize = new Vector3((maxWorldX - minWorldX), (maxWorldY - minWorldY), (maxWorldZ - minWorldZ));
                        var worldCenter = new Vector3(minWorldX + worldSize.x / 2, minWorldY + worldSize.y / 2, minWorldZ + worldSize.z / 2);


                        var zOffset = Math.Min(boundsValues.Value.Item5, boundsValues.Value.Item6);
                        // if the zOffset is negative, our camera is inside the bounding volume for this object, choose the farthest z instead.. this happens for particle effects like fx_ImpSpawner in bossroom
                        // we still need to track down why this thing has HUGE screenspace bounds compared to what is shown in the editor
                        if (zOffset < 0.0f)
                        {
                            zOffset = Math.Max(boundsValues.Value.Item5, boundsValues.Value.Item6);
                        }
                        // don't let world space objects be <= 0.0f as they would appear on top of true ui overlay objects in our processing
                        if (zOffset <= 0f)
                        {
                            zOffset = 0.0001f; // Logic with these numbers is also in MouseInputActionObserver.. this value needs to be further from the camera than the one in MouseInputActionObserver
                        }

                        // get the screen point values for the world max / min and find the screen space z offset closest the camera
                        return (new Bounds(center, size), zOffset, new Bounds(worldCenter, worldSize));
                    }
                }
            }

            return (null, 0f, null);
        }

        private void PopulateUITransformsForCurrentFrame()
        {
            var screenHeight = Screen.height;
            var screenWidth = Screen.width;
            var mainCamera = Camera.main;
            var canvasRenderers = FindObjectsByType(typeof(CanvasRenderer), FindObjectsSortMode.None);

            // we re-use this over and over instead of allocating multiple times
            var canvasRenderersLength = canvasRenderers.Length;
            for (var j = 0; j < canvasRenderersLength; j++)
            {
                var canvasRenderer = (CanvasRenderer)canvasRenderers[j];
                var statefulUiObjectTransform = canvasRenderer.transform;
                if (statefulUiObjectTransform != null && statefulUiObjectTransform.GetComponentInParent<RGExcludeFromState>() == null)
                {
                    var tStatus = TransformStatus.GetOrCreateTransformStatus(statefulUiObjectTransform);

                    var bounds = SelectBoundsForTransform(mainCamera, screenWidth, screenHeight, statefulUiObjectTransform);
                    tStatus.screenSpaceBounds = bounds.Item1;
                    tStatus.screenSpaceZOffset = bounds.Item2;
                    tStatus.worldSpaceBounds = bounds.Item3;

                    // only include visible UI elements
                    if (tStatus.screenSpaceBounds != null)
                    {
                        _newObjects[tStatus.Id] = tStatus;
                    }
                }
            }
        }

        /**
         * <returns>worldSpaceObjects transform status for previous and current frame, ... will have null screenSpaceBounds if off camera</returns>
         */
        private void PopulateGameObjectTransformsForCurrentFrame()
        {
            var screenHeight = Screen.height;
            var screenWidth = Screen.width;
            var mainCamera = Camera.main;
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

                var bounds = SelectBoundsForTransform(mainCamera, screenWidth, screenHeight, theTransform);
                tStatus.screenSpaceBounds = bounds.Item1;
                tStatus.screenSpaceZOffset = bounds.Item2;
                tStatus.worldSpaceBounds = bounds.Item3;

                // only include visible elements
                if (tStatus.screenSpaceBounds != null)
                {
                    _newObjects[tStatus.Id] = tStatus;
                }
            }

        }

        private RecordedGameObjectState GetStateForTransformObject(TransformStatus tStatus)
        {
            // only process visible objects into the state
            if (tStatus.Transform != null)
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
                    };
                }

                resultObject.position = tStatus.Transform.position;
                resultObject.rotation = tStatus.Transform.rotation;

                resultObject.screenSpaceZOffset = tStatus.screenSpaceZOffset;
                resultObject.screenSpaceBounds = tStatus.screenSpaceBounds;
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
                    if (statusList.TryGetValue(newStateEntry.id, out var status))
                    {
                        var theTransform = (status is TransformStatus transformStatus) ? transformStatus.Transform : null;

                        if (theTransform != null)
                        {
                            var parentTransform = theTransform.parent;
                            // go up the tree until we find something in our parent hierarchy existing..
                            // stop if we hit the top
                            while (parentId.HasValue && parentTransform != null && !_newStates.ContainsKey(parentId.Value) && !_fillInStates.ContainsKey(parentId.Value))
                            {

                                var tStatus = TransformStatus.GetOrCreateTransformStatus(parentTransform);
                                var resultObject = GetStateForTransformObject(tStatus);

                                // don't update the _newStates dictionary while iterating
                                _fillInStates[parentId.Value] = resultObject;

                                parentTransform = parentTransform.parent;
                                parentId = resultObject.parentId;
                            }
                        }
                    }
                }
            }

            foreach (var recordedGameObjectState in _fillInStates)
            {
                _newStates[recordedGameObjectState.Key] = recordedGameObjectState.Value;
            }

            return (_priorStates, _newStates);
        }

    }
}
