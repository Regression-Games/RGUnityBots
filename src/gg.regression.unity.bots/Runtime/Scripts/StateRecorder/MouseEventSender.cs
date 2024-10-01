using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using Object = UnityEngine.Object;

namespace RegressionGames.StateRecorder
{
    public static class MouseEventSender
    {

        private static Vector2? _lastMousePosition;

        private static IDisposable _virtualMouseEventHandler;

        private static IDisposable _realMouseEventHandler;

        // ReSharper disable once InconsistentNaming
        private static readonly RecordedGameObjectStatePathEqualityComparer _recordedGameObjectStatePathEqualityComparer = new();

        private static InputDevice _realMouse;

        private static InputDevice _virtualMouse;


        public static void Reset()
        {
            MoveMouseOffScreen();
            try
            {
                _virtualMouseEventHandler?.Dispose();
            }
            catch (Exception)
            {
                // do nothing
            }

            try
            {
                _realMouseEventHandler?.Dispose();
            }
            catch (Exception)
            {
                // do nothing
            }

            _virtualMouseEventHandler = null;
            _realMouseEventHandler = null;

            if (_virtualMouse != null)
            {
                InputSystem.RemoveDevice(_virtualMouse);
                if (_realMouse is { enabled: false })
                {
                    RGDebug.LogDebug("reset - Enabling the real mouse device for mouse event");
                    InputSystem.EnableDevice(_realMouse);
                }

                if (_realMouse != null)
                {
                    _realMouse.MakeCurrent();
                }
            }

            _virtualMouse = null;
            _realMouse = null;
        }

