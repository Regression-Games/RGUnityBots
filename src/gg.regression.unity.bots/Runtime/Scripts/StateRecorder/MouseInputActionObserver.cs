using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{
    public class MouseInputActionObserver : MonoBehaviour
    {
        private MouseInputActionData _priorMouseState;

        // limit this to 5 hits... any hit should be good enough to guess the proper screen x,y based on precise world x,y,z .. but sometimes
        // we get slight positional variances in games from run to run and need to account for edge cases where objects get hidden by other objects
        // based on a few pixel shift in relative camera position
        private readonly RaycastHit[] _cachedRaycastHits = new RaycastHit[5];

        private readonly HashSet<string> _clickedObjectNormalizedPaths = new(100);

        private readonly Comparer<RaycastHit> _mouseHitComparer = Comparer<RaycastHit>.Create(
            (x1, x2) =>
            {
                var d = x1.distance - x2.distance;
                return (d > 0) ? 1 : (d < 0) ? -1 : 0;
            } );

        public void ObserveMouse(IEnumerable<ObjectStatus> statefulObjects)
        {
            var mousePosition = Mouse.current.position.ReadValue();
            var newMouseState = GetCurrentMouseState(mousePosition);
            if (newMouseState != null)
            {
                if (_priorMouseState == null && newMouseState.IsButtonClicked || _priorMouseState != null && !_priorMouseState.ButtonStatesEqual(newMouseState))
                {
                    Vector3? worldPosition = null;
                    var clickedOnObjects = FindObjectsAtPosition(newMouseState.position, statefulObjects, out var maxZDepth);

                    var mouseRayHits = 0;

                    var ray = Camera.main.ScreenPointToRay(mousePosition);
                    mouseRayHits = Physics.RaycastNonAlloc(ray,
                        _cachedRaycastHits,
                        maxZDepth * 2f + 1f); // make sure we go deep enough to hit the collider on that object.. we hope

                    if (mouseRayHits > 0)
                    {
                        // order by distance from camera
                        Array.Sort(_cachedRaycastHits, 0, mouseRayHits, _mouseHitComparer);
                    }

                    _clickedObjectNormalizedPaths.Clear();

                    var bestIndex = int.MaxValue;
                    if (mouseRayHits > 0)
                    {
                        foreach (var clickedTransformStatus in clickedOnObjects)
                        {
                            _clickedObjectNormalizedPaths.Add(clickedTransformStatus.NormalizedPath);
                            if (clickedTransformStatus.worldSpaceBounds != null)
                            {
                                // compare to any raycast hits and pick the one closest to the camera
                                if (bestIndex > 0)
                                {
                                    for (var i = 0; i < mouseRayHits; i++)
                                    {
                                        var rayHit = _cachedRaycastHits[i];
                                        try
                                        {
                                            if (_cachedRaycastHits[i].transform.GetInstanceID() == clickedTransformStatus.Id)
                                            {
                                                if (i < bestIndex)
                                                {
                                                    worldPosition = _cachedRaycastHits[i].point;
                                                    bestIndex = i;
                                                }

                                                break;
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            RGDebug.LogException(e, "Exception handling raycast hits for in game object click");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // without a collider hit, we can't set a worldPosition
                        foreach (var recordedGameObjectState in clickedOnObjects)
                        {
                            _clickedObjectNormalizedPaths.Add(recordedGameObjectState.NormalizedPath);
                        }
                    }

                    newMouseState.worldPosition = worldPosition;
                    newMouseState.clickedObjectNormalizedPaths = _clickedObjectNormalizedPaths.ToArray();
                    _mouseInputActions.Enqueue(newMouseState);
                }
                else if (_priorMouseState?.PositionsEqual(newMouseState) != true)
                {
                    _mouseInputActions.Enqueue(newMouseState);
                }

                _priorMouseState = newMouseState;
            }
        }

        public MouseInputActionData GetCurrentMouseState(Vector2? position = null)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                position ??= mouse.position.ReadValue();
                var newMouseState = new MouseInputActionData()
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
                return newMouseState;
            }

            return null;
        }

        private readonly ConcurrentQueue<MouseInputActionData> _mouseInputActions = new();

        public void ClearBuffer()
        {
            _priorMouseState = null;
            _mouseInputActions.Clear();
        }

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

        private IEnumerable<ObjectStatus> FindObjectsAtPosition(Vector2 position, IEnumerable<ObjectStatus> statefulObjects, out float maxZDepth)
        {
            // make sure screen space position Z is around 0
            var vec3Position = new Vector3(position.x, position.y, 0);
            List<ObjectStatus> result = new();
            maxZDepth = 0f;
            var hitUIElement = false;
            foreach (var recordedGameObjectState in statefulObjects)
            {
                if (recordedGameObjectState.screenSpaceBounds.HasValue)
                {
                    if (recordedGameObjectState.screenSpaceBounds.Value.Contains(vec3Position))
                    {
                        if (!hitUIElement && recordedGameObjectState.worldSpaceBounds != null)
                        {
                            if (recordedGameObjectState.screenSpaceZOffset > maxZDepth)
                            {
                                maxZDepth = recordedGameObjectState.screenSpaceZOffset;
                            }

                            result.Add(recordedGameObjectState);
                        }
                        else
                        {
                            maxZDepth = 0f;
                            hitUIElement = true;
                            result.Add(recordedGameObjectState);
                        }
                    }
                }
            }

            if (hitUIElement)
            {
                // if we hit a UI element, ignore the in game elements
                result.RemoveAll(a => a.worldSpaceBounds != null);
            }

            return result;
        }
    }
}
