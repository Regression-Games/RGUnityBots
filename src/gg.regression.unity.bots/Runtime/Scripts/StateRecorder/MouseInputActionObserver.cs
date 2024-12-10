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
                        var clickedOnObjects = FindObjectsAtPosition(newMouseState.position, statefulObjects, out _);

                        _clickedObjectNormalizedPaths.Clear();

                        // the objects found are already in order, but we do need to see if there was a worldPosition clicked
                        foreach (var clickedTransformStatus in clickedOnObjects)
                        {
                            if (clickedTransformStatus.worldSpaceBounds != null && !newMouseState.worldPosition.HasValue)
                            {
                                newMouseState.worldPosition = clickedTransformStatus.worldSpaceCoordinatesForMousePoint;
                            }
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
         * <param name="minimizeOutput">Flag to leave out everything except the key movements around clicks.</param>
         */
        public List<MouseInputActionData> FlushInputDataBuffer(bool ignoreLastClick, bool minimizeOutput)
        {
            List<MouseInputActionData> result = new();
            MouseInputActionData lastAction = null;
            while (_mouseInputActions.TryPeek(out var action))
            {
                _mouseInputActions.TryDequeue(out _);

                // when minimizing output, only capture the inputs 1 before 1 after and during mouse clicks or holds... the normalized paths is populated for both clicks and un-clicks so is useful for determining the un-click data

                if (lastAction != null && (!minimizeOutput || action.IsButtonClicked || action.clickedObjectNormalizedPaths.Length > 0 || lastAction.IsButtonClicked || lastAction.clickedObjectNormalizedPaths.Length > 0))
                {
                    result.Add(lastAction);
                }

                lastAction = action;
            }

            if (lastAction != null)
            {
                result.Add(lastAction);
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

        public static List<ObjectStatus> FindObjectsAtPosition(Vector2 position, IEnumerable<ObjectStatus> statefulObjects, out float maxZDepth)
        {
            // make sure screen space position Z is around 0
            var vec3Position = new Vector3(position.x, position.y, 0);
            List<ObjectStatus> result = new();
            maxZDepth = 0f;
            var mainCamera = Camera.main;
            Ray? ray = null;
            if (mainCamera != null)
            {
                ray = mainCamera.ScreenPointToRay(position);
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
                                // make sure 1 is interactable
                                isInteractable = selectables.Any(a => a.interactable);
                            }
                        }

                        if (isInteractable)
                        {
                            if (recordedGameObjectState.worldSpaceBounds.HasValue)
                            {
                                if (ray.HasValue)
                                {
                                    // compute the zOffset at this point based on the ray
                                    if (recordedGameObjectState.worldSpaceBounds.Value.IntersectRay(ray.Value, out var zDepthHit))
                                    {
                                        if (zDepthHit >= 0f)
                                        {
                                            recordedGameObjectState.zOffsetForMousePoint = zDepthHit;
                                            recordedGameObjectState.worldSpaceCoordinatesForMousePoint = ray.Value.GetPoint(zDepthHit);
                                            if (zDepthHit > maxZDepth)
                                            {
                                                maxZDepth = zDepthHit;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // wasn't in front of the camera
                                        recordedGameObjectState.zOffsetForMousePoint = -1f;
                                        recordedGameObjectState.worldSpaceCoordinatesForMousePoint = null;
                                    }
                                }
                                else
                                {
                                    // go with the zOffset for the bounding box itself instead of at this exact point... this leads to really weird cases in 3d spaces with close objects and should be avoided
                                    // example.. a stone you click sitting on the top back corner of a cube will be 'behind' the cube because the front plane of the cube is closer, just not at that screen point
                                    // leaving this in here for now though as some sorting is still better than no sorting if we get here (have no camera)
                                    if (recordedGameObjectState.screenSpaceZOffset > maxZDepth)
                                    {
                                        maxZDepth = recordedGameObjectState.screenSpaceZOffset;
                                    }
                                }
                            }
                            else if (recordedGameObjectState.screenSpaceZOffset >= 0f)
                            {
                                recordedGameObjectState.zOffsetForMousePoint = recordedGameObjectState.screenSpaceZOffset;
                                recordedGameObjectState.worldSpaceCoordinatesForMousePoint = null;
                            }
                            result.Add(recordedGameObjectState);
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

            var uiHitMapping = eventSystemHits.ToDictionary(a => (long)a.gameObject.transform.GetInstanceID(), a => a);

            var worldSpaceHits = new Dictionary<int, RaycastHit>();

            if (ray.HasValue)
            {
                // do a raycast all into the world and see what hits... we want to be able to sort things with colliders above those that don't have one
                var raycastHits = Physics.RaycastAll(ray.Value);
                worldSpaceHits = raycastHits.ToDictionary(a => a.transform.GetInstanceID(), a => a);
            }

            // sort with lower z-offsets first so we have UI things on top of game object things
            // if both elements are from the UI.. then sort by the event system ray hits
            // for world space objects.. sort those with ray cast hits before those without
            result.Sort((a, b) =>
            {
                if (a.worldSpaceBounds == null && b.worldSpaceBounds == null)
                {
                    RaycastResult? aHitData = null;
                    if (a is TransformStatus at)
                    {
                        if (uiHitMapping.TryGetValue(at.Id, out var value))
                        {
                            aHitData = value;
                        }
                    }
                    RaycastResult? bHitData = null;
                    if (b is TransformStatus bt)
                    {
                        if (uiHitMapping.TryGetValue(bt.Id, out var value))
                        {
                            bHitData = value;
                        }
                    }

                    if (aHitData.HasValue)
                    {
                        if (bHitData.HasValue)
                        {
                            if (aHitData.Value.distance < bHitData.Value.distance)
                            {
                                return -1;
                            }

                            if (aHitData.Value.distance > bHitData.Value.distance)
                            {
                                return 1;
                            }

                            // higher depth for ui hits == closer to camera.. don't even get me started
                            return bHitData.Value.depth - aHitData.Value.depth;
                        }

                        // aDidHit.. but not b, put a in front
                        return -1;
                    }

                    if (bHitData.HasValue)
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
