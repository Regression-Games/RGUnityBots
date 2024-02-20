using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Component = UnityEngine.Component;

namespace StateRecorder
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RenderableGameObjectState
    {
        public int id;

        public string path;
        public string scene;
        public string tag;
        public string layer;

        public Bounds screenSpaceBounds;

        public Vector3 position;
        public Quaternion rotation;

        public List<RigidbodyState> rigidbodies;

        public List<ColliderState> colliders;

        public List<BehaviourState> behaviours;
        public Bounds? worldSpaceBounds;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class BehaviourState
    {
        public string name;
        public string path;
        public Behaviour state;

        public override string ToString()
        {
            return name;
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ColliderState
    {
        public string path;
        public Bounds bounds;
        public bool isTrigger;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RigidbodyState
    {
        public string path;

        public Vector3 position;

        // for 3D this is rotation on Z axis
        public Quaternion rotation;
        public Vector3 velocity;
        public float mass;
        public float drag;
        public float angularDrag;
        public bool useGravity;
        public bool isKinematic;
    }

    public class TransformStatus
    {
        public bool? HasKeyTypes;
        public bool? IsTopLevel;
        public string Path;
    }

    public class InGameObjectFinder : MonoBehaviour
    {
        private static InGameObjectFinder _this;

        [FormerlySerializedAs("CollapseRenderersIntoTopLevelGameObject")]
        [Tooltip(
            "Collapse all renderers into their top level gameObject. If a gameObject hierarchy exists that has colliders/renderers/animators/etc at multiple levels, they will all be represented by a single entry in the state.  This defaults to True as it is normally desired to see each player, car, building, etc as a single entry in the state.  However, it can be useful to set this to false in cases where you want to validate individual render bounds, colliders, rigibodies, etc on individual armatures, weapons, or other components that are children in the hierarchy.")]
        public bool collapseRenderersIntoTopLevelGameObject = true;

        [FormerlySerializedAs("IncludeOnlyOnCameraObjects")]
        [Tooltip("Include only objects at are within the render bounds of the main camera view.")]
        public bool includeOnlyOnCameraObjects = true;

        [FormerlySerializedAs("CollectStateFromBehaviours")]
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

        private string GetUniqueTransformPath(Transform theTransform)
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
                    // add our result to the cache
                    _transformsIveSeen[theTransform] = new TransformStatus
                    {
                        Path = tPath
                    };
                }
            }

            return tPath;
        }

        private BehaviourState GetStateForBehaviour(Behaviour behaviour)
        {
            var type = behaviour.GetType();
            return new BehaviourState
            {
                path = GetUniqueTransformPath(behaviour.transform),
                name = type.FullName,
                state = collectStateFromBehaviours ? behaviour : null
            };
        }

        private RenderableGameObjectState CreateStateForTransform(int screenWidth, int screenHeight, Transform t)
        {
            var gameObjectPath = GetUniqueTransformPath(t);

            // find the full bounds of the statefulGameObject
            var statefulGameObject = t.gameObject;
            var renderers = statefulGameObject.GetComponentsInChildren<Renderer>();

            var worldSpaceBounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; ++i)
            {
                worldSpaceBounds.Encapsulate(renderers[i].bounds);
            }

            var onCamera = true;

            // convert world space to screen space
            var c = worldSpaceBounds.center;
            var e = worldSpaceBounds.extents;

            Vector3[] worldCorners =
            {
                new(c.x + e.x, c.y + e.y, c.z + e.z),
                new(c.x + e.x, c.y + e.y, c.z - e.z),
                new(c.x + e.x, c.y - e.y, c.z + e.z),
                new(c.x + e.x, c.y - e.y, c.z - e.z),
                new(c.x - e.x, c.y + e.y, c.z + e.z),
                new(c.x - e.x, c.y + e.y, c.z - e.z),
                new(c.x - e.x, c.y - e.y, c.z + e.z),
                new(c.x - e.x, c.y - e.y, c.z - e.z)
            };

            var screenSpaceObjectCorners =
                worldCorners.Select(corner => Camera.main.WorldToScreenPoint(corner));

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;

            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;


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

                if (includeOnlyOnCameraObjects
                    && (
                        ((minY < 0 || minY > screenHeight - 1)
                         && (maxY < 0 || maxY > screenHeight - 1)
                        ) ||
                        ((minX < 0 || minX > screenWidth - 1)
                         && (maxX < 0 || maxX > screenWidth - 1)
                        )
                    ))
                {
                    // not in camera.. stop iterating
                    onCamera = false;
                    break;
                }
            }


            if (onCamera)
            {
                var size = new Vector3(maxX - minX, maxY - minY);
                var center = new Vector3(minX + (maxX - minX) / 2, minY + (maxY - minY) / 2);
                var screenSpaceBounds = new Bounds(center, size);

                var behaviours = statefulGameObject.GetComponentsInChildren<MonoBehaviour>()
                    .Select(GetStateForBehaviour)
                    .ToList();

                var collidersState = new List<ColliderState>();
                var colliders = statefulGameObject.GetComponentsInChildren<Collider>();
                if (colliders.Length > 0)
                {
                    foreach (var colliderEntry in colliders)
                    {
                        collidersState.Add(new ColliderState
                        {
                            path = GetUniqueTransformPath(colliderEntry.transform),
                            bounds = colliderEntry.bounds,
                            isTrigger = colliderEntry.isTrigger
                        });
                    }
                }
                else
                {
                    var colliders2D = statefulGameObject.GetComponentsInChildren<Collider2D>();
                    foreach (var colliderEntry in colliders2D)
                    {
                        collidersState.Add(new ColliderState
                            {
                                path = GetUniqueTransformPath(colliderEntry.transform),
                                bounds = colliderEntry.bounds,
                                isTrigger = colliderEntry.isTrigger
                            }
                        );
                    }
                }

                List<RigidbodyState> rigidbodiesState = new();
                var myRigidbodies = statefulGameObject.GetComponentsInChildren<Rigidbody>();
                if (myRigidbodies.Length > 0)
                {
                    foreach (var myRigidbody in myRigidbodies)
                    {
                        rigidbodiesState.Add(new RigidbodyState
                            {
                                path = GetUniqueTransformPath(myRigidbody.transform),
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
                }
                else
                {
                    var myRigidbodies2D = statefulGameObject.GetComponentsInChildren<Rigidbody2D>();
                    foreach (var myRigidbody in myRigidbodies2D)
                    {
                        rigidbodiesState.Add(new RigidbodyState
                            {
                                path = GetUniqueTransformPath(myRigidbody.transform),
                                position = myRigidbody.position,
                                rotation = Quaternion.Euler(0, 0, myRigidbody.rotation),
                                velocity = myRigidbody.velocity
                            }
                        );
                    }
                }

                return new RenderableGameObjectState
                {
                    id = statefulGameObject.transform.GetInstanceID(),
                    path = gameObjectPath,
                    screenSpaceBounds = screenSpaceBounds,
                    worldSpaceBounds = worldSpaceBounds,
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

            return null;
        }


        public List<RenderableGameObjectState> GetStateForCurrentFrame()
        {
            var resultList = new List<RenderableGameObjectState>();

            //find any gameObject with a renderer or canvas renderer (rect transform)
            var statefulUIObjects =
                FindObjectsByType<CanvasRenderer>(FindObjectsSortMode.None).Select(r => r.gameObject);

            var screenSpaceCorners = new Vector3[4];
            foreach (var statefulUIObject in statefulUIObjects)
            {
                Button b;
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
                        var size = screenSpaceCorners[2] - screenSpaceCorners[0];
                        var center = screenSpaceCorners[0] + (screenSpaceCorners[2] - screenSpaceCorners[0]) / 2;
                        var screenSpaceBounds = new Bounds(center, size);

                        for (var i = 1; i < rectTransforms.Length; ++i)
                        {
                            rectTransforms[i].GetWorldCorners(screenSpaceCorners);
                            screenSpaceBounds.Encapsulate(screenSpaceCorners[0]);
                            screenSpaceBounds.Encapsulate(screenSpaceCorners[2]);
                        }

                        var gameObjectPath = GetUniqueTransformPath(statefulUIObject.transform);
                        var behaviours = statefulUIObject.GetComponents<Behaviour>()
                            .Select(GetStateForBehaviour)
                            .ToList();

                        resultList.Add(new RenderableGameObjectState
                        {
                            id = statefulUIObject.transform.GetInstanceID(),
                            path = gameObjectPath,
                            screenSpaceBounds = screenSpaceBounds,
                            position = statefulUIObject.transform.position,
                            rotation = statefulUIObject.transform.rotation,
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

            // find everything with a renderer.. then select the last parent walking up the tree that has
            // one of the key types.. in most cases that should be the correct 'parent' game object

            var transformsForThisFrame = new HashSet<Transform>();
            var transformsIveSeenThisFrame = new HashSet<Transform>();

            // we walk all the way to the root and record which ones had key types to find the 'parent'
            var rendererObjects = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in rendererObjects)
            {
                var theTransform = r.transform;
                transformsIveSeenThisFrame.Add(theTransform);

                if (!collapseRenderersIntoTopLevelGameObject)
                {
                    transformsForThisFrame.Add(theTransform);
                }

                if (_transformsIveSeen.TryGetValue(theTransform, out var tStatus))
                {
                    tStatus.HasKeyTypes = true;
                }
                else
                {
                    _transformsIveSeen[theTransform] = new TransformStatus
                    {
                        HasKeyTypes = true
                    };
                }

                var maybeTopLevel = theTransform;

                // find any parents we need to evaluate
                var nextParent = theTransform.parent;

                // go until the root of the tree
                while (nextParent != null)
                {
                    if (transformsIveSeenThisFrame.Contains(nextParent))
                    {
                        maybeTopLevel = null;
                        // we already went up this parent tree.. stop
                        break;
                    }

                    transformsIveSeenThisFrame.Add(nextParent);

                    bool parentHasKeyTypes;
                    if (_transformsIveSeen.TryGetValue(nextParent, out var nextParentStatus) &&
                        nextParentStatus.HasKeyTypes != null)
                    {
                        parentHasKeyTypes = nextParentStatus.HasKeyTypes.Value;
                    }
                    else
                    {
                        parentHasKeyTypes = nextParent.GetComponentsInParent<Component>()
                            .FirstOrDefault(pc =>
                                pc is Renderer or Collider or Collider2D or Rigidbody or Rigidbody2D or Animator
                                    or ParticleSystem
                            ) != null;
                    }

                    if (nextParentStatus != null)
                    {
                        nextParentStatus.HasKeyTypes = parentHasKeyTypes;
                    }
                    else
                    {
                        _transformsIveSeen[nextParent] = new TransformStatus
                        {
                            HasKeyTypes = parentHasKeyTypes
                        };
                    }

                    if (parentHasKeyTypes)
                    {
                        // set the old maybeTopLevel to false
                        // set the top level parent we found on that path
                        _transformsIveSeen[maybeTopLevel].IsTopLevel = false;

                        // track the new one
                        maybeTopLevel = nextParent;

                        if (!collapseRenderersIntoTopLevelGameObject)
                        {
                            transformsForThisFrame.Add(nextParent);
                        }
                    }

                    nextParent = nextParent.parent;
                }

                // set the top level parent we found on that path
                if (maybeTopLevel != null)
                {
                    _transformsIveSeen[maybeTopLevel].IsTopLevel = true;
                    if (collapseRenderersIntoTopLevelGameObject)
                    {
                        transformsForThisFrame.Add(maybeTopLevel);
                    }
                }
            }


            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            foreach (var statefulTransform in transformsForThisFrame)
            {
                if (statefulTransform != null)
                {
                    var stateEntry = CreateStateForTransform(screenWidth, screenHeight, statefulTransform);
                    // depending on the include only on camera setting, this object may be null
                    if (stateEntry != null)
                    {
                        resultList.Add(stateEntry);
                    }
                }
            }

            return resultList;
        }
    }
}