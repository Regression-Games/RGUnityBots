﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.RGLegacyInputUtility
{
    public static class RGLegacyInputUtils
    {
        public static KeyCode InputSystemKeyToKeyCode(Key key)
        {
            switch (key)
            {
                case Key.None: return KeyCode.None;
                case Key.Space: return KeyCode.Space;
                case Key.Enter: return KeyCode.Return;
                case Key.Tab: return KeyCode.Tab;
                case Key.Backquote: return KeyCode.BackQuote;
                case Key.Quote: return KeyCode.Quote;
                case Key.Semicolon: return KeyCode.Semicolon;
                case Key.Comma: return KeyCode.Comma;
                case Key.Period: return KeyCode.Period;
                case Key.Slash: return KeyCode.Slash;
                case Key.Backslash: return KeyCode.Backslash;
                case Key.LeftBracket: return KeyCode.LeftBracket;
                case Key.RightBracket: return KeyCode.RightBracket;
                case Key.Minus: return KeyCode.Minus;
                case Key.Equals: return KeyCode.Equals;
                case Key.A: return KeyCode.A;
                case Key.B: return KeyCode.B;
                case Key.C: return KeyCode.C;
                case Key.D: return KeyCode.D;
                case Key.E: return KeyCode.E;
                case Key.F: return KeyCode.F;
                case Key.G: return KeyCode.G;
                case Key.H: return KeyCode.H;
                case Key.I: return KeyCode.I;
                case Key.J: return KeyCode.J;
                case Key.K: return KeyCode.K;
                case Key.L: return KeyCode.L;
                case Key.M: return KeyCode.M;
                case Key.N: return KeyCode.N;
                case Key.O: return KeyCode.O;
                case Key.P: return KeyCode.P;
                case Key.Q: return KeyCode.Q;
                case Key.R: return KeyCode.R;
                case Key.S: return KeyCode.S;
                case Key.T: return KeyCode.T;
                case Key.U: return KeyCode.U;
                case Key.V: return KeyCode.V;
                case Key.W: return KeyCode.W;
                case Key.X: return KeyCode.X;
                case Key.Y: return KeyCode.Y;
                case Key.Z: return KeyCode.Z;
                case Key.Digit1: return KeyCode.Alpha1;
                case Key.Digit2: return KeyCode.Alpha2;
                case Key.Digit3: return KeyCode.Alpha3;
                case Key.Digit4: return KeyCode.Alpha4;
                case Key.Digit5: return KeyCode.Alpha5;
                case Key.Digit6: return KeyCode.Alpha6;
                case Key.Digit7: return KeyCode.Alpha7;
                case Key.Digit8: return KeyCode.Alpha8;
                case Key.Digit9: return KeyCode.Alpha9;
                case Key.Digit0: return KeyCode.Alpha0;
                case Key.LeftShift: return KeyCode.LeftShift;
                case Key.RightShift: return KeyCode.RightShift;
                case Key.LeftAlt: return KeyCode.LeftAlt;
                case Key.RightAlt: return KeyCode.RightAlt;
                case Key.LeftCtrl: return KeyCode.LeftControl;
                case Key.RightCtrl: return KeyCode.RightControl;
                case Key.LeftCommand: return KeyCode.LeftCommand;
                case Key.RightCommand: return KeyCode.RightCommand;
                case Key.ContextMenu: return KeyCode.Menu;
                case Key.Escape: return KeyCode.Escape;
                case Key.LeftArrow: return KeyCode.LeftArrow;
                case Key.RightArrow: return KeyCode.RightArrow;
                case Key.UpArrow: return KeyCode.UpArrow;
                case Key.DownArrow: return KeyCode.DownArrow;
                case Key.Backspace: return KeyCode.Backspace;
                case Key.PageDown: return KeyCode.PageDown;
                case Key.PageUp: return KeyCode.PageUp;
                case Key.Home: return KeyCode.Home;
                case Key.End: return KeyCode.End;
                case Key.Insert: return KeyCode.Insert;
                case Key.Delete: return KeyCode.Delete;
                case Key.CapsLock: return KeyCode.CapsLock;
                case Key.NumLock: return KeyCode.Numlock;
                case Key.PrintScreen: return KeyCode.Print;
                case Key.ScrollLock: return KeyCode.ScrollLock;
                case Key.Pause: return KeyCode.Pause;
                case Key.NumpadEnter: return KeyCode.KeypadEnter;
                case Key.NumpadDivide: return KeyCode.KeypadDivide;
                case Key.NumpadMultiply: return KeyCode.KeypadMultiply;
                case Key.NumpadPlus: return KeyCode.KeypadPlus;
                case Key.NumpadMinus: return KeyCode.KeypadMinus;
                case Key.NumpadPeriod: return KeyCode.KeypadPeriod;
                case Key.NumpadEquals: return KeyCode.KeypadEquals;
                case Key.Numpad0: return KeyCode.Keypad0;
                case Key.Numpad1: return KeyCode.Keypad1;
                case Key.Numpad2: return KeyCode.Keypad2;
                case Key.Numpad3: return KeyCode.Keypad3;
                case Key.Numpad4: return KeyCode.Keypad4;
                case Key.Numpad5: return KeyCode.Keypad5;
                case Key.Numpad6: return KeyCode.Keypad6;
                case Key.Numpad7: return KeyCode.Keypad7;
                case Key.Numpad8: return KeyCode.Keypad8;
                case Key.Numpad9: return KeyCode.Keypad9;
                case Key.F1: return KeyCode.F1;
                case Key.F2: return KeyCode.F2;
                case Key.F3: return KeyCode.F3;
                case Key.F4: return KeyCode.F4;
                case Key.F5: return KeyCode.F5;
                case Key.F6: return KeyCode.F6;
                case Key.F7: return KeyCode.F7;
                case Key.F8: return KeyCode.F8;
                case Key.F9: return KeyCode.F9;
                case Key.F10: return KeyCode.F10;
                case Key.F11: return KeyCode.F11;
                case Key.F12: return KeyCode.F12;
                default: 
                    RGDebug.LogWarning($"Unsupported keyboard input for legacy input: {key}");
                    return KeyCode.None;
            }
        }
        
        public static Key KeyCodeToInputSystemKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.None: return Key.None;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftCommand: return Key.LeftCommand;
                case KeyCode.RightCommand: return Key.RightCommand;
                case KeyCode.Menu: return Key.ContextMenu;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.CapsLock: return Key.CapsLock;
                case KeyCode.Numlock: return Key.NumLock;
                case KeyCode.Print: return Key.PrintScreen;
                case KeyCode.ScrollLock: return Key.ScrollLock;
                case KeyCode.Pause: return Key.Pause;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.KeypadDivide: return Key.NumpadDivide;
                case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
                case KeyCode.KeypadPlus: return Key.NumpadPlus;
                case KeyCode.KeypadMinus: return Key.NumpadMinus;
                case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
                case KeyCode.KeypadEquals: return Key.NumpadEquals;
                case KeyCode.Keypad0: return Key.Numpad0;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
                case KeyCode.Keypad4: return Key.Numpad4;
                case KeyCode.Keypad5: return Key.Numpad5;
                case KeyCode.Keypad6: return Key.Numpad6;
                case KeyCode.Keypad7: return Key.Numpad7;
                case KeyCode.Keypad8: return Key.Numpad8;
                case KeyCode.Keypad9: return Key.Numpad9;
                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;
                default: 
                    RGDebug.LogWarning($"Unsupported legacy input key: {keyCode}");
                    return Key.None;
            }
        }

        public static Event CreateUIKeyboardEvent(KeyCode keyCode, ISet<KeyCode> keysHeld)
        {
            Event evt = new Event(0) { type = EventType.KeyDown };
            if (keysHeld.Contains(KeyCode.LeftShift) || keysHeld.Contains(KeyCode.RightShift))
            {
                evt.modifiers |= EventModifiers.Shift;
            }
            if (keysHeld.Contains(KeyCode.LeftCommand) || keysHeld.Contains(KeyCode.RightCommand))
            {
                evt.modifiers |= EventModifiers.Command;
            }
            if (keysHeld.Contains(KeyCode.LeftAlt) || keysHeld.Contains(KeyCode.RightAlt))
            {
                evt.modifiers |= EventModifiers.Alt;
            }
            if (keysHeld.Contains(KeyCode.LeftControl) || keysHeld.Contains(KeyCode.RightControl))
            {
                evt.modifiers |= EventModifiers.Control;
            }
            
            
        }
    }
}