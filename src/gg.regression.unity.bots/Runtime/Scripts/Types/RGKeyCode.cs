using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.Types
{
    // Key code enum that is independent of any particular input system
    public enum RGKeyCode
    {
	    None = 0,
	    Backspace = 8,
	    Tab = 9,
	    Clear = 12, 
	    Return = 13, 
	    Pause = 19, 
	    Escape = 27, 
	    Space = 32, 
	    Exclaim = 33, 
	    DoubleQuote = 34, 
	    Hash = 35, 
	    Dollar = 36, 
	    Percent = 37, 
	    Ampersand = 38, 
	    Quote = 39, 
	    LeftParen = 40, 
	    RightParen = 41, 
	    Asterisk = 42, 
	    Plus = 43, 
	    Comma = 44, 
	    Minus = 45, 
	    Period = 46, 
	    Slash = 47, 
	    Alpha0 = 48, 
	    Alpha1 = 49, 
	    Alpha2 = 50, 
	    Alpha3 = 51, 
	    Alpha4 = 52, 
	    Alpha5 = 53, 
	    Alpha6 = 54, 
	    Alpha7 = 55, 
	    Alpha8 = 56, 
	    Alpha9 = 57, 
	    Colon = 58, 
	    Semicolon = 59, 
	    Less = 60, 
	    Equals = 61, 
	    Greater = 62, 
	    Question = 63, 
	    At = 64, 
	    LeftBracket = 91, 
	    Backslash = 92, 
	    RightBracket = 93, 
	    Caret = 94, 
	    Underscore = 95, 
	    BackQuote = 96, 
	    A = 97, 
	    B = 98, 
	    C = 99, 
	    D = 100, 
	    E = 101, 
	    F = 102, 
	    G = 103, 
	    H = 104, 
	    I = 105, 
	    J = 106, 
	    K = 107, 
	    L = 108, 
	    M = 109, 
	    N = 110, 
	    O = 111, 
	    P = 112, 
	    Q = 113, 
	    R = 114, 
	    S = 115, 
	    T = 116, 
	    U = 117, 
	    V = 118, 
	    W = 119, 
	    X = 120, 
	    Y = 121, 
	    Z = 122, 
	    LeftCurlyBracket = 123, 
	    Pipe = 124, 
	    RightCurlyBracket = 125, 
	    Tilde = 126, 
	    Delete = 127, 
	    Keypad0 = 256, 
	    Keypad1 = 257, 
	    Keypad2 = 258, 
	    Keypad3 = 259, 
	    Keypad4 = 260, 
	    Keypad5 = 261, 
	    Keypad6 = 262, 
	    Keypad7 = 263, 
	    Keypad8 = 264, 
	    Keypad9 = 265, 
	    KeypadPeriod = 266, 
	    KeypadDivide = 267, 
	    KeypadMultiply = 268, 
	    KeypadMinus = 269, 
	    KeypadPlus = 270, 
	    KeypadEnter = 271, 
	    KeypadEquals = 272, 
	    UpArrow = 273, 
	    DownArrow = 274, 
	    RightArrow = 275, 
	    LeftArrow = 276, 
	    Insert = 277, 
	    Home = 278, 
	    End = 279, 
	    PageUp = 280, 
	    PageDown = 281, 
	    F1 = 282, 
	    F2 = 283, 
	    F3 = 284, 
	    F4 = 285, 
	    F5 = 286, 
	    F6 = 287, 
	    F7 = 288, 
	    F8 = 289, 
	    F9 = 290, 
	    F10 = 291, 
	    F11 = 292, 
	    F12 = 293, 
	    F13 = 294, 
	    F14 = 295, 
	    F15 = 296, 
	    Numlock = 300, 
	    CapsLock = 301, 
	    ScrollLock = 302, 
	    RightShift = 303, 
	    LeftShift = 304, 
	    RightControl = 305, 
	    LeftControl = 306, 
	    RightAlt = 307, 
	    LeftAlt = 308, 
	    RightApple = 309, 
	    RightCommand = 309, 
	    RightMeta = 309, 
	    LeftApple = 310, 
	    LeftCommand = 310, 
	    LeftMeta = 310, 
	    LeftWindows = 311, 
	    RightWindows = 312, 
	    AltGr = 313, 
	    Help = 315, 
	    Print = 316, 
	    SysReq = 317, 
	    Break = 318, 
	    Menu = 319, 
	    Mouse0 = 323, 
	    Mouse1 = 324, 
	    Mouse2 = 325, 
	    Mouse3 = 326, 
	    Mouse4 = 327, 
	    Mouse5 = 328, 
	    Mouse6 = 329 
    }

    public static class RGKeyCodeHelper
    {
	    #if ENABLE_LEGACY_INPUT_MANAGER
	    public static RGKeyCode ToRGKeyCode(this KeyCode keyCode)
	    {
		    switch (keyCode)
		    {
			    case KeyCode.None: return RGKeyCode.None;
			    case KeyCode.Backspace: return RGKeyCode.Backspace;
			    case KeyCode.Tab: return RGKeyCode.Tab;
			    case KeyCode.Clear: return RGKeyCode.Clear;
			    case KeyCode.Return: return RGKeyCode.Return;
			    case KeyCode.Pause: return RGKeyCode.Pause;
			    case KeyCode.Escape: return RGKeyCode.Escape;
			    case KeyCode.Space: return RGKeyCode.Space;
			    case KeyCode.Exclaim: return RGKeyCode.Exclaim;
			    case KeyCode.DoubleQuote: return RGKeyCode.DoubleQuote;
			    case KeyCode.Hash: return RGKeyCode.Hash;
			    case KeyCode.Dollar: return RGKeyCode.Dollar;
			    case KeyCode.Percent: return RGKeyCode.Percent;
			    case KeyCode.Ampersand: return RGKeyCode.Ampersand;
			    case KeyCode.Quote: return RGKeyCode.Quote;
			    case KeyCode.LeftParen: return RGKeyCode.LeftParen;
			    case KeyCode.RightParen: return RGKeyCode.RightParen;
			    case KeyCode.Asterisk: return RGKeyCode.Asterisk;
			    case KeyCode.Plus: return RGKeyCode.Plus;
			    case KeyCode.Comma: return RGKeyCode.Comma;
			    case KeyCode.Minus: return RGKeyCode.Minus;
			    case KeyCode.Period: return RGKeyCode.Period;
			    case KeyCode.Slash: return RGKeyCode.Slash;
			    case KeyCode.Alpha0: return RGKeyCode.Alpha0;
			    case KeyCode.Alpha1: return RGKeyCode.Alpha1;
			    case KeyCode.Alpha2: return RGKeyCode.Alpha2;
			    case KeyCode.Alpha3: return RGKeyCode.Alpha3;
			    case KeyCode.Alpha4: return RGKeyCode.Alpha4;
			    case KeyCode.Alpha5: return RGKeyCode.Alpha5;
			    case KeyCode.Alpha6: return RGKeyCode.Alpha6;
			    case KeyCode.Alpha7: return RGKeyCode.Alpha7;
			    case KeyCode.Alpha8: return RGKeyCode.Alpha8;
			    case KeyCode.Alpha9: return RGKeyCode.Alpha9;
			    case KeyCode.Colon: return RGKeyCode.Colon;
			    case KeyCode.Semicolon: return RGKeyCode.Semicolon;
			    case KeyCode.Less: return RGKeyCode.Less;
			    case KeyCode.Equals: return RGKeyCode.Equals;
			    case KeyCode.Greater: return RGKeyCode.Greater;
			    case KeyCode.Question: return RGKeyCode.Question;
			    case KeyCode.At: return RGKeyCode.At;
			    case KeyCode.LeftBracket: return RGKeyCode.LeftBracket;
			    case KeyCode.Backslash: return RGKeyCode.Backslash;
			    case KeyCode.RightBracket: return RGKeyCode.RightBracket;
			    case KeyCode.Caret: return RGKeyCode.Caret;
			    case KeyCode.Underscore: return RGKeyCode.Underscore;
			    case KeyCode.BackQuote: return RGKeyCode.BackQuote;
			    case KeyCode.A: return RGKeyCode.A;
			    case KeyCode.B: return RGKeyCode.B;
			    case KeyCode.C: return RGKeyCode.C;
			    case KeyCode.D: return RGKeyCode.D;
			    case KeyCode.E: return RGKeyCode.E;
			    case KeyCode.F: return RGKeyCode.F;
			    case KeyCode.G: return RGKeyCode.G;
			    case KeyCode.H: return RGKeyCode.H;
			    case KeyCode.I: return RGKeyCode.I;
			    case KeyCode.J: return RGKeyCode.J;
			    case KeyCode.K: return RGKeyCode.K;
			    case KeyCode.L: return RGKeyCode.L;
			    case KeyCode.M: return RGKeyCode.M;
			    case KeyCode.N: return RGKeyCode.N;
			    case KeyCode.O: return RGKeyCode.O;
			    case KeyCode.P: return RGKeyCode.P;
			    case KeyCode.Q: return RGKeyCode.Q;
			    case KeyCode.R: return RGKeyCode.R;
			    case KeyCode.S: return RGKeyCode.S;
			    case KeyCode.T: return RGKeyCode.T;
			    case KeyCode.U: return RGKeyCode.U;
			    case KeyCode.V: return RGKeyCode.V;
			    case KeyCode.W: return RGKeyCode.W;
			    case KeyCode.X: return RGKeyCode.X;
			    case KeyCode.Y: return RGKeyCode.Y;
			    case KeyCode.Z: return RGKeyCode.Z;
			    case KeyCode.LeftCurlyBracket: return RGKeyCode.LeftCurlyBracket;
			    case KeyCode.Pipe: return RGKeyCode.Pipe;
			    case KeyCode.RightCurlyBracket: return RGKeyCode.RightCurlyBracket;
			    case KeyCode.Tilde: return RGKeyCode.Tilde;
			    case KeyCode.Delete: return RGKeyCode.Delete;
			    case KeyCode.Keypad0: return RGKeyCode.Keypad0;
			    case KeyCode.Keypad1: return RGKeyCode.Keypad1;
			    case KeyCode.Keypad2: return RGKeyCode.Keypad2;
			    case KeyCode.Keypad3: return RGKeyCode.Keypad3;
			    case KeyCode.Keypad4: return RGKeyCode.Keypad4;
			    case KeyCode.Keypad5: return RGKeyCode.Keypad5;
			    case KeyCode.Keypad6: return RGKeyCode.Keypad6;
			    case KeyCode.Keypad7: return RGKeyCode.Keypad7;
			    case KeyCode.Keypad8: return RGKeyCode.Keypad8;
			    case KeyCode.Keypad9: return RGKeyCode.Keypad9;
			    case KeyCode.KeypadPeriod: return RGKeyCode.KeypadPeriod;
			    case KeyCode.KeypadDivide: return RGKeyCode.KeypadDivide;
			    case KeyCode.KeypadMultiply: return RGKeyCode.KeypadMultiply;
			    case KeyCode.KeypadMinus: return RGKeyCode.KeypadMinus;
			    case KeyCode.KeypadPlus: return RGKeyCode.KeypadPlus;
			    case KeyCode.KeypadEnter: return RGKeyCode.KeypadEnter;
			    case KeyCode.KeypadEquals: return RGKeyCode.KeypadEquals;
			    case KeyCode.UpArrow: return RGKeyCode.UpArrow;
			    case KeyCode.DownArrow: return RGKeyCode.DownArrow;
			    case KeyCode.RightArrow: return RGKeyCode.RightArrow;
			    case KeyCode.LeftArrow: return RGKeyCode.LeftArrow;
			    case KeyCode.Insert: return RGKeyCode.Insert;
			    case KeyCode.Home: return RGKeyCode.Home;
			    case KeyCode.End: return RGKeyCode.End;
			    case KeyCode.PageUp: return RGKeyCode.PageUp;
			    case KeyCode.PageDown: return RGKeyCode.PageDown;
			    case KeyCode.F1: return RGKeyCode.F1;
			    case KeyCode.F2: return RGKeyCode.F2;
			    case KeyCode.F3: return RGKeyCode.F3;
			    case KeyCode.F4: return RGKeyCode.F4;
			    case KeyCode.F5: return RGKeyCode.F5;
			    case KeyCode.F6: return RGKeyCode.F6;
			    case KeyCode.F7: return RGKeyCode.F7;
			    case KeyCode.F8: return RGKeyCode.F8;
			    case KeyCode.F9: return RGKeyCode.F9;
			    case KeyCode.F10: return RGKeyCode.F10;
			    case KeyCode.F11: return RGKeyCode.F11;
			    case KeyCode.F12: return RGKeyCode.F12;
			    case KeyCode.F13: return RGKeyCode.F13;
			    case KeyCode.F14: return RGKeyCode.F14;
			    case KeyCode.F15: return RGKeyCode.F15;
			    case KeyCode.Numlock: return RGKeyCode.Numlock;
			    case KeyCode.CapsLock: return RGKeyCode.CapsLock;
			    case KeyCode.ScrollLock: return RGKeyCode.ScrollLock;
			    case KeyCode.RightShift: return RGKeyCode.RightShift;
			    case KeyCode.LeftShift: return RGKeyCode.LeftShift;
			    case KeyCode.RightControl: return RGKeyCode.RightControl;
			    case KeyCode.LeftControl: return RGKeyCode.LeftControl;
			    case KeyCode.RightAlt: return RGKeyCode.RightAlt;
			    case KeyCode.LeftAlt: return RGKeyCode.LeftAlt;
			    case KeyCode.RightCommand: return RGKeyCode.RightCommand;
			    case KeyCode.LeftCommand: return RGKeyCode.LeftCommand;
			    case KeyCode.LeftWindows: return RGKeyCode.LeftWindows;
			    case KeyCode.RightWindows: return RGKeyCode.RightWindows;
			    case KeyCode.AltGr: return RGKeyCode.AltGr;
			    case KeyCode.Help: return RGKeyCode.Help;
			    case KeyCode.Print: return RGKeyCode.Print;
			    case KeyCode.SysReq: return RGKeyCode.SysReq;
			    case KeyCode.Break: return RGKeyCode.Break;
			    case KeyCode.Menu: return RGKeyCode.Menu;
			    case KeyCode.Mouse0: return RGKeyCode.Mouse0;
			    case KeyCode.Mouse1: return RGKeyCode.Mouse1;
			    case KeyCode.Mouse2: return RGKeyCode.Mouse2;
			    case KeyCode.Mouse3: return RGKeyCode.Mouse3;
			    case KeyCode.Mouse4: return RGKeyCode.Mouse4;
			    case KeyCode.Mouse5: return RGKeyCode.Mouse5;
			    case KeyCode.Mouse6: return RGKeyCode.Mouse6;
			    default:
				    RGDebug.LogWarning($"Unsupported conversion from legacy input manager key code {keyCode}");
				    return RGKeyCode.None;
		    }
	    }

	    public static KeyCode ToLegacyKeyCode(this RGKeyCode keyCode)
	    {
		    switch (keyCode)
		    {
			    case RGKeyCode.None: return KeyCode.None;
			    case RGKeyCode.Backspace: return KeyCode.Backspace;
			    case RGKeyCode.Tab: return KeyCode.Tab;
			    case RGKeyCode.Clear: return KeyCode.Clear;
			    case RGKeyCode.Return: return KeyCode.Return;
			    case RGKeyCode.Pause: return KeyCode.Pause;
			    case RGKeyCode.Escape: return KeyCode.Escape;
			    case RGKeyCode.Space: return KeyCode.Space;
			    case RGKeyCode.Exclaim: return KeyCode.Exclaim;
			    case RGKeyCode.DoubleQuote: return KeyCode.DoubleQuote;
			    case RGKeyCode.Hash: return KeyCode.Hash;
			    case RGKeyCode.Dollar: return KeyCode.Dollar;
			    case RGKeyCode.Percent: return KeyCode.Percent;
			    case RGKeyCode.Ampersand: return KeyCode.Ampersand;
			    case RGKeyCode.Quote: return KeyCode.Quote;
			    case RGKeyCode.LeftParen: return KeyCode.LeftParen;
			    case RGKeyCode.RightParen: return KeyCode.RightParen;
			    case RGKeyCode.Asterisk: return KeyCode.Asterisk;
			    case RGKeyCode.Plus: return KeyCode.Plus;
			    case RGKeyCode.Comma: return KeyCode.Comma;
			    case RGKeyCode.Minus: return KeyCode.Minus;
			    case RGKeyCode.Period: return KeyCode.Period;
			    case RGKeyCode.Slash: return KeyCode.Slash;
			    case RGKeyCode.Alpha0: return KeyCode.Alpha0;
			    case RGKeyCode.Alpha1: return KeyCode.Alpha1;
			    case RGKeyCode.Alpha2: return KeyCode.Alpha2;
			    case RGKeyCode.Alpha3: return KeyCode.Alpha3;
			    case RGKeyCode.Alpha4: return KeyCode.Alpha4;
			    case RGKeyCode.Alpha5: return KeyCode.Alpha5;
			    case RGKeyCode.Alpha6: return KeyCode.Alpha6;
			    case RGKeyCode.Alpha7: return KeyCode.Alpha7;
			    case RGKeyCode.Alpha8: return KeyCode.Alpha8;
			    case RGKeyCode.Alpha9: return KeyCode.Alpha9;
			    case RGKeyCode.Colon: return KeyCode.Colon;
			    case RGKeyCode.Semicolon: return KeyCode.Semicolon;
			    case RGKeyCode.Less: return KeyCode.Less;
			    case RGKeyCode.Equals: return KeyCode.Equals;
			    case RGKeyCode.Greater: return KeyCode.Greater;
			    case RGKeyCode.Question: return KeyCode.Question;
			    case RGKeyCode.At: return KeyCode.At;
			    case RGKeyCode.LeftBracket: return KeyCode.LeftBracket;
			    case RGKeyCode.Backslash: return KeyCode.Backslash;
			    case RGKeyCode.RightBracket: return KeyCode.RightBracket;
			    case RGKeyCode.Caret: return KeyCode.Caret;
			    case RGKeyCode.Underscore: return KeyCode.Underscore;
			    case RGKeyCode.BackQuote: return KeyCode.BackQuote;
			    case RGKeyCode.A: return KeyCode.A;
			    case RGKeyCode.B: return KeyCode.B;
			    case RGKeyCode.C: return KeyCode.C;
			    case RGKeyCode.D: return KeyCode.D;
			    case RGKeyCode.E: return KeyCode.E;
			    case RGKeyCode.F: return KeyCode.F;
			    case RGKeyCode.G: return KeyCode.G;
			    case RGKeyCode.H: return KeyCode.H;
			    case RGKeyCode.I: return KeyCode.I;
			    case RGKeyCode.J: return KeyCode.J;
			    case RGKeyCode.K: return KeyCode.K;
			    case RGKeyCode.L: return KeyCode.L;
			    case RGKeyCode.M: return KeyCode.M;
			    case RGKeyCode.N: return KeyCode.N;
			    case RGKeyCode.O: return KeyCode.O;
			    case RGKeyCode.P: return KeyCode.P;
			    case RGKeyCode.Q: return KeyCode.Q;
			    case RGKeyCode.R: return KeyCode.R;
			    case RGKeyCode.S: return KeyCode.S;
			    case RGKeyCode.T: return KeyCode.T;
			    case RGKeyCode.U: return KeyCode.U;
			    case RGKeyCode.V: return KeyCode.V;
			    case RGKeyCode.W: return KeyCode.W;
			    case RGKeyCode.X: return KeyCode.X;
			    case RGKeyCode.Y: return KeyCode.Y;
			    case RGKeyCode.Z: return KeyCode.Z;
			    case RGKeyCode.LeftCurlyBracket: return KeyCode.LeftCurlyBracket;
			    case RGKeyCode.Pipe: return KeyCode.Pipe;
			    case RGKeyCode.RightCurlyBracket: return KeyCode.RightCurlyBracket;
			    case RGKeyCode.Tilde: return KeyCode.Tilde;
			    case RGKeyCode.Delete: return KeyCode.Delete;
			    case RGKeyCode.Keypad0: return KeyCode.Keypad0;
			    case RGKeyCode.Keypad1: return KeyCode.Keypad1;
			    case RGKeyCode.Keypad2: return KeyCode.Keypad2;
			    case RGKeyCode.Keypad3: return KeyCode.Keypad3;
			    case RGKeyCode.Keypad4: return KeyCode.Keypad4;
			    case RGKeyCode.Keypad5: return KeyCode.Keypad5;
			    case RGKeyCode.Keypad6: return KeyCode.Keypad6;
			    case RGKeyCode.Keypad7: return KeyCode.Keypad7;
			    case RGKeyCode.Keypad8: return KeyCode.Keypad8;
			    case RGKeyCode.Keypad9: return KeyCode.Keypad9;
			    case RGKeyCode.KeypadPeriod: return KeyCode.KeypadPeriod;
			    case RGKeyCode.KeypadDivide: return KeyCode.KeypadDivide;
			    case RGKeyCode.KeypadMultiply: return KeyCode.KeypadMultiply;
			    case RGKeyCode.KeypadMinus: return KeyCode.KeypadMinus;
			    case RGKeyCode.KeypadPlus: return KeyCode.KeypadPlus;
			    case RGKeyCode.KeypadEnter: return KeyCode.KeypadEnter;
			    case RGKeyCode.KeypadEquals: return KeyCode.KeypadEquals;
			    case RGKeyCode.UpArrow: return KeyCode.UpArrow;
			    case RGKeyCode.DownArrow: return KeyCode.DownArrow;
			    case RGKeyCode.RightArrow: return KeyCode.RightArrow;
			    case RGKeyCode.LeftArrow: return KeyCode.LeftArrow;
			    case RGKeyCode.Insert: return KeyCode.Insert;
			    case RGKeyCode.Home: return KeyCode.Home;
			    case RGKeyCode.End: return KeyCode.End;
			    case RGKeyCode.PageUp: return KeyCode.PageUp;
			    case RGKeyCode.PageDown: return KeyCode.PageDown;
			    case RGKeyCode.F1: return KeyCode.F1;
			    case RGKeyCode.F2: return KeyCode.F2;
			    case RGKeyCode.F3: return KeyCode.F3;
			    case RGKeyCode.F4: return KeyCode.F4;
			    case RGKeyCode.F5: return KeyCode.F5;
			    case RGKeyCode.F6: return KeyCode.F6;
			    case RGKeyCode.F7: return KeyCode.F7;
			    case RGKeyCode.F8: return KeyCode.F8;
			    case RGKeyCode.F9: return KeyCode.F9;
			    case RGKeyCode.F10: return KeyCode.F10;
			    case RGKeyCode.F11: return KeyCode.F11;
			    case RGKeyCode.F12: return KeyCode.F12;
			    case RGKeyCode.F13: return KeyCode.F13;
			    case RGKeyCode.F14: return KeyCode.F14;
			    case RGKeyCode.F15: return KeyCode.F15;
			    case RGKeyCode.Numlock: return KeyCode.Numlock;
			    case RGKeyCode.CapsLock: return KeyCode.CapsLock;
			    case RGKeyCode.ScrollLock: return KeyCode.ScrollLock;
			    case RGKeyCode.RightShift: return KeyCode.RightShift;
			    case RGKeyCode.LeftShift: return KeyCode.LeftShift;
			    case RGKeyCode.RightControl: return KeyCode.RightControl;
			    case RGKeyCode.LeftControl: return KeyCode.LeftControl;
			    case RGKeyCode.RightAlt: return KeyCode.RightAlt;
			    case RGKeyCode.LeftAlt: return KeyCode.LeftAlt;
			    case RGKeyCode.RightCommand: return KeyCode.RightCommand;
			    case RGKeyCode.LeftCommand: return KeyCode.LeftCommand;
			    case RGKeyCode.LeftWindows: return KeyCode.LeftWindows;
			    case RGKeyCode.RightWindows: return KeyCode.RightWindows;
			    case RGKeyCode.AltGr: return KeyCode.AltGr;
			    case RGKeyCode.Help: return KeyCode.Help;
			    case RGKeyCode.Print: return KeyCode.Print;
			    case RGKeyCode.SysReq: return KeyCode.SysReq;
			    case RGKeyCode.Break: return KeyCode.Break;
			    case RGKeyCode.Menu: return KeyCode.Menu;
			    case RGKeyCode.Mouse0: return KeyCode.Mouse0;
			    case RGKeyCode.Mouse1: return KeyCode.Mouse1;
			    case RGKeyCode.Mouse2: return KeyCode.Mouse2;
			    case RGKeyCode.Mouse3: return KeyCode.Mouse3;
			    case RGKeyCode.Mouse4: return KeyCode.Mouse4;
			    case RGKeyCode.Mouse5: return KeyCode.Mouse5;
			    case RGKeyCode.Mouse6: return KeyCode.Mouse6;
			    default:
				    RGDebug.LogWarning($"Unsupported conversion from {keyCode} to legacy input manager key code");
				    return KeyCode.None;
		    }
	    }
		#endif
	    
	    #if ENABLE_INPUT_SYSTEM
	    public static RGKeyCode ToRGKeyCode(this Key key)
	    {
            switch (key)
            {
                case Key.None: return RGKeyCode.None;
                case Key.Space: return RGKeyCode.Space;
                case Key.Enter: return RGKeyCode.Return;
                case Key.Tab: return RGKeyCode.Tab;
                case Key.Backquote: return RGKeyCode.BackQuote;
                case Key.Quote: return RGKeyCode.Quote;
                case Key.Semicolon: return RGKeyCode.Semicolon;
                case Key.Comma: return RGKeyCode.Comma;
                case Key.Period: return RGKeyCode.Period;
                case Key.Slash: return RGKeyCode.Slash;
                case Key.Backslash: return RGKeyCode.Backslash;
                case Key.LeftBracket: return RGKeyCode.LeftBracket;
                case Key.RightBracket: return RGKeyCode.RightBracket;
                case Key.Minus: return RGKeyCode.Minus;
                case Key.Equals: return RGKeyCode.Equals;
                case Key.A: return RGKeyCode.A;
                case Key.B: return RGKeyCode.B;
                case Key.C: return RGKeyCode.C;
                case Key.D: return RGKeyCode.D;
                case Key.E: return RGKeyCode.E;
                case Key.F: return RGKeyCode.F;
                case Key.G: return RGKeyCode.G;
                case Key.H: return RGKeyCode.H;
                case Key.I: return RGKeyCode.I;
                case Key.J: return RGKeyCode.J;
                case Key.K: return RGKeyCode.K;
                case Key.L: return RGKeyCode.L;
                case Key.M: return RGKeyCode.M;
                case Key.N: return RGKeyCode.N;
                case Key.O: return RGKeyCode.O;
                case Key.P: return RGKeyCode.P;
                case Key.Q: return RGKeyCode.Q;
                case Key.R: return RGKeyCode.R;
                case Key.S: return RGKeyCode.S;
                case Key.T: return RGKeyCode.T;
                case Key.U: return RGKeyCode.U;
                case Key.V: return RGKeyCode.V;
                case Key.W: return RGKeyCode.W;
                case Key.X: return RGKeyCode.X;
                case Key.Y: return RGKeyCode.Y;
                case Key.Z: return RGKeyCode.Z;
                case Key.Digit1: return RGKeyCode.Alpha1;
                case Key.Digit2: return RGKeyCode.Alpha2;
                case Key.Digit3: return RGKeyCode.Alpha3;
                case Key.Digit4: return RGKeyCode.Alpha4;
                case Key.Digit5: return RGKeyCode.Alpha5;
                case Key.Digit6: return RGKeyCode.Alpha6;
                case Key.Digit7: return RGKeyCode.Alpha7;
                case Key.Digit8: return RGKeyCode.Alpha8;
                case Key.Digit9: return RGKeyCode.Alpha9;
                case Key.Digit0: return RGKeyCode.Alpha0;
                case Key.LeftShift: return RGKeyCode.LeftShift;
                case Key.RightShift: return RGKeyCode.RightShift;
                case Key.LeftAlt: return RGKeyCode.LeftAlt;
                case Key.RightAlt: return RGKeyCode.RightAlt;
                case Key.LeftCtrl: return RGKeyCode.LeftControl;
                case Key.RightCtrl: return RGKeyCode.RightControl;
                case Key.LeftCommand: return RGKeyCode.LeftCommand;
                case Key.RightCommand: return RGKeyCode.RightCommand;
                case Key.ContextMenu: return RGKeyCode.Menu;
                case Key.Escape: return RGKeyCode.Escape;
                case Key.LeftArrow: return RGKeyCode.LeftArrow;
                case Key.RightArrow: return RGKeyCode.RightArrow;
                case Key.UpArrow: return RGKeyCode.UpArrow;
                case Key.DownArrow: return RGKeyCode.DownArrow;
                case Key.Backspace: return RGKeyCode.Backspace;
                case Key.PageDown: return RGKeyCode.PageDown;
                case Key.PageUp: return RGKeyCode.PageUp;
                case Key.Home: return RGKeyCode.Home;
                case Key.End: return RGKeyCode.End;
                case Key.Insert: return RGKeyCode.Insert;
                case Key.Delete: return RGKeyCode.Delete;
                case Key.CapsLock: return RGKeyCode.CapsLock;
                case Key.NumLock: return RGKeyCode.Numlock;
                case Key.PrintScreen: return RGKeyCode.Print;
                case Key.ScrollLock: return RGKeyCode.ScrollLock;
                case Key.Pause: return RGKeyCode.Pause;
                case Key.NumpadEnter: return RGKeyCode.KeypadEnter;
                case Key.NumpadDivide: return RGKeyCode.KeypadDivide;
                case Key.NumpadMultiply: return RGKeyCode.KeypadMultiply;
                case Key.NumpadPlus: return RGKeyCode.KeypadPlus;
                case Key.NumpadMinus: return RGKeyCode.KeypadMinus;
                case Key.NumpadPeriod: return RGKeyCode.KeypadPeriod;
                case Key.NumpadEquals: return RGKeyCode.KeypadEquals;
                case Key.Numpad0: return RGKeyCode.Keypad0;
                case Key.Numpad1: return RGKeyCode.Keypad1;
                case Key.Numpad2: return RGKeyCode.Keypad2;
                case Key.Numpad3: return RGKeyCode.Keypad3;
                case Key.Numpad4: return RGKeyCode.Keypad4;
                case Key.Numpad5: return RGKeyCode.Keypad5;
                case Key.Numpad6: return RGKeyCode.Keypad6;
                case Key.Numpad7: return RGKeyCode.Keypad7;
                case Key.Numpad8: return RGKeyCode.Keypad8;
                case Key.Numpad9: return RGKeyCode.Keypad9;
                case Key.F1: return RGKeyCode.F1;
                case Key.F2: return RGKeyCode.F2;
                case Key.F3: return RGKeyCode.F3;
                case Key.F4: return RGKeyCode.F4;
                case Key.F5: return RGKeyCode.F5;
                case Key.F6: return RGKeyCode.F6;
                case Key.F7: return RGKeyCode.F7;
                case Key.F8: return RGKeyCode.F8;
                case Key.F9: return RGKeyCode.F9;
                case Key.F10: return RGKeyCode.F10;
                case Key.F11: return RGKeyCode.F11;
                case Key.F12: return RGKeyCode.F12;
                default: 
				    RGDebug.LogWarning($"Unsupported conversion from input system key code {key}");
                    return RGKeyCode.None;
            }
	    }

	    public static Key ToInputSystemKeyCode(this RGKeyCode keyCode)
	    {
		    switch (keyCode)
		    {
			    case RGKeyCode.None: return Key.None;
			    case RGKeyCode.Space: return Key.Space;
			    case RGKeyCode.Return: return Key.Enter;
			    case RGKeyCode.Tab: return Key.Tab;
			    case RGKeyCode.BackQuote: return Key.Backquote;
			    case RGKeyCode.Quote: return Key.Quote;
			    case RGKeyCode.Semicolon: return Key.Semicolon;
			    case RGKeyCode.Comma: return Key.Comma;
			    case RGKeyCode.Period: return Key.Period;
			    case RGKeyCode.Slash: return Key.Slash;
			    case RGKeyCode.Backslash: return Key.Backslash;
			    case RGKeyCode.LeftBracket: return Key.LeftBracket;
			    case RGKeyCode.RightBracket: return Key.RightBracket;
			    case RGKeyCode.Minus: return Key.Minus;
			    case RGKeyCode.Equals: return Key.Equals;
			    case RGKeyCode.A: return Key.A;
			    case RGKeyCode.B: return Key.B;
			    case RGKeyCode.C: return Key.C;
			    case RGKeyCode.D: return Key.D;
			    case RGKeyCode.E: return Key.E;
			    case RGKeyCode.F: return Key.F;
			    case RGKeyCode.G: return Key.G;
			    case RGKeyCode.H: return Key.H;
			    case RGKeyCode.I: return Key.I;
			    case RGKeyCode.J: return Key.J;
			    case RGKeyCode.K: return Key.K;
			    case RGKeyCode.L: return Key.L;
			    case RGKeyCode.M: return Key.M;
			    case RGKeyCode.N: return Key.N;
			    case RGKeyCode.O: return Key.O;
			    case RGKeyCode.P: return Key.P;
			    case RGKeyCode.Q: return Key.Q;
			    case RGKeyCode.R: return Key.R;
			    case RGKeyCode.S: return Key.S;
			    case RGKeyCode.T: return Key.T;
			    case RGKeyCode.U: return Key.U;
			    case RGKeyCode.V: return Key.V;
			    case RGKeyCode.W: return Key.W;
			    case RGKeyCode.X: return Key.X;
			    case RGKeyCode.Y: return Key.Y;
			    case RGKeyCode.Z: return Key.Z;
			    case RGKeyCode.Alpha1: return Key.Digit1;
			    case RGKeyCode.Alpha2: return Key.Digit2;
			    case RGKeyCode.Alpha3: return Key.Digit3;
			    case RGKeyCode.Alpha4: return Key.Digit4;
			    case RGKeyCode.Alpha5: return Key.Digit5;
			    case RGKeyCode.Alpha6: return Key.Digit6;
			    case RGKeyCode.Alpha7: return Key.Digit7;
			    case RGKeyCode.Alpha8: return Key.Digit8;
			    case RGKeyCode.Alpha9: return Key.Digit9;
			    case RGKeyCode.Alpha0: return Key.Digit0;
			    case RGKeyCode.LeftShift: return Key.LeftShift;
			    case RGKeyCode.RightShift: return Key.RightShift;
			    case RGKeyCode.LeftAlt: return Key.LeftAlt;
			    case RGKeyCode.RightAlt: return Key.RightAlt;
			    case RGKeyCode.LeftControl: return Key.LeftCtrl;
			    case RGKeyCode.RightControl: return Key.RightCtrl;
			    case RGKeyCode.LeftCommand: return Key.LeftCommand;
			    case RGKeyCode.RightCommand: return Key.RightCommand;
			    case RGKeyCode.Menu: return Key.ContextMenu;
			    case RGKeyCode.Escape: return Key.Escape;
			    case RGKeyCode.LeftArrow: return Key.LeftArrow;
			    case RGKeyCode.RightArrow: return Key.RightArrow;
			    case RGKeyCode.UpArrow: return Key.UpArrow;
			    case RGKeyCode.DownArrow: return Key.DownArrow;
			    case RGKeyCode.Backspace: return Key.Backspace;
			    case RGKeyCode.PageDown: return Key.PageDown;
			    case RGKeyCode.PageUp: return Key.PageUp;
			    case RGKeyCode.Home: return Key.Home;
			    case RGKeyCode.End: return Key.End;
			    case RGKeyCode.Insert: return Key.Insert;
			    case RGKeyCode.Delete: return Key.Delete;
			    case RGKeyCode.CapsLock: return Key.CapsLock;
			    case RGKeyCode.Numlock: return Key.NumLock;
			    case RGKeyCode.Print: return Key.PrintScreen;
			    case RGKeyCode.ScrollLock: return Key.ScrollLock;
			    case RGKeyCode.Pause: return Key.Pause;
			    case RGKeyCode.KeypadEnter: return Key.NumpadEnter;
			    case RGKeyCode.KeypadDivide: return Key.NumpadDivide;
			    case RGKeyCode.KeypadMultiply: return Key.NumpadMultiply;
			    case RGKeyCode.KeypadPlus: return Key.NumpadPlus;
			    case RGKeyCode.KeypadMinus: return Key.NumpadMinus;
			    case RGKeyCode.KeypadPeriod: return Key.NumpadPeriod;
			    case RGKeyCode.KeypadEquals: return Key.NumpadEquals;
			    case RGKeyCode.Keypad0: return Key.Numpad0;
			    case RGKeyCode.Keypad1: return Key.Numpad1;
			    case RGKeyCode.Keypad2: return Key.Numpad2;
			    case RGKeyCode.Keypad3: return Key.Numpad3;
			    case RGKeyCode.Keypad4: return Key.Numpad4;
			    case RGKeyCode.Keypad5: return Key.Numpad5;
			    case RGKeyCode.Keypad6: return Key.Numpad6;
			    case RGKeyCode.Keypad7: return Key.Numpad7;
			    case RGKeyCode.Keypad8: return Key.Numpad8;
			    case RGKeyCode.Keypad9: return Key.Numpad9;
			    case RGKeyCode.F1: return Key.F1;
			    case RGKeyCode.F2: return Key.F2;
			    case RGKeyCode.F3: return Key.F3;
			    case RGKeyCode.F4: return Key.F4;
			    case RGKeyCode.F5: return Key.F5;
			    case RGKeyCode.F6: return Key.F6;
			    case RGKeyCode.F7: return Key.F7;
			    case RGKeyCode.F8: return Key.F8;
			    case RGKeyCode.F9: return Key.F9;
			    case RGKeyCode.F10: return Key.F10;
			    case RGKeyCode.F11: return Key.F11;
			    case RGKeyCode.F12: return Key.F12;
			    default: 
				    RGDebug.LogWarning($"Unsupported conversion from {keyCode} to new input system key code");
				    return Key.None;
		    }
	    }
	    #endif

	    public static bool IsMouseButton(this RGKeyCode keyCode)
	    {
		    return keyCode >= RGKeyCode.Mouse0 && keyCode <= RGKeyCode.Mouse6;
	    }
    }
}