        public static InputDevice InitializeVirtualMouse()
        {
            _realMouse ??= InputSystem.devices.FirstOrDefault(a => a.name == "Mouse");

            _virtualMouse ??= InputSystem.devices.FirstOrDefault(a => a.name == "RGVirtualMouse");

            if (_virtualMouse == null)
            {
                _virtualMouse = InputSystem.AddDevice<Mouse>("RGVirtualMouse");
            }

            if (_virtualMouse != null)
            {
                if (!_virtualMouse.enabled)
                {
                    InputSystem.EnableDevice(_virtualMouse);
                }

                if (!_virtualMouse.canRunInBackground)
                {
                    // Forcibly allow the virtual mouse to send events while the application is backgrounded
                    // Note that if the user continues creating mouse events while outside the application, this could still interfere
                    // with the game if it is reading mouse input via the Input System.
                    var deviceFlagsField = _virtualMouse.GetType().GetField("m_DeviceFlags", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (deviceFlagsField != null)
                    {
                        int canRunInBackground = 1 << 11;
                        int canRunInBackgroundHasBeenQueried = 1 << 12;
                        var deviceFlags = (int)deviceFlagsField.GetValue(_virtualMouse);
                        deviceFlags |= canRunInBackground;
                        deviceFlags |= canRunInBackgroundHasBeenQueried;
                        deviceFlagsField.SetValue(_virtualMouse, deviceFlags);
                    }
                    else
                    {
                        RGDebug.LogWarning("Unable to set device canRunInBackground flags for virtual mouse");
                    }
                }

                _virtualMouseEventHandler ??= InputSystem.onEvent.ForDevice(_virtualMouse).Call(e =>
                {
                    var positionControl = _virtualMouse.allControls.First(a => a is Vector2Control && a.name == "position") as Vector2Control;
                    var position = positionControl.ReadValueFromEvent(e);

                    var buttonsClicked = _virtualMouse.allControls.FirstOrDefault(a =>
                        a is ButtonControl abc && abc.ReadValueFromEvent(e) > 0.1f
                    ) != null;
                    RGDebug.LogDebug("Virtual mouse event at: " + position.x + "," + position.y + "  buttonsClicked: " + buttonsClicked);

                    // need to use the static accessor here as this anonymous function's parent gameObject instance could get destroyed
                    var virtualMouseCursors = Object.FindObjectsOfType<VirtualMouseCursor>();
                    virtualMouseCursors.FirstOrDefault()?.SetPosition(position, buttonsClicked);

                    if (!buttonsClicked && _realMouse is { enabled: false })
                    {
                        RGDebug.LogDebug("Enabling the real mouse device after virtual click mouse event");
                        InputSystem.EnableDevice(_realMouse);
                    }
                });
            }

            if (_realMouseEventHandler == null)
            {
                if (_realMouse != null)
                {
                    _realMouseEventHandler = InputSystem.onEvent.ForDevice(_realMouse).Call(e =>
                    {
                        if (RGDebug.IsVerboseEnabled || _realMouse is {enabled: false})
                        {
                            var positionControl = _realMouse.allControls.First(a => a is Vector2Control && a.name == "position") as Vector2Control;
                            var position = positionControl.ReadValueFromEvent(e);

                            var buttonsClicked = _realMouse.allControls.FirstOrDefault(a =>
                                a is ButtonControl abc && abc.ReadValueFromEvent(e) > 0.1f
                            ) != null;
                            if (_realMouse is { enabled: false })
                            {
                                //shouldn't happen.. we disabled it
                                RGDebug.LogWarning("Real mouse event at: " + position.x + "," + position.y + "  buttonsClicked: " + buttonsClicked);
                            }
                            else
                            {
                                RGDebug.LogVerbose("Real mouse event at: " + position.x + "," + position.y + "  buttonsClicked: " + buttonsClicked);
                            }
                        }
                    });
                }
            }

            return _virtualMouse;
        }

        public static Vector2 MoveMouseOffScreen(int replaySegment = 0)
        {
            var mousePosition = new Vector2(Screen.width + 20, -20);

            // just get the cursor off the screen
            // need to use the static accessor here as this anonymous function's parent gameObject instance could get destroyed
            var virtualMouseCursors = Object.FindObjectsOfType<VirtualMouseCursor>();
            virtualMouseCursors.FirstOrDefault()?.SetPosition(mousePosition);

            // also send the event to move it off screen in the tracking if it hasn't been destroyed
            SendRawPositionMouseEvent(replaySegment, mousePosition);

            return mousePosition;
        }

        private static void SendRawPositionMouseEvent(int replaySegment, Vector2 normalizedPosition, bool leftButton = false, bool middleButton = false, bool rightButton = false, bool forwardButton = false, bool backButton = false)
        {
            SendRawPositionMouseEvent(replaySegment, normalizedPosition, leftButton, middleButton, rightButton, forwardButton, backButton, Vector2.zero);
        }

        public static void SendRawPositionMouseEvent(int replaySegment, Vector2 normalizedPosition, bool leftButton, bool middleButton, bool rightButton, bool forwardButton, bool backButton, Vector2 scroll)
        {
            if (_virtualMouse != null)
            {
                using (StateEvent.From(_virtualMouse, out var eventPtr))
                {
                    eventPtr.time = InputState.currentTime;

                    var mouseControls = _virtualMouse.allControls;
                    var mouseEventString = "";

                    // 1f == true == clicked state
                    // 0f == false == un-clicked state
                    foreach (var mouseControl in mouseControls)
                    {
                        switch (mouseControl.name)
                        {
                            case "delta":
                                if (_lastMousePosition != null)
                                {
                                    var delta = normalizedPosition - _lastMousePosition.Value;
                                    if (RGDebug.IsDebugEnabled)
                                    {
                                        mouseEventString += $"delta: {delta.x},{delta.y}  ";
                                    }

                                    ((Vector2Control)mouseControl).WriteValueIntoEvent(delta, eventPtr);
                                }

                                break;
                            case "position":
                                if (RGDebug.IsDebugEnabled)
                                {
                                    mouseEventString += $"position: {normalizedPosition.x},{normalizedPosition.y}  ";
                                }

                                ((Vector2Control)mouseControl).WriteValueIntoEvent(normalizedPosition, eventPtr);
                                break;
                            case "scroll":
                                if (RGDebug.IsDebugEnabled)
                                {
                                    if (scroll.x < -0.1f || scroll.x > 0.1f || scroll.y < -0.1f || scroll.y > 0.1f)
                                    {
                                        mouseEventString += $"scroll: {scroll.x},{scroll.y}  ";
                                    }
                                }

                                ((DeltaControl)mouseControl).WriteValueIntoEvent(scroll, eventPtr);
                                break;
                            case "leftButton":
                                if (RGDebug.IsDebugEnabled)
                                {
                                    if (leftButton)
                                    {
                                        mouseEventString += $"leftButton  ";
                                    }
                                }

                                ((ButtonControl)mouseControl).WriteValueIntoEvent(leftButton ? 1f : 0f, eventPtr);
                                break;
                            case "middleButton":
                                if (RGDebug.IsDebugEnabled)
                                {
                                    if (middleButton)
                                    {
                                        mouseEventString += $"middleButton  ";
                                    }
                                }

                                ((ButtonControl)mouseControl).WriteValueIntoEvent(middleButton ? 1f : 0f, eventPtr);
                                break;
                            case "rightButton":
                                if (RGDebug.IsDebugEnabled)
                                {
                                    if (rightButton)
                                    {
                                        mouseEventString += $"rightButton  ";
                                    }
                                }

                                ((ButtonControl)mouseControl).WriteValueIntoEvent(rightButton ? 1f : 0f, eventPtr);
                                break;
                            case "forwardButton":
                                if (RGDebug.IsDebugEnabled)
                                {
                                    if (forwardButton)
                                    {
                                        mouseEventString += $"forwardButton  ";
                                    }
                                }

                                ((ButtonControl)mouseControl).WriteValueIntoEvent(forwardButton ? 1f : 0f, eventPtr);
                                break;
                            case "backButton":
                                if (RGDebug.IsDebugEnabled)
                                {
                                    if (backButton)
                                    {
                                        mouseEventString += $"backButton  ";
                                    }
                                }

                                ((ButtonControl)mouseControl).WriteValueIntoEvent(backButton ? 1f : 0f, eventPtr);
                                break;
                        }
                    }

#if ENABLE_LEGACY_INPUT_MANAGER
                    {
                        Vector2 delta = _lastMousePosition.HasValue ? (normalizedPosition - _lastMousePosition.Value) : Vector2.zero;
                        SendMouseEventLegacy(position: normalizedPosition, delta: delta, scroll: scroll,
                            leftButton: leftButton, middleButton: middleButton, rightButton: rightButton,
                            forwardButton: forwardButton, backButton: backButton);
                    }
#endif

                    _lastMousePosition = normalizedPosition;

                    // Disable the Real mouse whenever we have a click about to happen, until we un-click
                    // This is a bit scary, but it provides stability to avoid any single or partial pixel twitches of the real mouse ruining click events or positions to the UI system
                    if (leftButton || middleButton || rightButton || forwardButton || backButton)
                    {
                        if (_realMouse is { enabled: true })
                        {
                            RGDebug.LogDebug("Disabling the real mouse device before virtual click mouse event");
                            InputSystem.DisableDevice(_realMouse);
                        }
                    }


                    if (RGDebug.IsDebugEnabled)
                    {
                        RGDebug.LogDebug($"({replaySegment}) [frame: {Time.frameCount}] - Sending Virtual Mouse Event - {mouseEventString}");
                    }

                    InputSystem.QueueEvent(eventPtr);
                }
            }
        }

        public static void SendMouseEvent(int replaySegment, MouseInputActionData mouseInput, Dictionary<long, ObjectStatus> priorTransforms, Dictionary<long, ObjectStatus> priorEntities, Dictionary<long, ObjectStatus> transforms, Dictionary<long, ObjectStatus> entities)
        {
            var clickObjectResult = FindBestClickObject(Camera.main, mouseInput, priorTransforms, priorEntities, transforms, entities);

            var bestObject = clickObjectResult.Item1;
            var normalizedPosition = clickObjectResult.Item3;

            // non-world space object found, make sure we hit the object
            if (bestObject is { screenSpaceBounds: not null } && !clickObjectResult.Item2)
            {
                var clickBounds = bestObject.screenSpaceBounds.Value;

                var possibleObjects = clickObjectResult.Item4;
                foreach (var objectToCheck in possibleObjects)
                {
                    if (objectToCheck.screenSpaceBounds.HasValue)
                    {
                        var ssb = objectToCheck.screenSpaceBounds.Value;
                        if (clickBounds.Intersects(ssb))
                        {
                            // max of the mins; and min of the maxes
                            clickBounds.SetMinMax(
                                Vector3.Max(clickBounds.min, ssb.min),
                                Vector3.Min(clickBounds.max, ssb.max)
                            );
                        }
                    }
                }

                // make sure our click is on that object
                if (!clickBounds.Contains(normalizedPosition))
                {
                    var old = normalizedPosition;
                    // use the center of these bounds as our best point to click
                    normalizedPosition = new Vector2((int)clickBounds.center.x, (int)clickBounds.center.y);

                    RGDebug.LogInfo($"({replaySegment}) Adjusting click location to ensure hit on object path: " + bestObject.Path + " oldPosition: (" + old.x + "," + old.y + "), newPosition: (" + normalizedPosition.x + "," + normalizedPosition.y + ")");
                }
            }

            SendRawPositionMouseEvent(replaySegment, normalizedPosition, mouseInput.leftButton, mouseInput.middleButton, mouseInput.rightButton, mouseInput.forwardButton, mouseInput.backButton, mouseInput.scroll);

        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static void SendMouseEventLegacy(Vector2 position, Vector2 delta, Vector2 scroll,
            bool leftButton, bool middleButton, bool rightButton, bool forwardButton, bool backButton)
        {
            if (RGLegacyInputWrapper.IsPassthrough)
            {
                return;
            }

            RGLegacyInputWrapper.SimulateMouseMovement(new Vector3(position.x, position.y, 0.0f), new Vector3(delta.x, delta.y, 0.0f));
            RGLegacyInputWrapper.SimulateMouseScrollWheel(scroll);

            if (leftButton)
                RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse0);
            else
                RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse0);

            if (middleButton)
                RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse2);
            else
                RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse2);

