using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder
{
    public class MouseInputActionObserver : MonoBehaviour
    {
        private MouseInputActionData _priorMouseState;

        private readonly List<string> _clickedObjectNormalizedPaths = new(100);

        public void ObserveMouse(IEnumerable<ObjectStatus> statefulObjects)
        {
            var pointer = Pointer.current;
            if (pointer != null)
            {
                var mousePosition = pointer.position.ReadValue();
                var newMouseState = GetCurrentMouseState(mousePosition);
                if (newMouseState != null)
                {
                    if (_priorMouseState == null && newMouseState.IsButtonClicked || _priorMouseState != null && !_priorMouseState.ButtonStatesEqual(newMouseState))
                    {
                        // get the z depth sorted clicked on objects
                        var clickedOnObjects = FindObjectsAtPosition(newMouseState.position, statefulObjects);

                        _clickedObjectNormalizedPaths.Clear();

                        // the objects found are already in order, but we do need to see if the topmost thing clicked had a worldPosition
                        if (clickedOnObjects.Count > 0)
                        {
                            if (clickedOnObjects[0].worldSpaceBounds != null && !newMouseState.worldPosition.HasValue)
                            {
                                newMouseState.worldPosition = clickedOnObjects[0].worldSpaceCoordinatesForMousePoint;
                            }
                        }

                        foreach (var clickedTransformStatus in clickedOnObjects)
                        {
                            _clickedObjectNormalizedPaths.Add(clickedTransformStatus.NormalizedPath);
                        }

                        // order by zDepth - this should keep UI elements that were already sorted in the same relative positions as they have exactly 0f zDepths
                        newMouseState.clickedObjectNormalizedPaths = _clickedObjectNormalizedPaths.Distinct().ToArray();
                        _mouseInputActions.Enqueue(newMouseState);
                    }
                    else if (_priorMouseState?.PositionsEqual(newMouseState) != true)
                    {
                        _mouseInputActions.Enqueue(newMouseState);
                    }

                    _priorMouseState = newMouseState;
                }
            }
        }

        private MouseInputActionData GetCurrentMouseState(Vector2? position = null)
        {
            var pointer = Pointer.current;
            if (pointer != null)
            {
                position ??= pointer.position.ReadValue();

                if (pointer is Mouse mouse)
                {
                    return new MouseInputActionData()
                    {
                        startTime = Time.unscaledTimeAsDouble,
                        position = new Vector2Int((int)position.Value.x, (int)position.Value.y),
                        leftButton = mouse.leftButton.isPressed,
                        middleButton = mouse.middleButton.isPressed,
                        rightButton = mouse.rightButton.isPressed,
                        forwardButton = mouse.forwardButton.isPressed,
                        backButton = mouse.backButton.isPressed,
                        scroll = mouse.scroll.ReadValue(),
                        clickedObjectNormalizedPaths = Array.Empty<string>(),
                        worldPosition = null,
                        screenSize = new Vector2Int(Screen.width, Screen.height)
                    };
                }

                // touch/pen pointer
                return new MouseInputActionData()
                {
                    startTime = Time.unscaledTimeAsDouble,
                    position = new Vector2Int((int)position.Value.x, (int)position.Value.y),
                    leftButton = pointer.press.isPressed,
                    middleButton = false,
                    rightButton = false,
                    forwardButton = false,
                    backButton = false,
                    scroll = Vector2.zero,
                    clickedObjectNormalizedPaths = Array.Empty<string>(),
                    worldPosition = null,
                    screenSize = new Vector2Int(Screen.width, Screen.height)
                };

            }

            return null;
        }

        private readonly ConcurrentQueue<MouseInputActionData> _mouseInputActions = new();

        public void ClearBuffer()
        {
            _priorMouseState = null;
            _mouseInputActions.Clear();
        }

        /**
         * <summary>Flush the latest captured mouse movements</summary>
         * <param name="ignoreLastClick">Ignore the last click.. useful for recordings to avoid capturing the click of the stop recording button</param>
         */
        public List<MouseInputActionData> FlushInputDataBuffer(bool ignoreLastClick)
        {
            List<MouseInputActionData> result = new();
            while (_mouseInputActions.TryPeek(out var action))
            {
                _mouseInputActions.TryDequeue(out _);
                result.Add(action);

            }

            if (result.Count == 0 && _priorMouseState != null)
            {
                // or.. we asked for at least 1 mouse observation per tick
                result.Add(_priorMouseState);
            }

            // get all mouse inputs up to the last click... this is used when we stop the recording from the toolbar to avoid capturing the click on our record button on end recording
            if (ignoreLastClick)
            {
                var lastIndex = result.FindLastIndex(a => a.IsButtonClicked);
                if (lastIndex > -1)
                {
                    result = result.GetRange(0, lastIndex);
                }
            }

            return result;
        }

        /**
         * This finds all objects at the given position that a mouse click would possibly register on and returns them sorted correctly based on how that click would be handled.
         *
         * 1. UI objects based on their order from EventSystem.current.RaycastAll
         *    If a UI object includes components that are `Selectable`, then it will only be returned if one of those has `interactable` == true.  This means disabled buttons/etc won't be included.
         *
         * 2. World Space objects with colliders hit based on the distance from Camera.main to their collider hit.
         *    This properly considers colliders on parent objects in the hierarchy being hit as it is common for a parent to have the collider and child objects to just be renderers.
         */
        public static List<ObjectStatus> FindObjectsAtPosition(Vector2 position, IEnumerable<ObjectStatus> statefulObjects)
        {
            // make sure screen space position Z is around 0
            var vec3Position = new Vector3(position.x, position.y, 0);
            List<ObjectStatus> result = new();
            var mainCamera = Camera.main;
            Ray? ray = null;
            if (mainCamera != null)
            {
                ray = mainCamera.ScreenPointToRay(position);
            }

            var worldSpaceHits = new Dictionary<int, RaycastHit>();

            if (ray.HasValue)
            {
                // do a ray-cast all into the world and see what hits... we want to be able to sort things with colliders above those that don't have one
                var rayCastHits = Physics.RaycastAll(ray.Value);
                worldSpaceHits = rayCastHits.ToDictionary(a => a.transform.GetInstanceID(), a => a);
            }

            foreach (var recordedGameObjectState in statefulObjects)
            {
                if (recordedGameObjectState.screenSpaceBounds.HasValue)
                {
                    // make sure the z offset is actually in front of the camera
                    if (recordedGameObjectState.screenSpaceBounds.Value.Contains(vec3Position) && recordedGameObjectState.screenSpaceZOffset >= 0f)
                    {

                        // filter out to only world space objects or interactable UI objects
                        var isInteractable = true;

                        if (recordedGameObjectState is TransformStatus tStatus)
                        {
                            var theTransform = tStatus.Transform;
                            if (theTransform is RectTransform)
                            {
                                // ui object
                                var selectables = theTransform.GetComponents<Selectable>();
                                if (selectables.Length > 0)
                                {
                                    // make sure 1 is interactable
                                    isInteractable = selectables.Any(a => a.interactable);
                                }
                                // else .. wasn't selectable, but certainly still obstructs what we're clicking on so leave isInteractable = true
                            }
                        }

                        if (isInteractable)
                        {
                            if (recordedGameObjectState.worldSpaceBounds.HasValue)
                            {
                                var didHit = false;
                                if (ray.HasValue)
                                {
                                    // compute the zOffset at this point based on the ray
                                    if (recordedGameObjectState is TransformStatus transformStatus)
                                    {
                                        var collider = transformStatus.Transform.GetComponentInParent<Collider>();
                                        if (collider != null)
                                        {
                                            // let's see if it has a collider to hit
                                            var myHit = worldSpaceHits.Values.Select(a=> a as RaycastHit?).Where(a => a.Value.collider == collider).DefaultIfEmpty(null).FirstOrDefault();
                                            if (myHit.HasValue)
                                            {
                                                var hitDistance = myHit.Value.distance;
                                                if (hitDistance >= 0f)
                                                {
                                                    var hitPoint = ray.Value.GetPoint(hitDistance);
                                                    didHit = true;
                                                    recordedGameObjectState.zOffsetForMousePoint = hitDistance;
                                                    recordedGameObjectState.worldSpaceCoordinatesForMousePoint = hitPoint;
                                                    result.Add(recordedGameObjectState);
                                                }
                                            }
                                        }
                                    }

                                }
                                if(!didHit)
                                {
                                    // wasn't in front of the camera
                                    recordedGameObjectState.zOffsetForMousePoint = -1f;
                                    recordedGameObjectState.worldSpaceCoordinatesForMousePoint = null;
                                }
                            }
                            else if (recordedGameObjectState.screenSpaceZOffset >= 0f)
                            {
                                // screen space object
                                recordedGameObjectState.zOffsetForMousePoint = 0f;
                                recordedGameObjectState.worldSpaceCoordinatesForMousePoint = null;
                                result.Add(recordedGameObjectState);
                            }
                        }
                    }
                }
            }

            // compute the UI event system hits at this point for sorting
            var eventSystemHits = new List<RaycastResult>();
            // ray-cast into the scene with the UI event system and see if this object is the first thing hit
            var pointerEventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = position
            };
            EventSystem.current.RaycastAll(pointerEventData, eventSystemHits);

            // store their ordering value.. RaycastAll is already sorted in the correct order for us by Unity.. don't mess with their ordering sort or you will get incorrect obstructions :/
            var uiHitMapping = eventSystemHits.Select((a,i) => (a,i)).ToDictionary(ai => (long)ai.a.gameObject.transform.GetInstanceID(), ai => ai.i);

            // sort with lower z-offsets first so we have UI things on top of game object things
            // if both elements are from the UI.. then sort by the event system ray hits
            // for world space objects.. sort those with ray cast hits before those without
            result.Sort((a, b) =>
            {
                if (a.worldSpaceBounds == null && b.worldSpaceBounds == null)
                {
                    int aHitOrder = -1;
                    if (a is TransformStatus at)
                    {
                        if (uiHitMapping.TryGetValue(at.Id, out var value))
                        {
                            aHitOrder = value;
                        }
                    }

                    int bHitOrder = -1;
                    if (b is TransformStatus bt)
                    {
                        if (uiHitMapping.TryGetValue(bt.Id, out var value))
                        {
                            bHitOrder = value;
                        }
                    }

                    if (aHitOrder >= 0)
                    {
                        if (bHitOrder >= 0)
                        {
                            return aHitOrder - bHitOrder;
                        }

                        // aDidHit.. but not b, put a in front
                        return -1;
                    }

                    if (bHitOrder >= 0)
                    {
                        //bDidHit.. but not a, put b in front
                        return 1;
                    }

                    return 0;
                }

                RaycastHit? aWorldHitData = null;
                if (a is TransformStatus awt)
                {
                    aWorldHitData = IsHitOnParent(awt.Transform, worldSpaceHits);
                }
                RaycastHit? bWorldHitData = null;
                if (b is TransformStatus bwt)
                {
                    bWorldHitData = IsHitOnParent(bwt.Transform, worldSpaceHits);
                }

                if (aWorldHitData.HasValue)
                {
                    if (bWorldHitData.HasValue)
                    {
                        if (aWorldHitData.Value.distance < bWorldHitData.Value.distance)
                        {
                            return -1;
                        }

                        if (aWorldHitData.Value.distance > bWorldHitData.Value.distance)
                        {
                            return 1;
                        }

                        return 0;
                    }

                    // aWorldHitData.. but not b
                    if (b.worldSpaceBounds == null)
                    {
                        // b is UI overlay, leave it in front
                        return 1;
                    }
                    // else put a in front
                    return -1;
                }

                if (bWorldHitData.HasValue)
                {
                    //bWorldHitData.. but not a
                    if (a.worldSpaceBounds == null)
                    {
                        // a is UI overlay, leave it in front
                        return -1;
                    }
                    // else put b in front
                    return 1;
                }

                // else no collider hit and not UI, so sort by the zOffset of the renderer itself (remember that -1f is used to represent 'behind the camera' so we have to consider that in our comparisons)
                if (a.zOffsetForMousePoint >= 0f && a.zOffsetForMousePoint < b.zOffsetForMousePoint)
                {
                    return -1;
                }

                if (b.zOffsetForMousePoint >= 0f && a.zOffsetForMousePoint > b.zOffsetForMousePoint)
                {
                    return 1;
                }

                return 0;
            });
            return result;
        }

        private static RaycastHit? IsHitOnParent(Transform transform, Dictionary<int, RaycastHit> worldSpaceHits)
        {
            foreach (var worldSpaceHit in worldSpaceHits)
            {
                // for each hit, walk up the hierarchy to see if a collider on myself or my parent was hit
                while (transform != null)
                {
                    var id = transform.GetInstanceID();
                    if (worldSpaceHits.TryGetValue(id, out var hitInfo))
                    {
                        return hitInfo;
                    }

                    transform = transform.parent;
                }

            }

            return null;
        }
    }
}
