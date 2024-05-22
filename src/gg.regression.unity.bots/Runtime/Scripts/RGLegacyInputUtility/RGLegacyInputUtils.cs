﻿#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
#endif

namespace RegressionGames.RGLegacyInputUtility
{
    public static class RGLegacyInputUtils
    {
#if ENABLE_INPUT_SYSTEM
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
#endif
    }
}