            if (rightButton)
                RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse1);
            else
                RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse1);

            if (forwardButton)
                RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse3);
            else
                RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse3);

            if (backButton)
                RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse4);
            else
                RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse4);
        }
#endif

        // Finds the best object to adjust our click position to for a given mouse input
        // Uses the exact path for UI clicks, but the normalized path for world space clicks
        // Returns (the object, whether it was world space, the suggested mouse position)
        private static (ObjectStatus, bool, Vector2, IEnumerable<ObjectStatus>) FindBestClickObject(Camera mainCamera, MouseInputActionData mouseInput, Dictionary<long, ObjectStatus> priorTransforms, Dictionary<long, ObjectStatus> priorEntities, Dictionary<long, ObjectStatus> transforms, Dictionary<long, ObjectStatus> entities)
        {

            // Mouse is hard... we can't use the raw position, we need to use the position relative to the current resolution
            // but.. it gets tougher than that.  Some UI elements scale differently with resolution (only horizontal, only vertical, preserve aspect, expand, etc)
            // so we have to take the bounding space of the original object(s) clicked on into consideration
            var normalizedPosition = mouseInput.NormalizedPosition;

            // note that if this mouse input wasn't a click, there will be no possible objects
            if (mouseInput.clickedObjectNormalizedPaths == null || mouseInput.clickedObjectNormalizedPaths.Length == 0)
            {
                // bail out early, no click
                return (null, false, normalizedPosition, transforms.Values.Concat(entities.Values));
            }

            var theNp = new Vector3(normalizedPosition.x, normalizedPosition.y, 0f);

            // find possible objects prioritizing the currentState, falling back to the priorState

            // copy so we can remove
            var mousePaths = mouseInput.clickedObjectNormalizedPaths;
            var pathsToFind = mousePaths.ToList();

            var possibleObjects = new List<ObjectStatus>();

            foreach (var os in transforms)
            {
                var val = os.Value;
                if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.Path))
                {
                    possibleObjects.Add(val);
                    StateRecorderUtils.OptimizedRemoveStringFromList(pathsToFind, val.Path);
                }
            }

            foreach (var os in entities)
            {
                var val = os.Value;
                if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.NormalizedPath))
                {
                    possibleObjects.Add(val);
                    StateRecorderUtils.OptimizedRemoveStringFromList(pathsToFind, val.NormalizedPath);
                }
            }

            // still have some objects we didnt' find in the current state, check previous state
            // this is used primarly for mouse up event processing
            if (pathsToFind.Count > 0)
            {
                if (priorTransforms != null)
                {
                    foreach (var os in priorTransforms)
                    {
                        var val = os.Value;
                        if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.Path))
                        {
                            possibleObjects.Add(val);
                        }
                    }

                }

                if (priorEntities != null)
                {
                    foreach (var os in priorEntities)
                    {
                        var val = os.Value;
                        if (StateRecorderUtils.OptimizedContainsStringInArray(mousePaths, val.NormalizedPath))
                        {
                            possibleObjects.Add(val);
                        }
                    }
                }
            }

            var cos = possibleObjects.Where(a=>a.screenSpaceBounds.HasValue).OrderBy(a =>
            {
                var closestPointInA = a.screenSpaceBounds.Value.ClosestPoint(theNp);
                return (theNp - closestPointInA).sqrMagnitude;
            });
            possibleObjects = cos.Distinct(_recordedGameObjectStatePathEqualityComparer)
                .ToList(); // select only the first entry of each path for ui elements; uses ToList due to multiple iterations of this structure later in the code to avoid multiple enumeration

            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            float smallestSize = screenWidth * screenHeight;

            ObjectStatus bestObject = null;
            var worldSpaceObject = false;

            var possibleObjectsCount = possibleObjects.Count;
            for (var j = 0; j < possibleObjectsCount; j++)
            {
                var objectToCheck = possibleObjects[j];
                var ssb = objectToCheck.screenSpaceBounds.Value;
                var size = ssb.size.x * ssb.size.y;

                if (objectToCheck.worldSpaceBounds != null)
                {
                    if (bestObject is { worldSpaceBounds: null })
                    {
                        //do nothing, prefer ui elements
                    }
                    else
                    {
                        if (mouseInput.worldPosition != null)
                        {
                            // use the world space click location closest to the actual object location
                            var mouseWorldPosition = mouseInput.worldPosition.Value;
                            // uses the collider bounds on that object as colliders are what would drive world space objects' click detection in scripts / etc
                            if (objectToCheck.PositionHitsCollider( mouseWorldPosition))
                            {
                                var screenPoint = Vector3.zero;
                                if (mainCamera != null)
                                {
                                    screenPoint = mainCamera.WorldToScreenPoint(mouseWorldPosition);
                                }

                                if (screenPoint.x < 0 || screenPoint.x > screenWidth || screenPoint.y < 0 || screenPoint.y > screenHeight)
                                {
                                    RGDebug.LogWarning($"Attempted to click at worldPosition: [{mouseWorldPosition.x},{mouseWorldPosition.y},{mouseWorldPosition.z}], which is off screen at position: [{screenPoint.x},{screenPoint.y}]");
                                }
                                else
                                {
                                    bestObject = objectToCheck;
                                    worldSpaceObject = true;
                                    var old = normalizedPosition;
                                    // we hit one of our world objects, set the normalized position and stop looping
                                    normalizedPosition = new Vector2Int((int)screenPoint.x, (int)screenPoint.y);
                                    RGDebug.LogInfo($"Adjusting world click location to ensure hit on object: " + objectToCheck.Path + " oldPosition: (" + old.x + "," + old.y + "), newPosition: (" + normalizedPosition.x + "," + normalizedPosition.y + ")");
                                    break; // end the for
                                }
                            }
                            else
                            {
                                // didn't hit a collider on this object, we fall back to the renderer bounds bestObject evaluation method, similar to ui elements

                                // compare elements bounds for best match
                                // give some threshold variance here for floating point math on sizes
                                // if 2 objects are very similarly sized, we want the one closest, not picking
                                // one based on some floating point rounding error
                                if (size * 1.02f < smallestSize)
                                {
                                    bestObject = objectToCheck;
                                    smallestSize = size;
                                }
                            }
                        }
                        else
                        {
                            // mouse input didn't capture a world position, so we weren't clicking on a world space object.. ignore anything not UI related from consideration
                            if (objectToCheck.worldSpaceBounds == null)
                            {
                                // compare elements bounds for best match
                                // give some threshold variance here for floating point math on sizes
                                // if 2 objects are very similarly sized, we want the one closest, not picking
                                // one based on some floating point rounding error
                                if (size * 1.02f < smallestSize)
                                {
                                    bestObject = objectToCheck;
                                    smallestSize = size;
                                }
                            }
                        }
                    }
                }
                else // objectToCheck.worldSpaceBounds == null
                {
                    if (bestObject is { worldSpaceBounds: null })
                    {
                        // compare ui elements for best match
                        // give some threshold variance here for floating point math on sizes
                        // if 2 objects are very similarly sized, we want the one closest, not picking
                        // one based on some floating point rounding error
                        if (size * 1.02f < smallestSize)
                        {
                            bestObject = objectToCheck;
                            smallestSize = size;
                        }
                    }
                    else //bestObject.worldSpaceBounds != null
                    {
                        // prefer UI elements when overlaps occur with game objects
                        bestObject = objectToCheck;
                        smallestSize = size;
                    }
                }
            }

            return (bestObject, worldSpaceObject, normalizedPosition, possibleObjects);
        }
    }
}
