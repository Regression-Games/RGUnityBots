using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder
{
    public class MouseInputActionObserver : MonoBehaviour
    {
        private MouseInputActionData _priorMouseState;

        private readonly Dictionary<string, float> _clickedObjectNormalizedPaths = new(100);

        private readonly Comparer<RaycastHit> _mouseHitComparer = Comparer<RaycastHit>.Create(
            (x1, x2) =>
            {
                var d = x1.distance - x2.distance;
                return (d > 0f) ? 1 : (d < 0f) ? -1 : 0;
            } );

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
                        Vector3? worldPosition = null;
                        var clickedOnObjects = FindObjectsAtPosition(newMouseState.position, statefulObjects, out var maxZDepth);

                        var didMouseRayHit = false;

                        RaycastHit hitInfo = default;

                        var mainCamera = Camera.main;
                        if (mainCamera != null)
                        {
                            var ray = mainCamera.ScreenPointToRay(mousePosition);
                            didMouseRayHit = Physics.Raycast(ray, out hitInfo); // make sure we go deep enough to hit the collider on that object.. we hope
                        }

                        _clickedObjectNormalizedPaths.Clear();

                        if (didMouseRayHit)
                        {
                            // we hit a collider and can compute the world position clicked
                            // we can also then sort the results based on said collision hit so that the best objects are in the front
                            foreach (var clickedTransformStatus in clickedOnObjects)
                            {
                                var zDepth = clickedTransformStatus.screenSpaceZOffset;
                                if (clickedTransformStatus.worldSpaceBounds != null)
                                {
                                    var rayHit = hitInfo;
                                    try
                                    {
                                        var rayHitObjectStatus = TransformStatus.GetOrCreateTransformStatus(rayHit.transform);
                                        // this handles cases like in bossroom where the collider is on EntranceStaticNetworkObjects/BreakablePot , but the renderer is on EntranceStaticNetworkObjects/BreakablePot/pot
                                        if (clickedTransformStatus.NormalizedPath.StartsWith(rayHitObjectStatus.NormalizedPath))
                                        {
                                            worldPosition = rayHit.point;
                                            // we do some fuzzy math here... we know for sure we hit this object first, but ... if we just use its hit distance that may not sort
                                            // correctly with the zDepth of other 3d objects.. there could be a giant box and we're clicking on something just on the back top edge of it
                                            // but the front of the box is wayy closer to us... but.. isn't obstructing that click... so for this optimization
                                            // we know ui overlay things are at depth 0f... so we put this just behind that, but should be in front of anything else
                                            zDepth = 0.00001f; // Logic with these numbers is also in TransformObjectFinder.. this value needs to be CLOSER from the camera than the one in TransformObjectFinder
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        RGDebug.LogException(e, "Exception handling raycast hits for in game object click");
                                    }
                                }

                                // track the shortest z depth
                                if (!_clickedObjectNormalizedPaths.TryGetValue(clickedTransformStatus.NormalizedPath, out var depth))
                                {
                                    _clickedObjectNormalizedPaths[clickedTransformStatus.NormalizedPath] = zDepth;
                                }
                                else
                                {
                                    if (zDepth < depth)
                                    {
                                        _clickedObjectNormalizedPaths[clickedTransformStatus.NormalizedPath] = zDepth;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // without a collider hit, we can't set a worldPosition
                            foreach (var recordedGameObjectState in clickedOnObjects)
                            {
                                // track the shortest z depth
                                if (!_clickedObjectNormalizedPaths.TryGetValue(recordedGameObjectState.NormalizedPath, out var depth))
                                {
                                    _clickedObjectNormalizedPaths[recordedGameObjectState.NormalizedPath] = recordedGameObjectState.screenSpaceZOffset;
                                }
                                else
                                {
                                    if (recordedGameObjectState.screenSpaceZOffset < depth)
                                    {
                                        _clickedObjectNormalizedPaths[recordedGameObjectState.NormalizedPath] = recordedGameObjectState.screenSpaceZOffset;
                                    }
                                }
                            }
                        }

                        newMouseState.worldPosition = worldPosition;
                        // order by zDepth - this should keep UI elements that were already sorted in the same relative positions as they have exactly 0f zDepths
                        newMouseState.clickedObjectNormalizedPaths = _clickedObjectNormalizedPaths.OrderBy(kvp => kvp.Value).Select(a => a.Key).ToArray();
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

        public MouseInputActionData GetCurrentMouseState(Vector2? position = null)
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

                // when minimizing output, only capture the inputs 1 before 1 after and during mouse clicks or holds... the normalized paths is populated for both clicks and unclicks so is useful for determining the unclick data

                if (lastAction != null && (!minimizeOutput || action.IsButtonClicked || action.clickedObjectNormalizedPaths.Length > 0 || lastAction?.IsButtonClicked == true|| lastAction?.clickedObjectNormalizedPaths.Length > 0))
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
                            if (recordedGameObjectState.worldSpaceBounds != null)
                            {
                                if (recordedGameObjectState.screenSpaceZOffset > maxZDepth)
                                {
                                    maxZDepth = recordedGameObjectState.screenSpaceZOffset;
                                }
                            }
                            result.Add(recordedGameObjectState);
                        }

                    }
                }
            }

            // sort with lower z-offsets first so we have UI things on top of game object things
            // if both elements are from the UI.. then sort by smallest bounding area
            result.Sort((a, b) =>
            {
                if (a.worldSpaceBounds == null && b.worldSpaceBounds == null)
                {
                    // 2 UI objects, sort by smallest render bounds
                    var aExtents = a.screenSpaceBounds.Value.extents;
                    var bExtents = b.screenSpaceBounds.Value.extents;

                    var aAreaComparison = aExtents.x * aExtents.y;
                    var bAreaComparison = bExtents.x * bExtents.y;

                    if (aAreaComparison < bAreaComparison)
                    {
                        return -1;
                    }

                    // if a isn't smaller then we don't much care between == vs > as in floating point land.. == is so unlikely as for us to not worry about 'stable' sort for this case
                    return 1;
                }

                if (a.screenSpaceZOffset < b.screenSpaceZOffset)
                {
                    return -1;
                }

                if (a.screenSpaceZOffset > b.screenSpaceZOffset)
                {
                    return 1;
                }

                return 0;
            });
            return result;
        }
    }
}
