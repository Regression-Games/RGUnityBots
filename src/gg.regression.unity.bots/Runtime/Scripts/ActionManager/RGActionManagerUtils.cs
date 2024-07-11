using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager
{
    public static class RGActionManagerUtils
    {
        private static FieldInfo _persistentCallsField;
        private static MethodInfo _countMethod;
        private static MethodInfo _getListenerMethod;
        private static FieldInfo _targetField;
        private static FieldInfo _methodNameField;
        
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
                    _methodNameField = listener.GetType()
                                        .GetField("m_MethodName", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                object target = _targetField.GetValue(listener);
                string methodName = (string)_methodNameField.GetValue(listener);
            
                if (target != null && !string.IsNullOrEmpty(methodName))
                {
                    yield return target.GetType().FullName + "." + methodName;
                }
            }
        }

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
    }
}