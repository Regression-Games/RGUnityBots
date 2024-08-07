﻿using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager
{
    public static class RGActionManagerUtils
    {
        private static FieldInfo _persistentCallsField;
        private static MethodInfo _countMethod;
        private static MethodInfo _getListenerMethod;
        private static FieldInfo _targetField;
        private static FieldInfo _targetAssemblyTypeNameField;
        private static FieldInfo _methodNameField;
        private static PropertyInfo _currentSelectionStateProperty;
        
        private static List<RaycastResult> _raycastResultCache = new List<RaycastResult>();
        private static Camera[] _camerasBuf;
        
        /// <summary>
        /// This method computes and returns the full method signature of all the event listeners that are bound to the given event
        /// </summary>
        public static IEnumerable<string> GetEventListenerMethodNames(UnityEvent evt)
        {
            if (_persistentCallsField == null)
            {
                _persistentCallsField = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            
            object persistentCalls = _persistentCallsField.GetValue(evt);

            if (_countMethod == null)
            {
                _countMethod = persistentCalls.GetType().GetMethod("get_Count", BindingFlags.Public | BindingFlags.Instance);
                _getListenerMethod = persistentCalls.GetType().GetMethod("GetListener", BindingFlags.Public | BindingFlags.Instance);
            }
        
            int listenerCount = (int)_countMethod.Invoke(persistentCalls, null);

            for (int i = 0; i < listenerCount; i++)
            {
                object listener = _getListenerMethod.Invoke(persistentCalls, new object[] { i });
                if (_targetField == null)
                {
                    _targetField = listener.GetType().GetField("m_Target", BindingFlags.NonPublic | BindingFlags.Instance);
                    _targetAssemblyTypeNameField = listener.GetType().GetField("m_TargetAssemblyTypeName", BindingFlags.NonPublic | BindingFlags.Instance);
                    _methodNameField = listener.GetType()
                                        .GetField("m_MethodName", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                string targetTypeName = (string)_targetAssemblyTypeNameField.GetValue(listener);
                string methodName = (string)_methodNameField.GetValue(listener);

                if (string.IsNullOrEmpty(targetTypeName))
                {
                    // if not available, try deriving from the target
                    Object target = (Object)_targetField.GetValue(listener);
                    if (target != null)
                    {
                        targetTypeName = target.GetType().FullName;
                    }
                }
                
                if (!string.IsNullOrEmpty(targetTypeName) && !string.IsNullOrEmpty(methodName))
                {
                    yield return targetTypeName.Split(", ")[0] + "." + methodName;
                }
            }
        }

        private static string GetUIObjectSelectionState(Selectable uiComponent)
        {
            if (_currentSelectionStateProperty == null)
            {
                _currentSelectionStateProperty = typeof(Selectable).GetProperty("currentSelectionState", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return _currentSelectionStateProperty.GetValue(uiComponent).ToString();
        }

        public static bool IsUIObjectPressed(Selectable uiComponent)
        {
            return GetUIObjectSelectionState(uiComponent) == "Pressed";
        }
        
        // Mapping from keyboard property (Keyboard.current.<property>) to the associated key code
        private static readonly Dictionary<string, Key> KeyboardPropNameToKeyCode = new Dictionary<string, Key>()
        {
            { "aKey", Key.A },
            { "altKey", Key.LeftAlt },
            { "bKey", Key.B },
            { "backquoteKey", Key.Backquote },
            { "backslashKey", Key.Backslash },
            { "backspaceKey", Key.Backspace },
            { "cKey", Key.C },
            { "capsLockKey", Key.CapsLock },
            { "commaKey", Key.Comma },
            { "contextMenuKey", Key.ContextMenu },
            { "ctrlKey", Key.LeftCtrl },
            { "dKey", Key.D },
            { "deleteKey", Key.Delete },
            { "digit0Key", Key.Digit0 },
            { "digit1Key", Key.Digit1 },
            { "digit2Key", Key.Digit2 },
            { "digit3Key", Key.Digit3 },
            { "digit4Key", Key.Digit4 },
            { "digit5Key", Key.Digit5 },
            { "digit6Key", Key.Digit6 },
            { "digit7Key", Key.Digit7 },
            { "digit8Key", Key.Digit8 },
            { "digit9Key", Key.Digit9 },
            { "downArrowKey", Key.DownArrow },
            { "eKey", Key.E },
            { "endKey", Key.End },
            { "enterKey", Key.Enter },
            { "equalsKey", Key.Equals },
            { "escapeKey", Key.Escape },
            { "f10Key", Key.F10 },
            { "f11Key", Key.F11 },
            { "f12Key", Key.F12 },
            { "f1Key", Key.F1 },
            { "f2Key", Key.F2 },
            { "f3Key", Key.F3 },
            { "f4Key", Key.F4 },
            { "f5Key", Key.F5 },
            { "f6Key", Key.F6 },
            { "f7Key", Key.F7 },
            { "f8Key", Key.F8 },
            { "f9Key", Key.F9 },
            { "fKey", Key.F },
            { "gKey", Key.G },
            { "hKey", Key.H },
            { "homeKey", Key.Home },
            { "iKey", Key.I },
            { "insertKey", Key.Insert },
            { "jKey", Key.J },
            { "kKey", Key.K },
            { "lKey", Key.L },
            { "leftAltKey", Key.LeftAlt },
            { "leftAppleKey", Key.LeftApple },
            { "leftArrowKey", Key.LeftArrow },
            { "leftBracketKey", Key.LeftBracket },
            { "leftCommandKey", Key.LeftCommand },
            { "leftCtrlKey", Key.LeftCtrl },
            { "leftMetaKey", Key.LeftMeta },
            { "leftShiftKey", Key.LeftShift },
            { "leftWindowsKey", Key.LeftWindows },
            { "mKey", Key.M },
            { "minusKey", Key.Minus },
            { "nKey", Key.N },
            { "numLockKey", Key.NumLock },
            { "numpad0Key", Key.Numpad0 },
            { "numpad1Key", Key.Numpad1 },
            { "numpad2Key", Key.Numpad2 },
            { "numpad3Key", Key.Numpad3 },
            { "numpad4Key", Key.Numpad4 },
            { "numpad5Key", Key.Numpad5 },
            { "numpad6Key", Key.Numpad6 },
            { "numpad7Key", Key.Numpad7 },
            { "numpad8Key", Key.Numpad8 },
            { "numpad9Key", Key.Numpad9 },
            { "numpadDivideKey", Key.NumpadDivide },
            { "numpadEnterKey", Key.NumpadEnter },
            { "numpadEqualsKey", Key.NumpadEquals },
            { "numpadMinusKey", Key.NumpadMinus },
            { "numpadMultiplyKey", Key.NumpadMultiply },
            { "numpadPeriodKey", Key.NumpadPeriod },
            { "numpadPlusKey", Key.NumpadPlus },
            { "oKey", Key.O },
            { "oem1Key", Key.OEM1 },
            { "oem2Key", Key.OEM2 },
            { "oem3Key", Key.OEM3 },
            { "oem4Key", Key.OEM4 },
            { "oem5Key", Key.OEM5 },
            { "pKey", Key.P },
            { "pageDownKey", Key.PageDown },
            { "pageUpKey", Key.PageUp },
            { "pauseKey", Key.Pause },
            { "periodKey", Key.Period },
            { "printScreenKey", Key.PrintScreen },
            { "qKey", Key.Q },
            { "quoteKey", Key.Quote },
            { "rKey", Key.R },
            { "rightAltKey", Key.RightAlt },
            { "rightAppleKey", Key.RightApple },
            { "rightArrowKey", Key.RightArrow },
            { "rightBracketKey", Key.RightBracket },
            { "rightCommandKey", Key.RightCommand },
            { "rightCtrlKey", Key.RightCtrl },
            { "rightMetaKey", Key.RightMeta },
            { "rightShiftKey", Key.RightShift },
            { "rightWindowsKey", Key.RightWindows },
            { "sKey", Key.S },
            { "scrollLockKey", Key.ScrollLock },
            { "semicolonKey", Key.Semicolon },
            { "shiftKey", Key.LeftShift },
            { "slashKey", Key.Slash },
            { "spaceKey", Key.Space },
            { "tKey", Key.T },
            { "tabKey", Key.Tab },
            { "uKey", Key.U },
            { "upArrowKey", Key.UpArrow },
            { "vKey", Key.V },
            { "wKey", Key.W },
            { "xKey", Key.X },
            { "yKey", Key.Y },
            { "zKey", Key.Z }
        };
        
        public static Key InputSystemKeyboardPropertyNameToKey(string keyPropName)
        {
            if (KeyboardPropNameToKeyCode.TryGetValue(keyPropName, out Key key))
            {
                return key;
            }
            else
            {
                return Key.None;
            }
        }
        
        /// <summary>
        /// This finds a point on the screen that is outside of the given rectangle.
        /// It tries to prioritize points that are on the screen, but if no such point exists then
        /// it will fall back to a point off the screen.
        /// </summary>
        public static Vector2 GetPointOutsideBounds(Bounds ssBounds)
        {
            Bounds screenRect = new Bounds(
                new Vector3(Screen.width/2.0f, Screen.height/2.0f, 0.0f),
                      new Vector3(Screen.width, Screen.height, 0.0f));
            float extentScale = 1.5f;
            Vector2[] candidates = new Vector2[]
            {
                ssBounds.center + new Vector3(ssBounds.extents.x*extentScale, 0.0f, 0.0f),
                ssBounds.center + new Vector3(-ssBounds.extents.x*extentScale, 0.0f, 0.0f),
                ssBounds.center + new Vector3(0.0f, ssBounds.extents.y*extentScale, 0.0f),
                ssBounds.center + new Vector3(0.0f, -ssBounds.extents.y*extentScale, 0.0f)
            };
            // first try to find a point outside the bounds that are on the screen
            foreach (Vector2 pt in candidates)
            {
                if (screenRect.Contains(pt))
                {
                    return pt;
                }
            }
            // if no such point found, just return the first point anyways and have the mouse be off-screen
            return candidates[0];
        }
        
        /// <summary>
        /// Compute the screen space bounds of the given game object's collider.
        /// </summary>
        public static Bounds? GetGameObjectScreenSpaceBounds(GameObject gameObject)
        {
            var instId = gameObject.transform.GetInstanceID();
            Bounds? ssBounds = null;
            if (RGActionManager.CurrentTransforms.TryGetValue(instId, out var tStatus))
            {
                ssBounds = tStatus.screenSpaceBounds;
            }

            if (!ssBounds.HasValue)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    Bounds? colliderBounds = null;
                    if (gameObject.TryGetComponent(out Collider col3D))
                    {
                        colliderBounds = col3D.bounds;
                    } else if (gameObject.TryGetComponent(out Collider2D col2D))
                    {
                        colliderBounds = col2D.bounds;
                    }

                    if (colliderBounds.HasValue)
                    {
                        Vector3 min = colliderBounds.Value.min;
                        Vector3 max = colliderBounds.Value.max;
                        Vector3[] points = new Vector3[8]
                        {
                            new Vector3(min.x, min.y, min.z),
                            new Vector3(max.x, min.y, min.z),
                            new Vector3(min.x, max.y, min.z),
                            new Vector3(max.x, max.y, min.z),
                            new Vector3(min.x, min.y, max.z),
                            new Vector3(max.x, min.y, max.z),
                            new Vector3(min.x, max.y, max.z),
                            new Vector3(max.x, max.y, max.z)
                        };
                        Vector3 minScreen = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0.0f);
                        Vector3 maxScreen = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0.0f);
                        foreach (Vector3 pt in points)
                        {
                            Vector3 screenPt = camera.WorldToScreenPoint(pt);
                            screenPt.x = screenPt.x / camera.pixelWidth * Screen.width;
                            screenPt.y = screenPt.y / camera.pixelHeight * Screen.height;
                            minScreen = Vector3.Min(minScreen, screenPt);
                            maxScreen = Vector3.Max(maxScreen, screenPt);
                        }
                        ssBounds = new Bounds((minScreen + maxScreen) / 2.0f, maxScreen - minScreen);
                    }
                }
            }
            
            return ssBounds;
        }
        
        /// <summary>
        /// Compute the screen space bounds of the given UI object (must have a CanvasRenderer component).
        /// </summary>
        public static Bounds? GetUIScreenSpaceBounds(GameObject uiObject)
        {
            var instId = uiObject.transform.GetInstanceID();
            if (RGActionManager.CurrentTransforms.TryGetValue(instId, out var tStatus))
            {
                return tStatus.screenSpaceBounds;
            }
            else
            {
                return null;
            }
        }

        private static bool IsAncestorOrEqualTo(GameObject ancestor, GameObject gameObject)
        {
            do
            {
                if (ancestor == gameObject)
                {
                    return true;
                }
                gameObject = gameObject.transform.parent?.gameObject;
            } while (gameObject != null);
            return false;
        }

        /// <summary>
        /// Returns whether the given mouse position is hovering over any UI element on the screen.
        /// </summary>
        public static bool IsMouseOverUI(Vector2 mousePos, out CanvasRenderer uiComponent)
        {
            foreach (var eventSys in RGActionManager.CurrentEventSystems)
            {
                PointerEventData data = new PointerEventData(eventSys);
                data.pointerId = PointerInputModule.kMouseLeftId;
                data.position = mousePos;
                data.delta = Vector2.zero;
                _raycastResultCache.Clear();
                eventSys.RaycastAll(data, _raycastResultCache);
                foreach (var raycastRes in _raycastResultCache)
                {
                    if (raycastRes.gameObject != null && raycastRes.gameObject.TryGetComponent(out uiComponent))
                    {
                        return true;
                    }
                }
            }
            uiComponent = null;
            return false;
        }

        /// <summary>
        /// Returns whether the given mouse position is hovering over any 3D collider on the screen.
        /// </summary>
        public static bool IsMouseOverCollider3D(Vector2 mousePos, int layerMask)
        {
            int numCameras = Camera.allCamerasCount;
            if (_camerasBuf == null || _camerasBuf.Length != numCameras)
            {
                _camerasBuf = new Camera[numCameras];
            }
            Camera.GetAllCameras(_camerasBuf);

            foreach (var camera in _camerasBuf)
            {
                if (camera == null || camera.eventMask == 0 || camera.targetTexture != null)
                {
                    continue;
                }
                
                Ray mouseRay = camera.ScreenPointToRay(mousePos);
                if (Physics.Raycast(mouseRay, out RaycastHit hit, maxDistance: Mathf.Infinity, layerMask: layerMask))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the given mouse position is hovering over any 2D collider on the screen.
        /// </summary>
        public static bool IsMouseOverCollider2D(Vector2 mousePos, int layerMask)
        {
            int numCameras = Camera.allCamerasCount;
            if (_camerasBuf == null || _camerasBuf.Length != numCameras)
            {
                _camerasBuf = new Camera[numCameras];
            }
            Camera.GetAllCameras(_camerasBuf);

            foreach (var camera in _camerasBuf)
            {
                if (camera == null || camera.eventMask == 0 || camera.targetTexture != null)
                {
                    continue;
                }
                
                Vector3 mouseWorldPt = camera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, camera.nearClipPlane));
                RaycastHit2D hit2D = Physics2D.Raycast(mouseWorldPt, Vector2.zero, distance: Mathf.Infinity,
                    layerMask: layerMask);
                if (hit2D.collider != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the given UI component is interactable based on the
        /// object's and its ancestors' properties.
        /// </summary>
        public static bool IsUIObjectInteractable(Selectable uiComponent)
        {
            // If the object itself is not interactable, return false
            if (!uiComponent.IsInteractable())
            {
                return false;
            }
            
            // If the containing canvas group of the UI object is not interactable, return false
            Transform t = uiComponent.transform.parent;
            while (t != null)
            {
                CanvasGroup canvasGroup = t.gameObject.GetComponent<CanvasGroup>();
                if (canvasGroup != null && (!canvasGroup.interactable || !canvasGroup.blocksRaycasts))
                {
                    return false;
                }
                t = t.parent;
            }

            return true;
        }

        /// <summary>
        /// This returns the mouse position on the screen needed in order to interact with the
        /// specified UI element. If it is impossible to hit the UI element (e.g.
        /// due to another object obstructing it) then this returns false.
        /// </summary>
        public static bool GetUIMouseHitPosition(GameObject uiObject, out Vector2 result)
        {
            var uiObjectBounds = GetUIScreenSpaceBounds(uiObject);
            if (!uiObjectBounds.HasValue)
            {
                result = Vector2.zero;
                return false;
            }
            
            // In order to handle the case of partial overlap, multiple raycasts are performed (center, bottom left, and top right).
            // The set of raycasts performed should be deterministic, since the action manager tries to behave deterministically as much as possible.
            IEnumerable<Vector2> GetRaycastPoints()
            {
                yield return uiObjectBounds.Value.center;
                yield return uiObjectBounds.Value.center - uiObjectBounds.Value.extents / 2.0f;
                yield return uiObjectBounds.Value.center + uiObjectBounds.Value.extents / 2.0f;
            }

            // Perform a UI raycast using every event system available in the scene (normally there should just be one)
            foreach (var eventSys in RGActionManager.CurrentEventSystems)
            {
                PointerEventData data = new PointerEventData(eventSys);
                data.pointerId = PointerInputModule.kMouseLeftId;
                foreach (var mousePos in GetRaycastPoints())
                {
                    data.position = mousePos;
                    data.delta = Vector2.zero;
                    _raycastResultCache.Clear();
                    eventSys.RaycastAll(data, _raycastResultCache);
                    foreach (var raycastRes in _raycastResultCache)
                    {
                        if (raycastRes.gameObject != null)
                        {
                            // If the raycast hit either the object or one of its children, then the given 
                            // mouse position can be used to interact with the object.
                            if (IsAncestorOrEqualTo(uiObject, raycastRes.gameObject))
                            {
                                result = mousePos;
                                return true;
                            }
                            break;
                        }
                    }
                }
            }

            result = Vector2.zero;
            return false;
        }


        /// <summary>
        /// This returns the mouse position on the screen needed in order to hit the
        /// specified game object's collider. If it is impossible to hit the object (e.g.
        /// due to another object obstructing it) then this returns false.
        /// </summary>
        public static bool GetGameObjectMouseHitPosition(GameObject gameObject, out Vector2 result)
        {
            var ssBounds = GetGameObjectScreenSpaceBounds(gameObject);
            if (!ssBounds.HasValue)
            {
                result = Vector2.zero;
                return false;
            }
            
            // In order to handle the case of partial overlap, multiple raycasts are performed (center, bottom left, and top right).
            // The set of raycasts performed should be deterministic, since the action manager tries to behave deterministically as much as possible.
            IEnumerable<Vector2> GetRaycastPoints()
            {
                yield return ssBounds.Value.center;
                yield return ssBounds.Value.center - ssBounds.Value.extents / 2.0f;
                yield return ssBounds.Value.center + ssBounds.Value.extents / 2.0f;
            }

            bool has3DCollider = gameObject.TryGetComponent<Collider>(out _);
            bool has2DCollider = gameObject.TryGetComponent<Collider2D>(out _);
            
            int numCameras = Camera.allCamerasCount;
            if (_camerasBuf == null || _camerasBuf.Length != numCameras)
            {
                _camerasBuf = new Camera[numCameras];
            }
            Camera.GetAllCameras(_camerasBuf);
            
            // Perform a raycast from each rendering camera in the scene
            foreach (Camera camera in _camerasBuf)
            {
                if (camera == null || camera.eventMask == 0 || camera.targetTexture != null)
                {
                    continue;
                }
                int cameraRaycastMask = camera.cullingMask & camera.eventMask;

                foreach (var screenPt in GetRaycastPoints())
                {
                    if (has3DCollider)
                    {
                        // If this is a 3D collider, and the Physics.Raycast call hits the desired object, then we've
                        // found the appropriate mouse position to use to interact with the object.
                        Ray mouseRay = camera.ScreenPointToRay(screenPt);
                        if (Physics.Raycast(mouseRay, out RaycastHit hit, maxDistance: Mathf.Infinity,
                                layerMask: cameraRaycastMask))
                        {
                            if (hit.collider.gameObject == gameObject)
                            {
                                result = screenPt;
                                return true;
                            }
                        }
                    }

                    if (has2DCollider)
                    {
                        // If this is a 2D collider, and the Physics2D.Raycast call hits the desired object, then we've
                        // found the appropriate mouse position to use to interact with the object.
                        Vector3 mouseWorldPt = camera.ScreenToWorldPoint(new Vector3(screenPt.x, screenPt.y, camera.nearClipPlane));
                        RaycastHit2D hit2D = Physics2D.Raycast(mouseWorldPt, Vector2.zero, distance: Mathf.Infinity, 
                            layerMask: cameraRaycastMask);
                        if (hit2D.collider != null)
                        {
                            if (hit2D.collider.gameObject == gameObject)
                            {
                                result = screenPt;
                                return true;
                            }
                        }
                    }
                }
            }

            result = Vector2.zero;
            return false;
        }
    }
}